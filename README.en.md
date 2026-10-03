# Vikings_ActivityViewer

Windows program that reads the SQLite database written by the **Vikings_ActivityLog** mod and lets admins investigate player activity.

*Versão em português: [README.md](README.md)*

## Installation

Run `Setup_Vikings_ActivityViewer_<version>.exe`. No .NET install and no administrator rights are needed.

## Language

The program is available in **English** and **Português**. On first start it uses the language chosen in the installer (or the Windows language). To change it, open **Settings** (gear in the top-right corner); the program restarts in the new language.

## First use

1. In the sidebar, click the pencil next to **Server** and create a profile:
   - Protocol: FTP, FTPS or SFTP.
   - Host, port, user and password from your hosting.
   - Remote folder: the server's `-savedir` folder followed by `/Vikings_ActivityLog` (e.g. `/SAVE/Vikings_ActivityLog`).
   - Use **Test connection** to check it; it lists the worlds (`.db`) it finds.
2. Choose the server, click the refresh button next to **World**, choose the world and click **Download**.
3. The program downloads a consistent copy of the database (up to 3 attempts while the server is writing), opens it and fills every tab.

For a server on the same PC, use the folder icon next to **Download** and pick the `.db` straight from the server folder.

The password is saved encrypted by Windows and can only be read by your user on this PC.
Each admin creates their own profiles on their own PC.

## Filters

- **Player** and **period** (From / To, format `yyyy-MM-dd` or `yyyy-MM-dd HH:mm`) apply to every tab.
- "To" with a date only includes the whole day. An empty field means no limit.
- **Analyze** reloads players and suggestions and runs every tab's search again.

## Tabs

- **Damage**: damage dealt (Damage) and taken (Damaged), with damage types and remaining health.
- **Items**: items picked up, dropped, moved, crafted, equipped and consumed, with origin and destination.
- **Actions/sec**: players with more actions per second than the limit (macro/cheat).
- **Speed**: impossible movement between the player's own positions (ignores teleport, death and respawn).
- **Interactions**: interactions with objects, item use and written texts.

In every tab: sort by clicking a header, **Copy ID** (SteamID of the row) and **Export** (CSV for Excel or TXT, always with the full result). In English the CSV uses `,` and a decimal point.

## Program files

- Profiles: `%AppData%\Vikings_ActivityViewer\profiles.json`
- Language: `%AppData%\Vikings_ActivityViewer\settings.json`
- Downloaded copies: `%LocalAppData%\Vikings_ActivityViewer\cache\`
- Error log: `%LocalAppData%\Vikings_ActivityViewer\logs\`

## Settings

Open it with the gear in the top-right corner:

- **Language**: English or Português (the program restarts).
- **Folder for downloaded copies**: choose where downloaded databases are saved. When you change it, existing copies are moved to the new folder.
- **Clear current copy / Clear all**: deletes the copies downloaded to this PC. The databases on the servers are never changed.
