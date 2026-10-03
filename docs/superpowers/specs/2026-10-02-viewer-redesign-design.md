# Vikings_ActivityViewer — Redesign, cache folder and cache cleanup

Date: 2026-10-02
Approved by the user after the inline mockup ("layout bom", dark theme as today, cleanup local only, move copies on folder change).

## Layout

- Header row: app name, open database name, settings button (gear).
- Left sidebar (dark panel):
  - Source: server selector + manage-servers icon, world selector + list-worlds icon, "Baixar/Download" button + open-local-file icon, copy date and size.
  - Filters: player, From/To, "Analisar/Analyze" (the only accent button on the screen), "Limpar filtros/Clear filters" as a link.
- Main area: tabs whose headers show the result count of the last search; per-tab filter row; result grid with Copy ID / Export as icon buttons; status bar with progress and Cancel.
- Theme: the current dark palette.

## Settings window (gear)

- Language (moved from the main screen; same save-and-restart flow).
- Folder for downloaded copies, with "Alterar.../Change...". Default `%LocalAppData%\Vikings_ActivityViewer\cache`. Saved in `settings.json` (`CacheRoot`, empty = default).
- Downloaded copies: count of worlds and total size; "Limpar cópia atual/Clear current copy" and "Limpar todas/Clear all".

## Rules

- Cleanup only deletes downloaded copies on this PC, never anything on the server.
- Cleanup and move only touch the viewer's own structure: `<root>\<profile>\<world>\` folders that contain `current.json` or `copy-*` folders. Any other file or folder in the chosen root is never deleted or moved.
- Changing the folder moves existing copies to the new folder (copy then delete, works across drives). The open database is closed before the move and reopened from the new location afterwards. Moving into a folder inside the current root (or the reverse) is refused.
- Clearing the current copy closes it first; the tabs return to the empty state.
