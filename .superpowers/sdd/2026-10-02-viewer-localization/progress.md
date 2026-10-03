# SDD ledger — plan: /d/UNITY VALHEIM/ActivityViewer/docs/superpowers/specs/2026-10-02-viewer-localization-design.md (inline, design note instead of a full plan)
Setup: Ruling: no separate plan document — user said 'faça' after the in-chat analysis; work is done in 7 steps with TDD and ledgered here — cost if wrong: less written detail for a later reviewer
Step 1-2: complete — Lang/Texts infra, ResultColumn Key+Header, localized computed values, ResultView uses keys (tests 162)
Step 3: complete — TimeFormat.Pattern per language, CSV separator/decimal per language, InputParser per language (EN rejects comma numbers, ISO dates), CachedCopy.Describe via Lang (tests 171)
Step 4: complete — 22 Core messages via Lang (download, db, profile, sqlite, transfer) (tests 174)
Step 5-6: complete — SettingsStore (settings.json, Windows-language default), TExtension markup, all XAML (62 texts) and app C# texts via Lang, language selector + restart; AppLanguage alias (FrameworkElement.Language clash); build 0/0, tests 179
Step 7: complete — installer pt/en with language dialog, writes settings.json on first install; README.en.md; version 1.1.0; published exe smoke alive; installer Setup_Vikings_ActivityViewer_1.1.0.exe
