# Compatibility and native-Skia feasibility

WinUI's `RichEditBox` ultimately uses the native Windows RichEdit text services.
Those services are not portable to Uno's Skia hosts. RichEditBoxLite therefore
keeps the WinUI-shaped control contract while using a package-owned document
model, Skia painting, and an Uno text-input bridge.

## Feasible in shared Skia code

- Glyph painting, formatted runs, caret and selection visuals.
- Incremental line/block layout, hit testing, scrolling, and range rectangles.
- Core RTF parsing/export, bounds checking, unknown destination policy, and
  deterministic round trips.
- Document/range APIs, UTF-16 compatibility positions, grapheme-aware editing,
  undo transactions, find, formatting, H1/H2 headings, bullet/Arabic lists,
  clear formatting, hyperlinks, and inline objects.
- English/Spanish dictionaries, tokenization, suggestions, ignore/custom words.
- Fluent template resources, visual states, context UI, and most Test UI
  automation.

The repository currently implements the foundation of these items, including
rendered and RTF-round-tripped H1/H2 headings and bullet/Arabic lists.
Incremental layout caching, grapheme-aware movement, additional list marker
styles, hyperlink activation, embedded bitmap painting, structural RTF tables,
and sanitized HTML clipboard import remain follow-up work and are not reported
as complete.

## Requires Uno/platform services

- Keyboard and command-key mapping.
- Dead keys, IME composition, candidate windows, and virtual keyboards.
- System clipboard format access and native context menus.
- Touch selection handles.
- Accessibility bridge from Uno's automation peer to each host.

RichEditBoxLite routes text input through an accessibility-hidden Uno `TextBox`.
The exact feature level is consequently bounded by the current Uno Skia host.
Desktop testing cannot prove Android/iOS virtual keyboard, IME candidate window,
or touch-handle behavior.

## Not portable from Windows RichEdit

- OLE embedding and COM text services.
- Word-specific advanced destinations.
- Windows handwriting integration.
- Native MathML/Office math editing.
- Windows pagination behavior.

These APIs should throw a descriptive `NotSupportedException` when exposed.
RTL-valued properties are retained but painting is intentionally left-to-right
for the English/Spanish scope.

## Compatibility rules

- Changing XAML from `RichEditBox` to `rte:RichEditBoxLite` is required.
- `Microsoft.UI.Text` enums are reused where Uno exposes them.
- Package-owned document/range/format and event-argument types are used where
  Uno cannot construct the WinUI counterparts.
- Unsupported dependency properties retain local values.
- `CompatibilityCoverage` and API tests prevent new declared control properties
  or events from being added without a Test UI registration.
