# RichEditBoxLite

[![NuGet](https://img.shields.io/nuget/v/CConner100.RichEditBoxLite.svg)](https://www.nuget.org/packages/CConner100.RichEditBoxLite/)

`RichEditBoxLite` is an experimental WinUI-shaped rich-text editor for Uno
Platform's Skia renderer. It targets Desktop, WebAssembly, Android, and iOS and
does not target WinAppSDK or Uno native rendering.

Install the current package:

```bash
dotnet add package CConner100.RichEditBoxLite --version 0.1.0
```

```xml
xmlns:rte="using:CConner100.RichEditBoxLite"

<rte:RichEditBoxLite
    Header="Notes"
    TextChanged="Editor_TextChanged" />
```

The public CLR and package namespace is `CConner100.RichEditBoxLite`.

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

| Area | v0.1 status |
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
| HTML clipboard import | Not implemented |
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

RTF input is capped at 16 MiB and 256 nested groups. Remote images are never
loaded. Unsupported binary/object/math destinations are discarded during
normalized import. Malformed unbalanced input is rejected.

## License

MIT. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
