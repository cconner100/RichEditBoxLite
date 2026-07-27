using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Windows.UI;

namespace CConner100.RichEditBoxLite;

internal static partial class RtfCodec
{
    private const int MaximumInputLength = 16 * 1024 * 1024;
    private const int MaximumGroupDepth = 256;

    public static void Import(RichEditTextDocument document, string rtf)
    {
        ArgumentNullException.ThrowIfNull(rtf);
        if (rtf.Length > MaximumInputLength)
        {
            throw new InvalidDataException($"RTF exceeds the {MaximumInputLength}-character safety limit.");
        }

        if (!rtf.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal))
        {
            document.ReplaceFromCodec(rtf, []);
            return;
        }

        var colors = ParseColorTable(rtf);
        var text = new StringBuilder();
        var runs = new List<FormatRun>();
        var paragraphs = new Dictionary<int, ParagraphFormatState>();
        var stack = new Stack<ParserState>();
        var state = new ParserState(new CharacterFormatState(), false, 1);
        var paragraphFormat = new ParagraphFormatState();
        var paragraphStart = 0;
        var runStart = 0;

        void Flush()
        {
            if (text.Length > runStart)
            {
                runs.Add(new FormatRun(runStart, text.Length - runStart, state.Format));
                runStart = text.Length;
            }
        }

        for (var i = 0; i < rtf.Length;)
        {
            var ch = rtf[i++];
            switch (ch)
            {
                case '{':
                    if (stack.Count >= MaximumGroupDepth)
                    {
                        throw new InvalidDataException($"RTF nesting exceeds the {MaximumGroupDepth}-group safety limit.");
                    }
                    stack.Push(state);
                    break;
                case '}':
                    Flush();
                    if (stack.Count > 0) state = stack.Pop();
                    break;
                case '\\':
                    if (i >= rtf.Length) break;
                    if (rtf[i] is '\\' or '{' or '}')
                    {
                        if (!state.Skip) text.Append(rtf[i]);
                        i++;
                        break;
                    }
                    if (rtf[i] == '\'')
                    {
                        i++;
                        if (i + 1 < rtf.Length && byte.TryParse(rtf.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) && !state.Skip)
                        {
                            text.Append((char)value);
                        }
                        i += Math.Min(2, rtf.Length - i);
                        break;
                    }
                    if (rtf[i] == '*')
                    {
                        i++;
                        state = state with { Skip = true };
                        break;
                    }

                    var wordStart = i;
                    while (i < rtf.Length && char.IsLetter(rtf[i])) i++;
                    var word = rtf[wordStart..i];
                    var sign = 1;
                    if (i < rtf.Length && rtf[i] == '-') { sign = -1; i++; }
                    var numberStart = i;
                    while (i < rtf.Length && char.IsDigit(rtf[i])) i++;
                    int? parameter = numberStart < i && int.TryParse(rtf[numberStart..i], out var number) ? number * sign : null;
                    if (i < rtf.Length && rtf[i] == ' ') i++;

                    if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or "mmath" or "xmlopen" or "listtable" or "listoverridetable" or "listtext" or "pntxtb" or "pntxta")
                    {
                        state = state with { Skip = true };
                        break;
                    }
                    if (state.Skip) break;

                    switch (word)
                    {
                        case "b": Flush(); state = state with { Format = state.Format with { Bold = parameter != 0 } }; break;
                        case "i": Flush(); state = state with { Format = state.Format with { Italic = parameter != 0 } }; break;
                        case "ul": Flush(); state = state with { Format = state.Format with { Underline = parameter != 0 } }; break;
                        case "ulnone": Flush(); state = state with { Format = state.Format with { Underline = false } }; break;
                        case "strike": Flush(); state = state with { Format = state.Format with { Strikethrough = parameter != 0 } }; break;
                        case "sub": Flush(); state = state with { Format = state.Format with { Subscript = true, Superscript = false } }; break;
                        case "super": Flush(); state = state with { Format = state.Format with { Superscript = true, Subscript = false } }; break;
                        case "nosupersub": Flush(); state = state with { Format = state.Format with { Superscript = false, Subscript = false } }; break;
                        case "fs" when parameter is not null: Flush(); state = state with { Format = state.Format with { Size = Math.Max(1, parameter.Value / 2f) } }; break;
                        case "cf" when parameter is not null && parameter.Value < colors.Count: Flush(); state = state with { Format = state.Format with { ForegroundColor = colors[parameter.Value] } }; break;
                        case "highlight" when parameter is not null && parameter.Value < colors.Count: Flush(); state = state with { Format = state.Format with { BackgroundColor = colors[parameter.Value] } }; break;
                        case "plain": Flush(); state = state with { Format = new CharacterFormatState() }; break;
                        case "pard": paragraphFormat = new ParagraphFormatState(); break;
                        case "ql": paragraphFormat = paragraphFormat with { Alignment = ParagraphAlignment.Left }; break;
                        case "qc": paragraphFormat = paragraphFormat with { Alignment = ParagraphAlignment.Center }; break;
                        case "qr": paragraphFormat = paragraphFormat with { Alignment = ParagraphAlignment.Right }; break;
                        case "qj": paragraphFormat = paragraphFormat with { Alignment = ParagraphAlignment.Justify }; break;
                        case "fi" when parameter is not null: paragraphFormat = paragraphFormat with { FirstLineIndent = parameter.Value / 20f }; break;
                        case "li" when parameter is not null: paragraphFormat = paragraphFormat with { LeftIndent = parameter.Value / 20f }; break;
                        case "ri" when parameter is not null: paragraphFormat = paragraphFormat with { RightIndent = parameter.Value / 20f }; break;
                        case "sb" when parameter is not null: paragraphFormat = paragraphFormat with { SpaceBefore = parameter.Value / 20f }; break;
                        case "sa" when parameter is not null: paragraphFormat = paragraphFormat with { SpaceAfter = parameter.Value / 20f }; break;
                        case "sl" when parameter is not null: paragraphFormat = paragraphFormat with { LineSpacing = parameter.Value / 20f }; break;
                        case "outlinelevel" when parameter is not null:
                            paragraphFormat = paragraphFormat with
                            {
                                HeadingLevel = parameter.Value switch
                                {
                                    0 => RichTextHeadingLevel.Heading1,
                                    1 => RichTextHeadingLevel.Heading2,
                                    _ => RichTextHeadingLevel.None
                                }
                            };
                            break;
                        case "pnlvlblt": paragraphFormat = paragraphFormat with { ListType = MarkerType.Bullet }; break;
                        case "pnlvlbody": paragraphFormat = paragraphFormat with { ListType = MarkerType.Arabic }; break;
                        case "pnstart" when parameter is not null: paragraphFormat = paragraphFormat with { ListStart = Math.Max(1, parameter.Value) }; break;
                        case "par":
                        case "line":
                            paragraphs[paragraphStart] = paragraphFormat;
                            text.Append('\n');
                            paragraphStart = text.Length;
                            paragraphFormat = new ParagraphFormatState();
                            break;
                        case "tab": text.Append('\t'); break;
                        case "emdash": text.Append('—'); break;
                        case "endash": text.Append('–'); break;
                        case "bullet": text.Append('•'); break;
                        case "lquote": text.Append('‘'); break;
                        case "rquote": text.Append('’'); break;
                        case "ldblquote": text.Append('“'); break;
                        case "rdblquote": text.Append('”'); break;
                        case "u" when parameter is not null:
                            text.Append((char)(parameter.Value < 0 ? parameter.Value + 65536 : parameter.Value));
                            for (var skipped = 0; skipped < state.UnicodeFallbackLength && i < rtf.Length && rtf[i] is not '\\' and not '{' and not '}'; skipped++, i++) { }
                            break;
                        case "uc" when parameter is not null: state = state with { UnicodeFallbackLength = Math.Max(0, parameter.Value) }; break;
                    }
                    break;
                case '\r':
                case '\n':
                    break;
                default:
                    if (!state.Skip) text.Append(ch);
                    break;
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidDataException("RTF contains unbalanced groups.");
        }

        Flush();
        if (text.Length > 0 || paragraphFormat != new ParagraphFormatState())
        {
            paragraphs[paragraphStart] = paragraphFormat;
        }
        document.ReplaceFromCodec(text.ToString(), runs, paragraphs);
    }

    public static string Export(RichEditTextDocument document)
    {
        var colors = document.Runs
            .SelectMany(r => new[] { r.Format.ForegroundColor, r.Format.BackgroundColor })
            .Where(c => c.A != 0)
            .Distinct()
            .ToList();

        var builder = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Open Sans;}}");
        builder.Append(@"{\colortbl;");
        foreach (var color in colors)
        {
            builder.Append(@"\red").Append(color.R).Append(@"\green").Append(color.G).Append(@"\blue").Append(color.B).Append(';');
        }
        builder.Append('}');

        CharacterFormatState? activeFormat = null;
        for (var position = 0; position < document.Length; position++)
        {
            if (position == 0 || document.Text[position - 1] == '\n')
            {
                AppendParagraphFormat(builder, document.GetParagraphFormat(position));
                activeFormat = null;
            }

            var format = document.GetCharacterFormat(position);
            if (format != activeFormat)
            {
                AppendCharacterFormat(builder, format, colors);
                activeFormat = format;
            }

            var ch = document.Text[position];
            if (ch == '\n')
            {
                builder.Append(@"\par ");
                activeFormat = null;
            }
            else
            {
                AppendEscaped(builder, ch);
            }
        }

        return builder.Append('}').ToString();
    }

    private static void AppendCharacterFormat(StringBuilder builder, CharacterFormatState format, List<Color> colors)
    {
        builder.Append(@"\plain\f0\fs").Append((int)Math.Round(format.Size * 2));
        if (format.Bold) builder.Append(@"\b");
        if (format.Italic) builder.Append(@"\i");
        if (format.Underline) builder.Append(@"\ul");
        if (format.Strikethrough) builder.Append(@"\strike");
        if (format.Subscript) builder.Append(@"\sub");
        if (format.Superscript) builder.Append(@"\super");
        var foreground = colors.IndexOf(format.ForegroundColor);
        if (foreground >= 0) builder.Append(@"\cf").Append(foreground + 1);
        var background = colors.IndexOf(format.BackgroundColor);
        if (background >= 0) builder.Append(@"\highlight").Append(background + 1);
        builder.Append(' ');
    }

    private static void AppendParagraphFormat(StringBuilder builder, ParagraphFormatState format)
    {
        builder.Append(@"\pard");
        builder.Append(format.Alignment switch
        {
            ParagraphAlignment.Center => @"\qc",
            ParagraphAlignment.Right => @"\qr",
            ParagraphAlignment.Justify => @"\qj",
            _ => @"\ql"
        });
        if (format.FirstLineIndent != 0) builder.Append(@"\fi").Append(ToTwips(format.FirstLineIndent));
        if (format.LeftIndent != 0) builder.Append(@"\li").Append(ToTwips(format.LeftIndent));
        if (format.RightIndent != 0) builder.Append(@"\ri").Append(ToTwips(format.RightIndent));
        if (format.SpaceBefore != 0) builder.Append(@"\sb").Append(ToTwips(format.SpaceBefore));
        if (format.SpaceAfter != 0) builder.Append(@"\sa").Append(ToTwips(format.SpaceAfter));
        if (format.LineSpacing != 0) builder.Append(@"\sl").Append(ToTwips(format.LineSpacing));
        if (format.HeadingLevel == RichTextHeadingLevel.Heading1) builder.Append(@"\outlinelevel0");
        if (format.HeadingLevel == RichTextHeadingLevel.Heading2) builder.Append(@"\outlinelevel1");
        if (format.ListType == MarkerType.Bullet)
        {
            builder.Append(@"{\pn\pnlvlblt\pnstart1\pnindent360{\pntxtb\bullet}}");
        }
        else if (format.ListType == MarkerType.Arabic)
        {
            builder.Append(@"{\pn\pnlvlbody\pnstart").Append(Math.Max(1, format.ListStart)).Append(@"\pnindent360{\pntxta .}}");
        }
    }

    private static int ToTwips(float value) => (int)Math.Round(value * 20);

    private static void AppendEscaped(StringBuilder builder, char ch)
    {
        switch (ch)
        {
            case '\\': builder.Append(@"\\"); break;
            case '{': builder.Append(@"\{"); break;
            case '}': builder.Append(@"\}"); break;
            case '\t': builder.Append(@"\tab "); break;
            default:
                if (ch <= 0x7f) builder.Append(ch);
                else builder.Append(@"\u").Append((short)ch).Append('?');
                break;
        }
    }

    private static List<Color> ParseColorTable(string rtf)
    {
        var colors = new List<Color> { Color.FromArgb(255, 0, 0, 0) };
        var match = ColorTableRegex().Match(rtf);
        if (!match.Success) return colors;
        foreach (Match color in ColorRegex().Matches(match.Value))
        {
            colors.Add(Color.FromArgb(255,
                byte.Parse(color.Groups[1].Value, CultureInfo.InvariantCulture),
                byte.Parse(color.Groups[2].Value, CultureInfo.InvariantCulture),
                byte.Parse(color.Groups[3].Value, CultureInfo.InvariantCulture)));
        }
        return colors;
    }

    [GeneratedRegex(@"\\colortbl(?<body>[^}]*)\}", RegexOptions.Compiled)]
    private static partial Regex ColorTableRegex();

    [GeneratedRegex(@"\\red(\d+)\\green(\d+)\\blue(\d+);", RegexOptions.Compiled)]
    private static partial Regex ColorRegex();

    private readonly record struct ParserState(CharacterFormatState Format, bool Skip, int UnicodeFallbackLength);
}
