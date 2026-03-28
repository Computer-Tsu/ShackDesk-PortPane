# Translating PortPane

Translations are community-contributed and always welcome.
**Any language is accepted** — PortPane is not limited to a predefined list.
Use the standard IETF language tag for your file name (see below).

Before starting a new translation, **check open pull requests** on GitHub to
avoid duplicating work that someone else may already have in progress.

---

## Current Languages

| Language | File | Status |
| --- | --- | --- |
| English (default) | `Strings.resx` | Complete |
| German (Deutsch) | `Strings.de.resx` | Partial — help wanted |
| Spanish (Español) | `Strings.es.resx` | Placeholder — needs translation |
| French (Français) | `Strings.fr.resx` | Placeholder — needs translation |
| Japanese (日本語) | `Strings.ja.resx` | Placeholder — needs translation |

---

## How to Add or Update a Translation

1. **Check for existing work first.** Open the
   [pull requests list](https://github.com/Computer-Tsu/shackdesk-portpane/pulls)
   and search for your language before starting.

2. **Locate the English source file:**
   `src/PortPane/Resources/Strings.resx`

3. **Copy it and name the copy** using the IETF language tag:
   - If a placeholder file already exists for your language, edit it instead.
   - If no file exists yet, copy `Strings.resx` and name it
     `Strings.{ietf-tag}.resx` (e.g. `Strings.pt-BR.resx` for Brazilian Portuguese).

4. **Fill in the header comment** at the top of the file:
   - Language name and IETF code
   - Your name as translator
   - Date

5. **Translate only the `<value>` content** inside each `<data>` element.
   **Do not change `<data name="...">` key names** — the app uses them to find strings.

6. **Preserve `{0}`, `{1}` placeholders** exactly — they are replaced with values at runtime.

7. **Validate your XML** at [xmlvalidation.com](https://www.xmlvalidation.com) before submitting.

8. **Agree to the CLA** (see [CLA.md](CLA.md)) — required before your PR can be merged.

9. Submit your file via a pull request or open a
   [Translation issue](.github/ISSUE_TEMPLATE/translation.md).

---

## IETF Language Tag Reference

Use any standard IETF tag — this list is a reference, not a restriction.
Any language not listed here is equally welcome.

| Code | Language |
| --- | --- |
| `de` | German (Deutsch) |
| `es` | Spanish (Español) |
| `fr` | French (Français) |
| `ja` | Japanese (日本語) |
| `ko` | Korean (한국어) |
| `zh-CN` | Chinese Simplified (简体中文) |
| `zh-TW` | Chinese Traditional (繁體中文) |
| `pt-BR` | Portuguese, Brazil |
| `pt` | Portuguese, Portugal |
| `it` | Italian (Italiano) |
| `nl` | Dutch (Nederlands) |
| `pl`    | Polish (Polski)                 |
| `sv`    | Swedish (Svenska)               |
| `fi`    | Finnish (Suomi)                 |
| `no`    | Norwegian (Norsk)               |
| `da`    | Danish (Dansk)                  |
| `ru`    | Russian (Русский)               |
| `ar`    | Arabic (العربية)                |
| `he`    | Hebrew (עברית)                  |
| `tr`    | Turkish (Türkçe)                |
| `cs`    | Czech (Čeština)                 |
| `hu`    | Hungarian (Magyar)              |
| `ro`    | Romanian (Română)               |
| `uk`    | Ukrainian (Українська)          |
| `el`    | Greek (Ελληνικά)                |
| `id`    | Indonesian (Bahasa Indonesia)   |
| `th`    | Thai (ภาษาไทย)                  |
| `vi`    | Vietnamese (Tiếng Việt)         |

Not listed? Your language is still welcome. Use the correct IETF tag and follow
the same contribution steps above.

---

## Terminology Guide

| English Term    | Notes                                                        |
|-----------------|--------------------------------------------------------------|
| COM port        | Windows serial port. Keep "COM" capitalized in all languages.|
| Audio Playback  | Output — speakers/headphones side                            |
| Audio Capture   | Input — microphone side                                      |
| Hotplug         | Device connect/disconnect detection                          |
| PortPane        | Product name — do not translate                              |
| ShackDesk       | Suite name — do not translate                                |

---

## Questions?

Ask in [GitHub Discussions](https://github.com/Computer-Tsu/shackdesk-portpane/discussions).
