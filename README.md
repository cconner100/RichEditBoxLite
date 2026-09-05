# RichEditBoxLite

[![NuGet](https://img.shields.io/nuget/v/CConner100.RichEditBoxLite.svg)](https://www.nuget.org/packages/CConner100.RichEditBoxLite/)

`RichEditBoxLite` is an experimental WinUI-shaped rich-text editor for Uno
Platform's Skia renderer. It targets Desktop, WebAssembly, Android, and iOS and
does not target WinAppSDK or Uno native rendering.

Install the current package:

```bash
dotnet add package CConner100.RichEditBoxLite --version 0.1.2
```

```xml
xmlns:rte="using:CConner100.RichEditBoxLite"

<rte:RichEditBoxLite
    Header="Notes"
    TextChanged="Editor_TextChanged" />
```

The public CLR and package namespace is `CConner100.RichEditBoxLite`.

## What's new in 0.1.2

- Bounded, sanitized HTML import (`Document.SetHtml`) and deterministic
  canonical HTML export (`Document.GetHtml`), with HTML clipboard paste routed
  through the same package-owned codec
  ([#24](https://github.com/cconner100/RichEditBoxLite/issues/24)). Supported
  profile, security bounds, and lossy behavior are documented in
  [docs/html.md](docs/html.md).
- Updated to Uno.Sdk 6.7.22.

## Fixes in 0.1.1

- Pointer clicks now place the caret using the rendered rich-text glyph layout,
  including wrapped lines and mixed font sizes ([#17](https://github.com/cconner100/RichEditBoxLite/issues/17)).
- Wrapped text now follows the live editor viewport instead of a fixed fallback
  width, while `NoWrap` retains horizontal scrolling ([#18](https://github.com/cconner100/RichEditBoxLite/issues/18)).
- Fixed macOS focus rendering so the hidden input bridge does not paint over the
  Skia surface ([#8](https://github.com/cconner100/RichEditBoxLite/issues/8)).
- Added H1/H2 headings, rendered bullet and Arabic-numbered lists, paragraph
  formatting persistence, and undoable clear-formatting operations
  ([#9](https://github.com/cconner100/RichEditBoxLite/issues/9)).

## What is implemented

- A Skia `SKCanvasElement` renderer with formatted runs, selection, caret,
  wrapping, spellcheck marks, and range geometry.
- A transparent Uno `TextBox` input bridge for platform keyboard, dead-key,
  IME, selection, clipboard, and virtual-keyboard integration.
- UTF-16 public positions with document/range/selection operations, find,
  case changes, undo/redo, grouped edits, character formatting, paragraph
  formatting, H1/H2 headings, rendered bullet/Arabic lists, clear formatting,
  and inline-object projection (`U+FFFC`).
- Bounded RTF import and canonical export for text, Unicode, font size,
  bold/italic/underline/strike, colors, highlight, subscript, superscript,
  headings, supported lists, paragraphs, and tabs.
- Bounded, sanitized HTML import (`Document.SetHtml`) and deterministic
  canonical HTML export (`Document.GetHtml`) for the supported formatting
  profile, with HTML clipboard paste routed through the same codec. See
  [docs/html.md](docs/html.md).
- Built-in lightweight English (`en-US`) and Spanish (`es-ES`) proofing with
  suggestions, ignored words, and custom words.
- A Fluent-styled control template with WinUI-compatible part/state intent,
  dependency properties, custom event arguments, and a value automation peer.
- A seven-section Test UI with stable automation IDs plus document, malformed
  input, RTF round-trip, API-inventory, and spellcheck tests.

## Compatibility status

This is a usable foundation, not a claim of complete WinUI RichEdit parity.
The public surface is deliberately WinUI-shaped, but a few types must come from
`CConner100.RichEditBoxLite` because Uno's WinUI event-argument and text-object
constructors are internal.

| Area | v0.1.2 status |
|---|---|
| Plain text, caret, selection, keyboard input | Implemented through Uno input bridge |
| Character formats | Implemented for core format run properties |
| Paragraph headings and lists | H1/H2 plus bullet and Arabic list rendering implemented; other marker styles remain partial |
| RTF core profile | Implemented and bounded; headings and supported lists round-trip |
| Clear formatting | Character, paragraph, or combined reset operations implemented |
| Undo/redo and grouped edits | Implemented |
| Find, movement, case conversion | Implemented; movement is character-based except unit expansion |
| Spellcheck en-US/es-ES | Implemented with bundled compact dictionaries |
| Hyperlinks | Link metadata surface exists; activation UI is not implemented |
| Images | Inline placeholder projection exists; binary PNG/JPEG persistence/painting is not implemented |
| Imported tables | Normalized editable text; structural table API is intentionally absent |
| HTML import/export | Bounded `SetHtml`/`GetHtml` codec for the supported profile; unsupported CSS, tables, images, and links are documented lossy |
| HTML clipboard import | Implemented through the shared HTML codec where the host exposes the clipboard HTML format |
| Touch selection handles | Delegated to the Uno TextBox bridge; host support varies |
| IME/dead keys/virtual keyboard | Delegated to Uno; requires target-device validation |
| Accessibility | Value automation pattern implemented; full text patterns depend on Uno runtime support |
| RTL | Properties retained; renderer is left-to-right |
| MathML, handwriting, OLE, advanced Word destinations | Unsupported |
| Candidate-window placement | Property retained; host placement may ignore it |
| Pagination-only paragraph behavior | Metadata only; continuous layout |

See [docs/compatibility.md](docs/compatibility.md) for the detailed native-Skia
feasibility assessment and [PublicAPI.Shipped.txt](PublicAPI.Shipped.txt) for the
approved compatibility inventory.

## Build and run

Requires .NET 10 and Uno Platform workloads.

```bash
dotnet build RichEditBoxLite.TestApp/RichEditBoxLite.TestApp.csproj -f net10.0-desktop
dotnet run --project RichEditBoxLite.TestApp/RichEditBoxLite.TestApp.csproj -f net10.0-desktop
dotnet test RichEditBoxLite.TestApp.Tests/RichEditBoxLite.TestApp.Tests.csproj
dotnet pack src/CConner100.RichEditBoxLite/CConner100.RichEditBoxLite.csproj -c Release
```

Target frameworks:

- `net10.0-desktop`
- `net10.0-browserwasm`
- `net10.0-android`
- `net10.0-ios`

The Test UI navigation covers Playground, Formatting and Document API, RTF and
Rich Content, Input/Clipboard/Spellcheck, Properties and Visual States, Event
Monitor, and Accessibility/Stress.

## Security profile

RTF and HTML input share the same caps: 16 MiB of input and 256 levels of
nesting. Remote resources are never loaded. Unsupported binary/object/math
destinations and unsafe HTML (`script`, `style`, `iframe`, `object`, embedded
media) are discarded during normalized import. Malformed unbalanced RTF is
rejected; malformed HTML is recovered browser-style within the same bounds.

## License

MIT. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
