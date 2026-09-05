using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.UI.Text;
using Windows.UI;

namespace CConner100.RichEditBoxLite;

/// <summary>
/// Bounded, sanitized HTML codec. Imports a supported HTML profile into the
/// document model and exports the model back to canonical HTML. The same
/// parser backs <see cref="RichEditTextDocument.SetHtml"/> and HTML clipboard
/// paste, so downstream consumers never need their own HTML adapter.
/// Remote resources are never loaded; unsafe elements are discarded.
/// </summary>
internal static class HtmlCodec
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.Ordinal)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol",
        "table", "tr", "thead", "tbody", "tfoot", "caption", "center", "blockquote", "pre",
        "address", "article", "aside", "section", "header", "footer", "main",
        "figure", "figcaption", "form", "fieldset", "dl", "dt", "dd", "nav"
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.Ordinal)
    {
        "script", "style", "title", "textarea", "xmp", "noscript", "noembed"
    };

    private static readonly HashSet<string> SkippedSubtreeElements = new(StringComparer.Ordinal)
    {
        "iframe", "object", "applet", "svg", "math", "head", "select", "template",
        "audio", "video", "canvas", "map", "picture", "datalist"
    };

    private static readonly HashSet<string> VoidElements = new(StringComparer.Ordinal)
    {
        "br", "hr", "img", "input", "area", "base", "col", "embed", "link",
        "meta", "param", "source", "track", "wbr"
    };

    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = Color.FromArgb(255, 0, 0, 0),
        ["silver"] = Color.FromArgb(255, 192, 192, 192),
        ["gray"] = Color.FromArgb(255, 128, 128, 128),
        ["grey"] = Color.FromArgb(255, 128, 128, 128),
        ["white"] = Color.FromArgb(255, 255, 255, 255),
        ["maroon"] = Color.FromArgb(255, 128, 0, 0),
        ["red"] = Color.FromArgb(255, 255, 0, 0),
        ["purple"] = Color.FromArgb(255, 128, 0, 128),
        ["fuchsia"] = Color.FromArgb(255, 255, 0, 255),
        ["magenta"] = Color.FromArgb(255, 255, 0, 255),
        ["green"] = Color.FromArgb(255, 0, 128, 0),
        ["lime"] = Color.FromArgb(255, 0, 255, 0),
        ["olive"] = Color.FromArgb(255, 128, 128, 0),
        ["yellow"] = Color.FromArgb(255, 255, 255, 0),
        ["navy"] = Color.FromArgb(255, 0, 0, 128),
        ["blue"] = Color.FromArgb(255, 0, 0, 255),
        ["teal"] = Color.FromArgb(255, 0, 128, 128),
        ["aqua"] = Color.FromArgb(255, 0, 255, 255),
        ["cyan"] = Color.FromArgb(255, 0, 255, 255),
        ["orange"] = Color.FromArgb(255, 255, 165, 0)
    };

    private static readonly float[] FontTagSizes = [8, 10, 12, 14, 18, 24, 36];

    internal sealed record HtmlFragment(
        string Text,
        IReadOnlyList<FormatRun> Runs,
        IReadOnlyDictionary<int, ParagraphFormatState> Paragraphs);

    private readonly record struct ElementEntry(
        string Name,
        CharacterFormatState Format,
        ParagraphFormatState Ambient,
        bool Skip,
        MarkerType ListType,
        int ListStart);

    public static void Import(RichEditTextDocument document, string html)
    {
        var fragment = Parse(html);
        document.ReplaceFromCodec(fragment.Text, fragment.Runs, fragment.Paragraphs);
    }

    /// <summary>
    /// Extracts the payload from a CF_HTML clipboard string (Version/StartHTML
    /// headers and StartFragment/EndFragment markers) or returns plain HTML as-is.
    /// </summary>
    public static string ExtractClipboardFragment(string clipboardHtml)
    {
        if (string.IsNullOrEmpty(clipboardHtml)) return string.Empty;
        var startMarker = clipboardHtml.IndexOf("<!--StartFragment", StringComparison.OrdinalIgnoreCase);
        if (startMarker >= 0)
        {
            var open = clipboardHtml.IndexOf("-->", startMarker, StringComparison.Ordinal);
            if (open >= 0)
            {
                var start = open + 3;
                var end = clipboardHtml.IndexOf("<!--EndFragment", start, StringComparison.OrdinalIgnoreCase);
                return clipboardHtml[start..(end >= start ? end : clipboardHtml.Length)];
            }
        }
        if (clipboardHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
        {
            var firstTag = clipboardHtml.IndexOf('<');
            return firstTag >= 0 ? clipboardHtml[firstTag..] : string.Empty;
        }
        return clipboardHtml;
    }

    public static HtmlFragment Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (html.Length > CodecLimits.MaximumInputLength)
        {
            throw new InvalidDataException($"HTML exceeds the {CodecLimits.MaximumInputLength}-character safety limit.");
        }

        var text = new StringBuilder();
        var runs = new List<FormatRun>();
        var paragraphs = new Dictionary<int, ParagraphFormatState>();
        var stack = new Stack<ElementEntry>();
        var currentFormat = new CharacterFormatState();
        var activeFormat = currentFormat;
        var ambient = new ParagraphFormatState();
        var paragraphFormat = ambient;
        var paragraphStart = 0;
        var runStart = 0;
        var skip = false;
        var pendingBreak = false;
        var pendingSpace = false;

        void FlushRun()
        {
            if (text.Length > runStart)
            {
                runs.Add(new FormatRun(runStart, text.Length - runStart, activeFormat));
                runStart = text.Length;
            }
        }

        void EnsureFormat()
        {
            if (activeFormat != currentFormat)
            {
                FlushRun();
                activeFormat = currentFormat;
            }
        }

        void CommitParagraph()
        {
            paragraphs[paragraphStart] = paragraphFormat;
            EnsureFormat();
            text.Append('\n');
            paragraphStart = text.Length;
            paragraphFormat = ambient;
            pendingBreak = false;
            pendingSpace = false;
        }

        void AppendVisible(string value)
        {
            if (skip || value.Length == 0) return;
            if (pendingBreak)
            {
                CommitParagraph();
            }
            else if (pendingSpace && text.Length > paragraphStart)
            {
                EnsureFormat();
                text.Append(' ');
            }
            pendingSpace = false;
            EnsureFormat();
            text.Append(value);
        }

        void CloseElement(string name)
        {
            var found = false;
            foreach (var entry in stack)
            {
                if (entry.Name == name) { found = true; break; }
            }
            if (!found) return;
            ElementEntry popped;
            do { popped = stack.Pop(); } while (popped.Name != name);
            currentFormat = popped.Format;
            ambient = popped.Ambient;
            skip = popped.Skip;
            if (skip) return;
            if (BlockElements.Contains(name)) pendingBreak = true;
            if (name is "td" or "th") pendingSpace = true;
        }

        // A block-level open implies closing any dangling <p>/<h#> (and <li> when
        // opening a sibling <li>) the way browsers do, so repeated unclosed tags
        // cannot masquerade as nesting depth.
        void ImpliedClose(string name)
        {
            foreach (var entry in stack)
            {
                if (name == "li" && entry.ListType != MarkerType.None) return;
                var candidate = entry.Name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    || (name == "li" && entry.Name == "li");
                if (candidate)
                {
                    CloseElement(entry.Name);
                    return;
                }
                if (BlockElements.Contains(entry.Name)) return;
            }
        }

        void OpenElement(string name, bool selfClosing, string? style, string? align, string? color, string? face, string? size, string? start)
        {
            if (stack.Count >= CodecLimits.MaximumNestingDepth)
            {
                throw new InvalidDataException($"HTML nesting exceeds the {CodecLimits.MaximumNestingDepth}-element safety limit.");
            }
            if (skip)
            {
                // Inside a discarded subtree elements are tracked only for balance.
                if (!selfClosing) stack.Push(new ElementEntry(name, currentFormat, ambient, skip, MarkerType.None, 1));
                return;
            }
            var isBlock = BlockElements.Contains(name);
            if (isBlock)
            {
                ImpliedClose(name);
                if (pendingBreak || text.Length > paragraphStart) CommitParagraph();
            }
            stack.Push(new ElementEntry(
                name, currentFormat, ambient, skip,
                name == "ul" ? MarkerType.Bullet : name == "ol" ? MarkerType.Arabic : MarkerType.None,
                name == "ol" && int.TryParse(start, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart) && parsedStart > 0 ? parsedStart : 1));
            if (SkippedSubtreeElements.Contains(name)) skip = true;

            if (isBlock)
            {
                var derived = ambient;
                switch (name)
                {
                    case "h1": derived = derived with { HeadingLevel = RichTextHeadingLevel.Heading1 }; break;
                    case "h2" or "h3" or "h4" or "h5" or "h6": derived = derived with { HeadingLevel = RichTextHeadingLevel.Heading2 }; break;
                    case "center": derived = derived with { Alignment = ParagraphAlignment.Center }; break;
                    case "li":
                        var listType = MarkerType.Bullet;
                        var listStart = 1;
                        var listDepth = 0;
                        foreach (var entry in stack)
                        {
                            if (entry.ListType == MarkerType.None) continue;
                            if (listDepth == 0)
                            {
                                listType = entry.ListType;
                                listStart = entry.ListStart;
                            }
                            listDepth++;
                        }
                        derived = derived with
                        {
                            ListType = listType,
                            ListStart = listStart,
                            LeftIndent = 24f * Math.Max(0, listDepth - 1)
                        };
                        break;
                }
                var alignment = ParseAlignment(align) ?? ParseAlignmentFromStyle(style);
                if (alignment is not null) derived = derived with { Alignment = alignment.Value };
                ambient = derived;
                paragraphFormat = derived;
            }

            switch (name)
            {
                case "b" or "strong": currentFormat = currentFormat with { Bold = true }; break;
                case "i" or "em": currentFormat = currentFormat with { Italic = true }; break;
                case "u" or "ins": currentFormat = currentFormat with { Underline = true }; break;
                case "s" or "strike" or "del": currentFormat = currentFormat with { Strikethrough = true }; break;
                case "sub": currentFormat = currentFormat with { Subscript = true, Superscript = false }; break;
                case "sup": currentFormat = currentFormat with { Superscript = true, Subscript = false }; break;
                case "td" or "th": pendingSpace = true; break;
                case "font":
                    if (TryParseColor(color, out var fontColor)) currentFormat = currentFormat with { ForegroundColor = fontColor };
                    if (!string.IsNullOrWhiteSpace(face)) currentFormat = currentFormat with { FontFamily = FirstFontFamily(face) };
                    if (int.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fontSize) && fontSize is >= 1 and <= 7)
                    {
                        currentFormat = currentFormat with { Size = FontTagSizes[fontSize - 1] };
                    }
                    break;
            }
            if (!string.IsNullOrEmpty(style)) currentFormat = ApplyStyles(currentFormat, style);
            if (selfClosing) CloseElement(name);
        }

        var i = 0;
        while (i < html.Length)
        {
            var ch = html[i];
            if (ch == '<' && i + 1 < html.Length)
            {
                var next = html[i + 1];
                if (next == '!')
                {
                    if (i + 3 < html.Length && html[i + 2] == '-' && html[i + 3] == '-')
                    {
                        var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                        i = end < 0 ? html.Length : end + 3;
                    }
                    else
                    {
                        var end = html.IndexOf('>', i + 1);
                        i = end < 0 ? html.Length : end + 1;
                    }
                    continue;
                }
                if (next == '?')
                {
                    var end = html.IndexOf('>', i + 1);
                    i = end < 0 ? html.Length : end + 1;
                    continue;
                }
                if (next == '/')
                {
                    var nameStart = i + 2;
                    var j = nameStart;
                    while (j < html.Length && char.IsAsciiLetterOrDigit(html[j])) j++;
                    var closeName = html[nameStart..j].ToLowerInvariant();
                    var end = html.IndexOf('>', j);
                    i = end < 0 ? html.Length : end + 1;
                    if (closeName.Length > 0) CloseElement(closeName);
                    continue;
                }
                if (char.IsAsciiLetter(next))
                {
                    var nameStart = i + 1;
                    var j = nameStart;
                    while (j < html.Length && char.IsAsciiLetterOrDigit(html[j])) j++;
                    var name = html[nameStart..j].ToLowerInvariant();
                    i = j;
                    var selfClosing = false;
                    string? styleAttr = null, alignAttr = null, colorAttr = null, faceAttr = null, sizeAttr = null, startAttr = null;
                    while (i < html.Length && html[i] != '>')
                    {
                        var c = html[i];
                        if (c == '/') { selfClosing = true; i++; continue; }
                        if (IsHtmlWhitespace(c)) { i++; continue; }
                        var attrStart = i;
                        while (i < html.Length && html[i] is not ('=' or '>' or '/') && !IsHtmlWhitespace(html[i])) i++;
                        var attrName = html[attrStart..i].ToLowerInvariant();
                        while (i < html.Length && IsHtmlWhitespace(html[i])) i++;
                        var attrValue = string.Empty;
                        if (i < html.Length && html[i] == '=')
                        {
                            i++;
                            while (i < html.Length && IsHtmlWhitespace(html[i])) i++;
                            if (i < html.Length && html[i] is '"' or '\'')
                            {
                                var quote = html[i++];
                                var valueStart = i;
                                while (i < html.Length && html[i] != quote) i++;
                                attrValue = html[valueStart..i];
                                if (i < html.Length) i++;
                            }
                            else
                            {
                                var valueStart = i;
                                while (i < html.Length && html[i] != '>' && !IsHtmlWhitespace(html[i])) i++;
                                attrValue = html[valueStart..i];
                            }
                        }
                        switch (attrName)
                        {
                            case "style": styleAttr = WebUtility.HtmlDecode(attrValue); break;
                            case "align": alignAttr = attrValue; break;
                            case "color": colorAttr = attrValue; break;
                            case "face": faceAttr = attrValue; break;
                            case "size": sizeAttr = attrValue; break;
                            case "start": startAttr = attrValue; break;
                        }
                    }
                    if (i < html.Length) i++;

                    if (RawTextElements.Contains(name))
                    {
                        if (!selfClosing)
                        {
                            var close = html.IndexOf("</" + name, i, StringComparison.OrdinalIgnoreCase);
                            if (close < 0)
                            {
                                i = html.Length;
                            }
                            else
                            {
                                var end = html.IndexOf('>', close);
                                i = end < 0 ? html.Length : end + 1;
                            }
                        }
                        continue;
                    }
                    if (VoidElements.Contains(name))
                    {
                        if (name is "br" or "hr" && !skip)
                        {
                            if (pendingBreak) CommitParagraph();
                            pendingBreak = true;
                        }
                        continue;
                    }
                    OpenElement(name, selfClosing, styleAttr, alignAttr, colorAttr, faceAttr, sizeAttr, startAttr);
                    continue;
                }
            }

            if (ch == '&')
            {
                var decoded = TryDecodeEntity(html, i, out var consumed);
                if (decoded is not null)
                {
                    AppendVisible(decoded);
                    i += consumed;
                    continue;
                }
            }

            i++;
            if (skip) continue;
            if (IsHtmlWhitespace(ch))
            {
                pendingSpace = true;
                continue;
            }
            AppendVisible(ch.ToString());
        }

        FlushRun();
        if (text.Length > 0 || paragraphFormat != new ParagraphFormatState())
        {
            paragraphs[paragraphStart] = paragraphFormat;
        }
        return new HtmlFragment(text.ToString(), runs, paragraphs);
    }

    public static string Export(RichEditTextDocument document)
    {
        var text = document.Text;
        var builder = new StringBuilder();
        var defaults = new CharacterFormatState();
        var openList = MarkerType.None;
        var paragraphStart = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', paragraphStart);
            var paragraphEnd = newline < 0 ? text.Length : newline;
            var format = document.GetParagraphFormat(paragraphStart);
            var listType = format.ListType is MarkerType.Bullet or MarkerType.Arabic ? format.ListType : MarkerType.None;
            if (listType != openList)
            {
                AppendListClose(builder, openList);
                if (listType == MarkerType.Bullet)
                {
                    builder.Append("<ul>");
                }
                else if (listType == MarkerType.Arabic)
                {
                    builder.Append(format.ListStart > 1
                        ? $"<ol start=\"{format.ListStart.ToString(CultureInfo.InvariantCulture)}\">"
                        : "<ol>");
                }
                openList = listType;
            }
            var headingTag = format.HeadingLevel switch
            {
                RichTextHeadingLevel.Heading1 => "h1",
                RichTextHeadingLevel.Heading2 => "h2",
                _ => null
            };
            var tag = listType != MarkerType.None ? "li" : headingTag ?? "p";
            builder.Append('<').Append(tag);
            if (format.Alignment != ParagraphAlignment.Left)
            {
                builder.Append(" style=\"text-align: ").Append(format.Alignment switch
                {
                    ParagraphAlignment.Center => "center",
                    ParagraphAlignment.Right => "right",
                    _ => "justify"
                }).Append('"');
            }
            builder.Append('>');
            var nestedHeading = listType != MarkerType.None ? headingTag : null;
            if (nestedHeading is not null) builder.Append('<').Append(nestedHeading).Append('>');
            AppendRuns(builder, document, paragraphStart, paragraphEnd, defaults);
            if (nestedHeading is not null) builder.Append("</").Append(nestedHeading).Append('>');
            builder.Append("</").Append(tag).Append('>');
            if (newline < 0) break;
            paragraphStart = newline + 1;
        }
        AppendListClose(builder, openList);
        return builder.ToString();
    }

    private static void AppendListClose(StringBuilder builder, MarkerType openList)
    {
        if (openList == MarkerType.Bullet) builder.Append("</ul>");
        else if (openList == MarkerType.Arabic) builder.Append("</ol>");
    }

    private static void AppendRuns(StringBuilder builder, RichEditTextDocument document, int start, int end, CharacterFormatState defaults)
    {
        var position = start;
        while (position < end)
        {
            var format = document.GetCharacterFormat(position);
            var segmentEnd = position + 1;
            while (segmentEnd < end && document.GetCharacterFormat(segmentEnd) == format) segmentEnd++;
            AppendSegment(builder, document.Text.AsSpan(position, segmentEnd - position), format, defaults);
            position = segmentEnd;
        }
    }

    private static void AppendSegment(StringBuilder builder, ReadOnlySpan<char> value, CharacterFormatState format, CharacterFormatState defaults)
    {
        var styles = new List<string>();
        if (format.FontFamily != defaults.FontFamily) styles.Add("font-family: " + format.FontFamily);
        if (format.Size != defaults.Size) styles.Add("font-size: " + format.Size.ToString("0.##", CultureInfo.InvariantCulture) + "pt");
        if (format.ForegroundColor.A != 0) styles.Add("color: " + ToHexColor(format.ForegroundColor));
        if (format.BackgroundColor.A != 0) styles.Add("background-color: " + ToHexColor(format.BackgroundColor));

        if (styles.Count > 0)
        {
            builder.Append("<span style=\"");
            AppendAttributeEscaped(builder, string.Join("; ", styles));
            builder.Append("\">");
        }
        if (format.Bold) builder.Append("<strong>");
        if (format.Italic) builder.Append("<em>");
        if (format.Underline) builder.Append("<u>");
        if (format.Strikethrough) builder.Append("<s>");
        if (format.Subscript) builder.Append("<sub>");
        if (format.Superscript) builder.Append("<sup>");

        foreach (var ch in value) AppendTextEscaped(builder, ch);

        if (format.Superscript) builder.Append("</sup>");
        if (format.Subscript) builder.Append("</sub>");
        if (format.Strikethrough) builder.Append("</s>");
        if (format.Underline) builder.Append("</u>");
        if (format.Italic) builder.Append("</em>");
        if (format.Bold) builder.Append("</strong>");
        if (styles.Count > 0) builder.Append("</span>");
    }

    private static string ToHexColor(Color color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

    private static void AppendTextEscaped(StringBuilder builder, char ch)
    {
        switch (ch)
        {
            case '&': builder.Append("&amp;"); break;
            case '<': builder.Append("&lt;"); break;
            case '>': builder.Append("&gt;"); break;
            case '\u00a0': builder.Append("&nbsp;"); break;
            case '\t': builder.Append("&#9;"); break;
            default: builder.Append(ch); break;
        }
    }

    private static void AppendAttributeEscaped(StringBuilder builder, string value)
    {
        foreach (var ch in value)
        {
            if (ch == '"') builder.Append("&quot;");
            else AppendTextEscaped(builder, ch);
        }
    }

    private static bool IsHtmlWhitespace(char ch) => ch is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    private static ParagraphAlignment? ParseAlignment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "left" => ParagraphAlignment.Left,
            "center" or "middle" => ParagraphAlignment.Center,
            "right" => ParagraphAlignment.Right,
            "justify" => ParagraphAlignment.Justify,
            _ => null
        };
    }

    private static ParagraphAlignment? ParseAlignmentFromStyle(string? style)
    {
        if (string.IsNullOrEmpty(style)) return null;
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0) continue;
            if (declaration[..colon].Trim().Equals("text-align", StringComparison.OrdinalIgnoreCase))
            {
                return ParseAlignment(declaration[(colon + 1)..]);
            }
        }
        return null;
    }

    private static string? TryDecodeEntity(string html, int index, out int consumed)
    {
        consumed = 0;
        var searchLength = Math.Min(33, html.Length - index - 1);
        if (searchLength <= 0) return null;
        var semicolon = html.IndexOf(';', index + 1, searchLength);
        if (semicolon < 0) return null;
        var candidate = html[index..(semicolon + 1)];
        var decoded = WebUtility.HtmlDecode(candidate);
        if (decoded == candidate) return null;
        consumed = candidate.Length;
        return decoded;
    }

    private static CharacterFormatState ApplyStyles(CharacterFormatState format, string style)
    {
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0) continue;
            var key = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim();
            switch (key)
            {
                case "font-weight":
                    if (value.Equals("bold", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("bolder", StringComparison.OrdinalIgnoreCase)
                        || (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) && weight >= 600))
                    {
                        format = format with { Bold = true };
                    }
                    else if (value.Equals("normal", StringComparison.OrdinalIgnoreCase) || value.Equals("400", StringComparison.Ordinal))
                    {
                        format = format with { Bold = false };
                    }
                    break;
                case "font-style":
                    if (value.StartsWith("italic", StringComparison.OrdinalIgnoreCase) || value.StartsWith("oblique", StringComparison.OrdinalIgnoreCase))
                    {
                        format = format with { Italic = true };
                    }
                    else if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    {
                        format = format with { Italic = false };
                    }
                    break;
                case "text-decoration" or "text-decoration-line":
                    var decorations = value.ToLowerInvariant();
                    if (decorations.Contains("underline", StringComparison.Ordinal)) format = format with { Underline = true };
                    if (decorations.Contains("line-through", StringComparison.Ordinal)) format = format with { Strikethrough = true };
                    if (decorations == "none") format = format with { Underline = false, Strikethrough = false };
                    break;
                case "vertical-align":
                    if (value.Equals("sub", StringComparison.OrdinalIgnoreCase)) format = format with { Subscript = true, Superscript = false };
                    else if (value.Equals("super", StringComparison.OrdinalIgnoreCase)) format = format with { Superscript = true, Subscript = false };
                    break;
                case "color":
                    if (TryParseColor(value, out var foreground)) format = format with { ForegroundColor = foreground };
                    break;
                case "background-color" or "background":
                    if (TryParseColor(value, out var background)) format = format with { BackgroundColor = background };
                    break;
                case "font-size":
                    if (TryParseFontSize(value, out var size)) format = format with { Size = size };
                    break;
                case "font-family":
                    var family = FirstFontFamily(value);
                    if (family.Length > 0) format = format with { FontFamily = family };
                    break;
            }
        }
        return format;
    }

    private static string FirstFontFamily(string value)
    {
        var comma = value.IndexOf(',');
        var family = (comma >= 0 ? value[..comma] : value).Trim().Trim('"', '\'').Trim();
        return family;
    }

    private static bool TryParseFontSize(string value, out float size)
    {
        size = 0;
        value = value.Trim().ToLowerInvariant();
        float scale;
        if (value.EndsWith("pt", StringComparison.Ordinal)) { value = value[..^2]; scale = 1f; }
        else if (value.EndsWith("px", StringComparison.Ordinal)) { value = value[..^2]; scale = 0.75f; }
        else if (value.Length > 0 && char.IsAsciiDigit(value[^1])) { scale = 0.75f; }
        else return false;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0) return false;
        size = Math.Clamp(parsed * scale, 1, 300);
        return true;
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3)
            {
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            }
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                return true;
            }
            return false;
        }
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(');
            var close = value.IndexOf(')');
            if (open < 0 || close <= open) return false;
            var parts = value[(open + 1)..close].Split(',');
            if (parts.Length is not (3 or 4)) return false;
            if (!byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                || !byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
                || !byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }
            if (parts.Length == 4
                && float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha)
                && alpha <= 0)
            {
                return false;
            }
            color = Color.FromArgb(255, r, g, b);
            return true;
        }
        if (NamedColors.TryGetValue(value, out var named))
        {
            color = named;
            return true;
        }
        return false;
    }
}
