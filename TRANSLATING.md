# Translating PortPane

Translations are community-contributed and always welcome.

## Current Languages

| Language | File | Status |
|----------|------|--------|
| English (default) | `Strings.resx` | Complete |
| German | `Strings.de.resx` | Placeholder — help wanted |

## How to Add or Update a Translation

1. Locate the English source file: `src/PortPane/Resources/Strings.resx`
2. Copy it and name the copy using the [ISO 639-1](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) language code:
   - French: `Strings.fr.resx`
   - Spanish: `Strings.es.resx`
   - Japanese: `Strings.ja.resx`
3. Translate only the `<value>` text inside each `<data>` element. **Do not change `<data name="...">` keys.**
4. For strings containing `{0}`, `{1}` placeholders, preserve them exactly — they are replaced with values at runtime.
5. Submit your file via a pull request or open a [Translation issue](.github/ISSUE_TEMPLATE/translation.md).

## Terminology Guide

| English Term | Notes |
|-------------|-------|
| COM port | Windows serial port. Keep "COM" capitalized in all languages. |
| Audio Playback | Output — speakers/headphones side |
| Audio Capture | Input — microphone side |
| Hotplug | Device connect/disconnect detection |

## Questions?

Ask in [GitHub Discussions](https://github.com/Computer-Tsu/shackdesk-portpane/discussions).
