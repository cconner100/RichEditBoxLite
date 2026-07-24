using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
        var stack = new Stack<ParserState>();
        var state = new ParserState(new CharacterFormatState(), false, 1);
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

                    if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or "mmath" or "xmlopen")
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
                        case "par":
                        case "line": text.Append('\n'); break;
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
        document.ReplaceFromCodec(text.ToString(), runs);
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

        foreach (var run in document.Runs.Count > 0 ? document.Runs : [new FormatRun(0, document.Length, document.DefaultCharacterFormat)])
        {
            var format = run.Format;
            builder.Append(@"\f0\fs").Append((int)Math.Round(format.Size * 2));
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
            AppendEscaped(builder, document.GetText(run.Start, run.Length));
            builder.Append(@"\plain ");
        }

        return builder.Append('}').ToString();
    }

    private static void AppendEscaped(StringBuilder builder, string text)
    {
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\\': builder.Append(@"\\"); break;
                case '{': builder.Append(@"\{"); break;
                case '}': builder.Append(@"\}"); break;
                case '\n': builder.Append(@"\par "); break;
                case '\t': builder.Append(@"\tab "); break;
                default:
                    if (ch <= 0x7f) builder.Append(ch);
                    else builder.Append(@"\u").Append((short)ch).Append('?');
                    break;
            }
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
