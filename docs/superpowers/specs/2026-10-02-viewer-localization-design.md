# Vikings_ActivityViewer — Localization (Português / English)

Date: 2026-10-02
Approved by the user ("faça") after the analysis in chat.

## Decisions

| Topic | Decision |
|---|---|
| Mechanism | `Lang` static class in Core with two C# dictionaries (`Texts.Portuguese`, `Texts.English`), `Lang.T(key)` and `Lang.F(key, args)` |
| Switching | Language selector in the top bar; the choice is saved and the app restarts to apply it |
| First run | Windows UI language: `pt` → Português, anything else → English. The installer language choice is written as the first-run default when no settings exist |
| Settings | `%AppData%\Vikings_ActivityViewer\settings.json` (`{ "Language": "Portuguese" }`) |
| Column identity | Each result column has a fixed English `Key` (used by grid binding, "Copiar ID", tests) and a translated `Header` |
| Values computed by Core | Translated when the result is built ("Chão"/"Ground", "Sucesso"/"Success", damage types) |
| Not translated | Event names and item prefabs (they come from the game/mod) |
| Dates | Português `dd/MM/yyyy HH:mm:ss`; English ISO `yyyy-MM-dd HH:mm:ss`. Filters accept `dd/MM/yyyy [HH:mm]` / `yyyy-MM-dd [HH:mm]` |
| Numbers | Português decimal comma (input accepts `,` or `.`); English decimal point only (`,` is rejected to avoid `1,000` being read as `1.0`) |
| CSV | Português `;` separator and decimal comma; English `,` separator and decimal point |
| Installer | Language selection dialog (English / Português) |
| Safety | A test asserts both dictionaries have the same keys, no empty values and the same `{n}` placeholders |
