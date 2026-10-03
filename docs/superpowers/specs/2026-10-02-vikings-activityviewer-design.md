# Vikings_ActivityViewer — Design

Date: 2026-10-02
Author: radamanto (werle.rodrigo@gmail.com)

## Purpose

Windows desktop tool for server admins to investigate player activity recorded by the Vikings_ActivityLog mod.
It replaces the Python `log_analyzer.py` (3.0 - log de dano), which parsed `.log` text files, with a faster tool
that downloads the mod's SQLite database from the game server over FTP, FTPS or SFTP and queries it locally.

Success means: an admin picks a saved server profile, presses one button, gets a consistent local copy of the
world database, and answers "who did what, where and when" through filtered tables that stay responsive with
millions of events.

## Decisions

| Topic | Decision |
|---|---|
| Name | `Vikings_ActivityViewer` |
| Location | `D:\UNITY VALHEIM\ActivityViewer\` |
| Platform | Windows x64 only. C#, .NET 10, WPF |
| Distribution | One self-contained single-file `.exe` (no .NET install needed). `e_sqlite3.dll` is an embedded resource, extracted on first use to `%LocalAppData%\Vikings_ActivityViewer\native\<sha256>\e_sqlite3.dll` and loaded from there by full path |
| SQLite access | The same native loader and wrapper used by the mod (`NativeLoader`, `SqliteNative`, `SqliteDatabase`), vendored as source into the Core project. Databases are opened read-only |
| Transfer | FTP and FTPS through FluentFTP; SFTP through SSH.NET. Both MIT licensed |
| Users | Each admin installs the tool on their own PC and creates their own profiles. Nothing is shared between PCs |
| Credentials | Password encrypted with Windows DPAPI (CurrentUser scope). Never stored in plain text |
| UI language | Portuguese (Brazil) |
| Times | Stored as UTC Unix ms in the database; shown in the local time zone of the PC |
| Refresh | Manual only ("Atualizar"). No automatic polling |
| Out of scope | Map view, charts, writing to the database, editing server files |
| Delivery phases | Phase 1: profiles, transfer, cache, global filters and tabs 1–5. Phase 2: tabs 6–11 |

## Database contract (from Vikings_ActivityLog schema v1)

- `schema_info(version)`: the viewer supports version 1. A higher version shows "Banco de uma versão mais nova do mod. Atualize o Viewer." and is not opened.
- `players(platform_id, player_id, last_name, first_seen_utc, last_seen_utc)`
- `events(id, time_utc, platform_id, player_name, player_id, event, x, y, z, target, amount, details)`
- `event_items(event_id, source, prefab, count, quality, crafter_id, crafter_name, custom_data)`
- `event_damage(event_id, damage, blunt, slash, pierce, fire, frost, lightning, poison, spirit, chop, pickaxe, total, health_after)`

Event names and the meaning of their columns, as written by the mod:

| event | target | amount | details | items |
|---|---|---|---|---|
| Spawned | `name(playerId)` | | | |
| Inventory | | | | inventory snapshot (every 10 min) |
| Equip, Unequip, Pickup, Craft, Repair item, TrinketActivated | | | | the item |
| Consume | inventory name | | | the item (count 1) |
| Drop | inventory name | dropped count | | the item |
| Move, MoveAll, StackAll | | moved count | `from:<inv> to:<inv>` | moved items |
| Grave | | | | `source` = `grave` or `player` |
| Dodge | | | | |
| Teleport | destination `(x y z)` | | `distant:True/False` | |
| Damage | target prefab or `name(playerId)` | | creatures: `lvl:N tamed:B` | damage row |
| Damaged | attacker label or `none` | | | damage row with `health_after` |
| Dead | attacker label or `none` | | | |
| Place, Remove, Repair building | piece prefab | | | |
| Interact | object prefab | | `result:True/False` plus optional ` info:...` | |
| Use | object prefab | | | the used item |
| Text | object prefab | | the text written | |
| Ping | | ping in ms | | |
| Connected | | | | (platform id only, no name, position 0,0,0) |
| Disconnected | | | | (name and last position) |
| Command, Command remote | | | the full command line | |

## Architecture

Solution `Vikings_ActivityViewer.sln` with three projects:

- `ActivityViewer.Core` (class library, no UI types): profiles, transfer, cache, SQLite access, queries, analyses.
- `ActivityViewer` (WPF app): windows, view models, the grid and filter controls.
- `ActivityViewer.Tests` (console test runner in the same style as the mod tests: `Check(bool, label)`, prints "N checks passed.").

### Components

1. **Profiles** — `ServerProfile { Name, Protocol (Ftp|Ftps|Sftp), Host, Port, User, EncryptedPassword, RemoteFolder, TrustedCertificateThumbprint, TrustedHostKeyFingerprint }`.
   Stored as JSON in `%AppData%\Vikings_ActivityViewer\profiles.json`. "Testar conexão" connects and lists the `.db` files in `RemoteFolder`.
2. **Transfer** — `IRemoteFolder { ListDatabases(); Stat(name) -> (size, modifiedUtc)?; Download(name, localPath, progress, cancel) }` with `FtpRemoteFolder` (FluentFTP, FTP or explicit FTPS) and `SftpRemoteFolder` (SSH.NET).
3. **Cache** — last good copy per profile and world in `%LocalAppData%\Vikings_ActivityViewer\cache\<profile>\<world>\` (`<world>.db` and `<world>.db-wal`, plus `copy.json` with download time and sizes). The tool can reopen the cached copy without connecting, or open any local `.db` file.
4. **Database access** — read-only connection, one query class per tab, parameterized SQL only, results streamed in pages. Every query runs off the UI thread and accepts a cancellation token.
5. **UI** — dark theme. Top bar: profile selector, world selector, "Atualizar", "Abrir arquivo local", copy date/time and size. Global filter bar: player and period. Then the tab control.
6. **Analyses** — action frequency, speed, sessions, death reconstruction and radius search live in Core as pure functions over event lists, independent of the UI.

## Safe download (server keeps writing)

1. Stat `<world>.db` and `<world>.db-wal` (size and modified time).
2. Download `<world>.db`, then `<world>.db-wal`, into a temporary folder. `-shm` is never downloaded.
3. Stat `<world>.db` again. If its size or modified time changed, a checkpoint happened during the download: discard and retry.
4. Open the temporary copy read-only and run `PRAGMA quick_check`. If it is not `ok`, retry.
5. Up to 3 attempts. On success, the temporary copy replaces the cached copy atomically. On failure, the previous cached copy stays in use and the status bar shows "Não foi possível obter uma cópia consistente após 3 tentativas. Usando a cópia de <data>."

A partially written last WAL frame is ignored by SQLite through its checksum; at most the last moments of activity are missing.
The size of the database is shown before the download; the download shows progress and can be cancelled.

## Global filters

- **Jogador**: list from `players` (`last_name (platform_id)`), plus "Todos os jogadores".
- **Período**: "De" and "Até" date-time fields, no shortcuts. An empty field means no limit on that side.

Applied to every tab. Each tab adds its own filters and a "Buscar" button.

## Tabs

Common to every result grid: sort by clicking a column header, "Copiar ID" (platform id of the selected row),
"Exportar" (CSV or TXT, always the full result), status line with row count and query time. The grid shows at most
200,000 rows; beyond that it shows the first 200,000 and the message "Resultado muito grande, refine o filtro".
Selecting a row that has a position offers "Investigar local", which fills tab 7 with that X/Z.

### Phase 1

1. **Dano** — events `Damage` and `Damaged` joined with `event_damage`. Filters: minimum total damage, target (contains), attacker (contains, for `Damaged`). Columns: time, player, event, target/attacker, total, per-type damage (non-zero types only, in one column), health after, position.
2. **Itens** — `event_items` joined with events `Pickup, Drop, Move, MoveAll, StackAll, Craft, Equip, Unequip, Consume`. Filters: item prefab (contains, with suggestions from distinct prefabs), minimum quantity, event types. Columns: time, player, event, item, quantity, quality, crafter, origin, destination (parsed from `from:/to:` or the inventory name), position.
3. **Ações/seg** — per player, sliding 1-second window over events `Pickup, Place, Remove, Interact, Drop, Move`. Filter: maximum actions per second (default 7). A window that exceeds the limit is reported once (overlapping windows merge). Columns: time, player, count, event types in the window, objects, position.
4. **Velocidade** — per player, consecutive events with a position (position not 0,0,0) ordered by time; speed = horizontal distance (X/Z) / elapsed seconds. Pairs whose interval contains a `Teleport`, `Spawned` or `Dead` event, or whose elapsed time is 0, are skipped. Filter: maximum speed in units/second (default 150). Columns: time, player, speed, distance, elapsed, from position, to position.
5. **Interações** — events `Interact`, `Use`, `Text`. Filters: object (contains, with suggestions), result (all/true/false). Columns: time, player, event, object, result, info or text, used item, position.

### Phase 2

6. **Linha do tempo** — every event of the selected player (player filter required) in time order, with a multi-select event type filter. Columns: time, event, target, amount, details, items summary, position.
7. **Local** — X, Z and radius (default 30). Uses the `(x, z)` index with a bounding box, then exact distance. Columns: time, player, event, target, distance from the point, details.
8. **Mortes e túmulos** — each `Dead` event with killer, the `Damaged` events of that player in the 10 seconds before death, and the next `Grave` event of that player within 60 seconds with its items split into "Túmulo" and "Inventário".
9. **Sessões e ping** — sessions built per platform id from `Connected` → next `Disconnected`. A `Connected` without a `Disconnected` before the next `Connected` or the end of data is shown as "sem desconexão registrada". A `Disconnected` without a `Connected` is ignored. Summary per player: number of sessions, total online time, average and maximum ping (from `Ping.amount`) in the period.
10. **Rastrear item** — item prefab plus optional crafter and quality. Lists every `event_items` row that matches, in time order, with event, player, quantity, origin and destination. Valheim items have no unique id, so prefab + crafter + quality is the tracking key.
11. **Comandos** — events `Command` and `Command remote`. Filter: text contains. Columns: time, player, event, command, position.

## Error handling

| Situation | Behavior |
|---|---|
| Connection fails (credentials, host, timeout) | Show the server message; keep the current cached copy open |
| No `.db` in the remote folder | "Nenhum mundo encontrado em <pasta>" |
| Inconsistent copy | Retry as described; after 3 failures keep the previous copy and warn |
| Schema version above 1 | Refuse to open with the update message |
| FTPS certificate not trusted by Windows | Ask, showing the certificate thumbprint; remember the answer in the profile; ask again if the thumbprint changes |
| SFTP host key | First connection asks with the fingerprint; a changed key is refused with a warning |
| DPAPI cannot decrypt the password | Ask for the password again |
| Unexpected exception | Write to `%LocalAppData%\Vikings_ActivityViewer\logs\viewer-<date>.log`, show a simple dialog, keep running |

## Testing

- Query tests per tab against a test database generated with the mod's own `ActivityStore` (vendored), with known rows.
- Analysis tests: action frequency at the exact limit and one above; speed skipping teleport, spawn and death; sessions with missing ends and missing starts; radius search on the boundary; death reconstruction windows.
- Safe download test with a fake `IRemoteFolder` that changes the database between the two stats: the download retries and the previous cache survives three failures.
- Profile tests: save and load round trip; the JSON never contains the plain password.
- Manual: connect to the test server and the real server, check every tab against a database produced by the mod.
