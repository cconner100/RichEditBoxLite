# Bounded HTML import/export

RichEditBoxLite owns a single sanitized HTML codec. `RichEditTextDocument.SetHtml`
imports HTML into the document model, `RichEditTextDocument.GetHtml` exports the
supported model back to canonical HTML, and HTML clipboard paste routes through
the same parser, so downstream consumers never need their own HTML adapter.

```csharp
editor.Document.SetHtml(storedHtml);
string canonicalHtml = editor.Document.GetHtml();
```

## Supported import profile

- Blocks: `p`, `div`, `br`, `h1`, `h2`, `ul`, `ol` (including `start`), `li`,
  `hr` (as a paragraph break), and `center`. `h3`–`h6` map to Heading 2.
- Inline formatting: `strong`/`b`, `em`/`i`, `u`/`ins`, `s`/`strike`/`del`,
  `sub`, `sup`, and `font` (`color`, `face`, `size`).
- Style declarations on any element: `font-weight`, `font-style`,
  `text-decoration(-line)`, `vertical-align: sub|super`, `color`,
  `background-color`, `font-size` (`pt`/`px`), `font-family`, and `text-align`
  (block elements only). Colors accept `#rgb`, `#rrggbb`, `rgb()/rgba()`, and
  the common named colors.
- HTML entities are decoded (named and numeric); numeric-encoded whitespace
  such as `&#9;` is preserved literally.
- Visible text of unknown, non-dangerous elements is preserved.
- Browser-style recovery for malformed input: unclosed tags are auto-closed at
  end of input, stray close tags are ignored, and repeated unclosed `p`/`h#`/`li`
  are implied-closed rather than treated as nesting.

## Sanitization and bounds

- `script`, `style`, `iframe`, `object`, `applet`, `svg`, `math`, `head`,
  `template`, form/media containers, and embedded media (`img`, `embed`, etc.)
  are discarded entirely. Remote resources are never loaded.
- Input is capped at 16 MiB and 256 levels of element nesting (the same
  `CodecLimits` the RTF codec enforces); violations throw
  `InvalidDataException` instead of freezing the UI. Parsing is a bounded
  single pass over the input.

## Canonical export

`GetHtml` is deterministic: one block element per paragraph (`h1`, `h2`, `li`
inside a grouped `ul`/`ol`, otherwise `p`), `text-align` styles for non-left
alignment, inline tags in the fixed order
`span` (font-family, font-size, color, background-color) → `strong` → `em` →
`u` → `s` → `sub`/`sup`, and minimal escaping (`&amp;`, `&lt;`, `&gt;`,
`&nbsp;` for U+00A0, `&#9;` for tabs). Re-importing canonical output reproduces
the same document and the same HTML.

## Documented lossy behavior

- Unsupported CSS declarations, classes, ids, and other attributes are dropped.
- Tables are flattened to text: each row becomes a paragraph and cell text is
  separated by spaces; no structural table model exists.
- Images and other embedded media are removed (no placeholder is inserted).
- Link `href` metadata is dropped and only the visible link text is kept,
  because the document model has no persistent link storage yet.
- Nested lists are flattened to the innermost list type with a left indent per
  extra level; the indent is not exported back as nesting.
- `br` produces a paragraph break (the model has no soft line break), so a
  `br` inside a paragraph exports as two block elements.
- Consecutive regular spaces collapse on import per HTML whitespace rules;
  non-breaking spaces are preserved.
- Headings render with synthesized bold/size; explicit `h#` font styling in the
  source HTML is not merged into character runs.

## Clipboard paste

`RichEditBoxLite` intercepts paste when the host clipboard reports HTML
content, unwraps CF_HTML headers/fragment markers, parses with this codec, and
splices the fragment over the selection as a single undoable edit (honoring
`MaxLength`). If clipboard HTML retrieval fails, the control falls back to
plain-text paste. Hosts that do not expose the HTML clipboard format keep the
existing plain-text paste behavior.
