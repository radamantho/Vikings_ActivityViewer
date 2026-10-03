# Vikings_ActivityViewer Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows desktop tool that downloads a Vikings_ActivityLog SQLite database over FTP, FTPS or SFTP into a consistent local copy and lets admins investigate it through global filters and tabs 1–5 (Dano, Itens, Ações/seg, Velocidade, Interações).

**Architecture:** `ActivityViewer.Core` (no UI) holds the native SQLite wrapper, read-only database access, queries, analyses, profiles, transfer, cache and export. `ActivityViewer` (WPF, code-behind, no MVVM framework) shows a top bar, a global filter bar, five tab user controls and one reusable `ResultView` grid. `ActivityViewer.Tests` is a console test runner that builds test databases with the mod's own writer code.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`, x64), WPF, FluentFTP 55.0.0, SSH.NET 2026.0.0, System.Security.Cryptography.ProtectedData 10.0.12, System.Text.Json source generation, native `e_sqlite3.dll` 3.46.1 (from the mod).

**Spec:** `D:\UNITY VALHEIM\ActivityViewer\docs\superpowers\specs\2026-10-02-vikings-activityviewer-design.md`

## Global Constraints

- Root folder: `D:\UNITY VALHEIM\ActivityViewer\` (all paths below are relative to it). The folder is not a git repository: "Checkpoint" steps mean build + tests pass, no commit.
- Windows x64 only. C#, .NET 10, WPF. `TargetFramework` = `net10.0-windows`, `PlatformTarget` = `x64` in every project.
- No `//` comments anywhere in C# code (the author's convention). XML doc comments are not used either.
- UI language: Portuguese (Brazil). Code identifiers: English.
- Databases are opened read-only. The viewer never writes to an activity database.
- Supported schema version: 1. A higher version shows "Banco de uma versão mais nova do mod. Atualize o Viewer." and is not opened.
- Password encrypted with Windows DPAPI (CurrentUser scope). Never stored in plain text.
- Profiles in `%AppData%\Vikings_ActivityViewer\profiles.json`; cache in `%LocalAppData%\Vikings_ActivityViewer\cache\<profile>\<world>\`; logs in `%LocalAppData%\Vikings_ActivityViewer\logs\viewer-<date>.log`; native SQLite extracted to `%LocalAppData%\Vikings_ActivityViewer\native\<sha256>\e_sqlite3.dll`.
- Times are stored as UTC Unix ms; shown in the local time zone of the PC, format `dd/MM/yyyy HH:mm:ss`.
- Refresh is manual only ("Atualizar").
- Result grids show at most 200,000 rows; beyond that: "Resultado muito grande, refine o filtro". Export always writes the full result.
- Period filter: "De" and "Até" fields, no shortcuts; an empty field means no limit on that side.
- Safe download: stat db and wal, download db then wal, stat db again, `PRAGMA quick_check`, up to 3 attempts; on failure keep the previous copy and show "Não foi possível obter uma cópia consistente após 3 tentativas. Usando a cópia de <data>." `-shm` is never downloaded.
- Single-file self-contained `.exe` named `Vikings_ActivityViewer.exe`.
- Result column names must not contain `/`, `.`, `[` or `]` (WPF auto-generated DataGrid bindings use the column name as a path).

## Spec clarifications decided in this plan

- **Speed analysis uses only events whose position is the player's own position.** The mod records the target's position for Place, Remove, Repair building, Interact, Use, Text, Damage, Pickup and Grave (for example Place uses the piece position). Measuring speed from those would produce false alarms, so tab 4 uses only: Dodge, Damaged, Drop, Consume, Equip, Unequip, Craft, Repair item, Ping, Inventory, TrinketActivated, Command, Command remote, Move, MoveAll, StackAll. Barrier events (a pair across them is never measured): Teleport, Spawned, Dead, Connected, Disconnected. Pairs less than 1 second apart are not measured (position noise); the earlier point is kept as the reference until a point at least 1 second later arrives.
- **Remote modified time** is used only for equality checks between the two stats, so it is kept exactly as the server reports it (`RemoteFileInfo.Modified`), without time-zone conversion.
- **Cache layout:** each successful download goes to a new folder `copy-<yyyyMMddHHmmssfff>` inside the world folder, and `current.json` points to it. Older copy folders are deleted when they are no longer open. This lets the tool keep the previous copy open while a new one downloads, and avoids moving files that SQLite has open.
- **Native loading:** the viewer is Windows-only, so the vendored loader uses `System.Runtime.InteropServices.NativeLibrary` instead of the mod's kernel32/libdl `NativeLoader`. The SQLite function bindings (`SqliteNative`) and the wrapper (`SqliteDatabase`, `SqliteStatement`) are the mod's code with read-only open, `ColumnDouble` and `ColumnIsNull` added.
- **Damage tab target/attacker filters:** "Alvo" filters `Damage` rows by target; "Atacante" filters `Damaged` rows by attacker. With only "Alvo" filled, only `Damage` rows are shown; with only "Atacante", only `Damaged` rows; with both, the union.
- **Item tab origin/destination:** Move/MoveAll/StackAll parse `from:<a> to:<b>`; Drop → (inventory, "Chão"); Pickup → ("Chão", "Inventário"); Consume → (inventory, "Consumido"); other events → empty.

## Review Focus

1. A database copied while the server is writing, with rows only in the `-wal` and no `-shm`: the copy must open read-only and show those rows (pinned in Task 2, `Database_ReadsWalCopyWithoutShm`).
2. The world disappears or the WAL disappears on the server between the stat and the download (clean server shutdown): the download must retry and never promote a half copy (pinned in Task 8, `Download_WalVanishesThenRetries`).
3. Text with quotes, semicolons, line breaks and non-ASCII names (Þór, emoji) in export: CSV must stay one record per row and open in Excel pt-BR (pinned in Task 5, `Export_CsvEscapesAndUsesPtBr`).
4. Filters typed with comma decimals ("2,5") and dates without time ("01/10/2026"), plus invalid input: must parse or show a clear message, never crash (pinned in Task 9, `Input_ParsesBrazilianFormats`).
5. A corrupt or hand-edited `profiles.json`, or a password encrypted on another PC: the tool must start, keep a backup of the bad file, and ask for the password again (pinned in Task 6, `Profiles_CorruptFileIsBackedUp` and `Password_ForeignBlobFails`).

## File Structure

```
Vikings_ActivityViewer.sln
native/e_sqlite3.dll                                   copied from the mod (natives/win-x64)
src/ActivityViewer.Core/ActivityViewer.Core.csproj
src/ActivityViewer.Core/AppPaths.cs                    roaming/local roots, overridable for tests
src/ActivityViewer.Core/ErrorLog.cs                    unexpected error log file
src/ActivityViewer.Core/InputParser.cs                 pt-BR number/date parsing for filters
src/ActivityViewer.Core/Sqlite/SqliteNative.cs         native function bindings
src/ActivityViewer.Core/Sqlite/SqliteDatabase.cs       connection, statement, exception
src/ActivityViewer.Core/Sqlite/SqliteRuntime.cs        extracts the embedded e_sqlite3.dll and loads it
src/ActivityViewer.Core/Data/ResultTable.cs            column + row container returned by every query
src/ActivityViewer.Core/Data/TimeFormat.cs             UTC ms <-> local DateTime
src/ActivityViewer.Core/Data/QueryFilter.cs            global player/period filter
src/ActivityViewer.Core/Data/SqlBuilder.cs             parameterized SQL assembly
src/ActivityViewer.Core/Data/QueryReader.cs            shared read loop with row cap and cancellation
src/ActivityViewer.Core/Data/PointReader.cs            reads event points for the analyzers
src/ActivityViewer.Core/Data/ActivityDatabase.cs       open/validate, players, suggestions, quick_check
src/ActivityViewer.Core/Data/DamageQuery.cs            tab 1
src/ActivityViewer.Core/Data/RouteParser.cs            origin/destination for tab 2
src/ActivityViewer.Core/Data/ItemQuery.cs              tab 2
src/ActivityViewer.Core/Data/InteractionQuery.cs       tab 5
src/ActivityViewer.Core/Data/FrequencyQuery.cs         tab 3 (reads points, runs analyzer)
src/ActivityViewer.Core/Data/SpeedQuery.cs             tab 4 (reads points, runs analyzer)
src/ActivityViewer.Core/Analysis/ActionPoint.cs        one event as seen by the analyzers
src/ActivityViewer.Core/Analysis/FrequencyAnalyzer.cs
src/ActivityViewer.Core/Analysis/SpeedAnalyzer.cs
src/ActivityViewer.Core/Export/ResultExporter.cs       CSV / TXT
src/ActivityViewer.Core/Profiles/ServerProfile.cs
src/ActivityViewer.Core/Profiles/PasswordProtector.cs
src/ActivityViewer.Core/Profiles/ProfileStore.cs
src/ActivityViewer.Core/Profiles/ProfileJsonContext.cs
src/ActivityViewer.Core/Transfer/IRemoteFolder.cs      interface, RemoteFileInfo, ITrustPrompt, RemotePath
src/ActivityViewer.Core/Transfer/ProgressStream.cs
src/ActivityViewer.Core/Transfer/FtpRemoteFolder.cs
src/ActivityViewer.Core/Transfer/SftpRemoteFolder.cs
src/ActivityViewer.Core/Transfer/RemoteFolderFactory.cs
src/ActivityViewer.Core/Cache/CachePaths.cs
src/ActivityViewer.Core/Cache/CachedCopy.cs
src/ActivityViewer.Core/Cache/SafeDownloader.cs
src/ActivityViewer/ActivityViewer.csproj
src/ActivityViewer/viking_icon.ico                     copied from 3.0 - log de dano
src/ActivityViewer/App.xaml / App.xaml.cs
src/ActivityViewer/Themes/Dark.xaml
src/ActivityViewer/MainWindow.xaml / .cs               top bar, filters, tabs, status, IQueryHost
src/ActivityViewer/IQueryHost.cs                       what tabs need from the main window
src/ActivityViewer/WpfTrustPrompt.cs                   ITrustPrompt with MessageBox on the UI thread
src/ActivityViewer/Windows/ProfilesWindow.xaml / .cs
src/ActivityViewer/Windows/PasswordWindow.xaml / .cs
src/ActivityViewer/Controls/ResultView.xaml / .cs      grid + Copiar ID + Exportar + summary
src/ActivityViewer/Tabs/DamageTab.xaml / .cs
src/ActivityViewer/Tabs/ItemsTab.xaml / .cs
src/ActivityViewer/Tabs/FrequencyTab.xaml / .cs
src/ActivityViewer/Tabs/SpeedTab.xaml / .cs
src/ActivityViewer/Tabs/InteractionsTab.xaml / .cs
tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj
tests/ActivityViewer.Tests/Program.cs                  Check, TempDir, Main
tests/ActivityViewer.Tests/Program.Suites.cs           RunAll + partial suite declarations
tests/ActivityViewer.Tests/Program.TestData.cs         builds databases with the mod writer
tests/ActivityViewer.Tests/ModWriter/*.cs              the mod's writer sources, copied unchanged
tests/ActivityViewer.Tests/Program.<Suite>.cs          one file per suite
README.md
```

Test command for every task: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected final line: `N checks passed.` and exit code 0. A failure prints `FAIL <label>` and the final line `M checks FAILED, N passed.`

---

### Task 1: Solution, native SQLite runtime and test runner

**Files:**
- Create: `Vikings_ActivityViewer.sln`, `native/e_sqlite3.dll`
- Create: `src/ActivityViewer.Core/ActivityViewer.Core.csproj`, `src/ActivityViewer.Core/AppPaths.cs`
- Create: `src/ActivityViewer.Core/Sqlite/SqliteNative.cs`, `SqliteDatabase.cs`, `SqliteRuntime.cs`
- Create: `tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`, `Program.cs`, `Program.Suites.cs`, `Program.Sqlite.cs`
- Create: `tests/ActivityViewer.Tests/ModWriter/` (7 files copied from the mod)

**Interfaces:**
- Produces:
  - `public static class AppPaths { static string RoamingRoot; static string LocalDataRoot; static void Override(string roaming, string local); }`
  - `internal static class SqliteRuntime { static void EnsureLoaded(); static string Extract(string root); static string? LoadedFrom; }`
  - `internal sealed class SqliteDatabase : IDisposable { SqliteDatabase(string path, bool readOnly); void Execute(string sql); SqliteStatement Prepare(string sql); long ScalarInt64(string sql); string ScalarText(string sql); string ErrorMessage(); }`
  - `internal sealed class SqliteStatement : IDisposable { void Bind(int i, long v); void Bind(int i, double v); void Bind(int i, string? v); void Bind(int i, object v); bool Step(); long ColumnInt64(int c); double ColumnDouble(int c); bool ColumnIsNull(int c); string ColumnText(int c); }`
  - `internal sealed class SqliteException : Exception`
  - Test helpers: `Check(bool, string)`, `TempDir(string) -> string`.

- [ ] **Step 1: Create folders, copy the native library and the mod writer sources**

```powershell
$root = "D:\UNITY VALHEIM\ActivityViewer"
$mod = "D:\UNITY VALHEIM\Projetos Visual Studio\valheim v_1.0\Vikings_ActivityLog"
New-Item -ItemType Directory -Force "$root\native", "$root\src\ActivityViewer.Core\Sqlite", "$root\tests\ActivityViewer.Tests\ModWriter" | Out-Null
Copy-Item "$mod\natives\win-x64\e_sqlite3.dll" "$root\native\e_sqlite3.dll"
Copy-Item "$mod\Core\Vikings_ActivityLog.Core.ActivityEventType.cs", "$mod\Core\Vikings_ActivityLog.Core.ActivityRecord.cs", "$mod\Storage\Vikings_ActivityLog.Storage.NativeLoader.cs", "$mod\Storage\Vikings_ActivityLog.Storage.SqliteNative.cs", "$mod\Storage\Vikings_ActivityLog.Storage.SqliteDatabase.cs", "$mod\Storage\Vikings_ActivityLog.Storage.Schema.cs", "$mod\Storage\Vikings_ActivityLog.Storage.ActivityStore.cs" "$root\tests\ActivityViewer.Tests\ModWriter\"
```

Expected: `native\e_sqlite3.dll` is 1,759,232 bytes; `ModWriter` holds 7 `.cs` files.

- [ ] **Step 2: Create the Core project**

`src/ActivityViewer.Core/ActivityViewer.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <RootNamespace>ActivityViewer.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="..\..\native\e_sqlite3.dll" LogicalName="e_sqlite3.dll" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="ActivityViewer.Tests" />
    <InternalsVisibleTo Include="Vikings_ActivityViewer" />
  </ItemGroup>
</Project>
```

`src/ActivityViewer.Core/AppPaths.cs`:

```csharp
using System;
using System.IO;

namespace ActivityViewer.Core
{
    public static class AppPaths
    {
        private const string FolderName = "Vikings_ActivityViewer";

        private static string? _roamingOverride;
        private static string? _localOverride;

        public static string RoamingRoot =>
            _roamingOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        public static string LocalDataRoot =>
            _localOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

        public static void Override(string roaming, string local)
        {
            _roamingOverride = roaming;
            _localOverride = local;
        }
    }
}
```

- [ ] **Step 3: Create the test project and runner**

`tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AssemblyName>ActivityViewer.Tests</AssemblyName>
    <NoWarn>$(NoWarn);CS0649</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ActivityViewer.Core\ActivityViewer.Core.csproj" />
  </ItemGroup>
</Project>
```

`Main` loads the mod writer's SQLite bindings from the same extracted file the Core uses, so the process has a single SQLite library.

`tests/ActivityViewer.Tests/Program.cs`:

```csharp
using System;
using System.IO;
using ActivityViewer.Core;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Check(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("PASS " + label);
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL " + label);
            }
        }

        private static string TempDir(string name)
        {
            string path = Path.Combine(Path.GetTempPath(), "ActivityViewer_Tests", name);
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            return path;
        }

        private static int Main()
        {
            string root = TempDir("approot");
            AppPaths.Override(Path.Combine(root, "roaming"), Path.Combine(root, "local"));
            ActivityViewer.Core.Sqlite.SqliteRuntime.EnsureLoaded();
            Vikings_ActivityLog.SqliteNative.Load(Path.GetDirectoryName(ActivityViewer.Core.Sqlite.SqliteRuntime.LoadedFrom)!);
            RunAll();
            Console.WriteLine(_failed == 0 ? _passed + " checks passed." : _failed + " checks FAILED, " + _passed + " passed.");
            return _failed == 0 ? 0 : 1;
        }
    }
}
```

`tests/ActivityViewer.Tests/Program.Suites.cs` (declares every suite of this plan; a suite without an implementation is removed by the compiler, so later tasks only add their own file):

```csharp
namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private static void RunAll()
        {
            RunSqlite();
            RunDatabase();
            RunDamageAndItems();
            RunInteractionsAndAnalyses();
            RunExport();
            RunProfiles();
            RunTransfer();
            RunDownload();
            RunInput();
        }

        static partial void RunSqlite();
        static partial void RunDatabase();
        static partial void RunDamageAndItems();
        static partial void RunInteractionsAndAnalyses();
        static partial void RunExport();
        static partial void RunProfiles();
        static partial void RunTransfer();
        static partial void RunDownload();
        static partial void RunInput();
    }
}
```

- [ ] **Step 4: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Sqlite.cs`:

```csharp
using System;
using System.IO;
using ActivityViewer.Core;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunSqlite()
        {
            Sqlite_RuntimeExtractsAndLoads();
            Sqlite_ExtractIsIdempotent();
            Sqlite_ReadOnlyReadsTypesAndRefusesWrites();
        }

        private static void Sqlite_RuntimeExtractsAndLoads()
        {
            SqliteRuntime.EnsureLoaded();
            Check(SqliteNative.IsLoaded, "Native SQLite is loaded");
            string loaded = SqliteRuntime.LoadedFrom ?? "";
            Check(File.Exists(loaded), "Extracted library exists");
            Check(loaded.StartsWith(Path.Combine(AppPaths.LocalDataRoot, "native"), StringComparison.OrdinalIgnoreCase), "Library extracted under LocalDataRoot\\native");
        }

        private static void Sqlite_ExtractIsIdempotent()
        {
            string first = SqliteRuntime.Extract(AppPaths.LocalDataRoot);
            string second = SqliteRuntime.Extract(AppPaths.LocalDataRoot);
            Check(first == second, "Extract returns the same path twice");
            Check(new FileInfo(first).Length == 1759232, "Extracted library has the expected size");
        }

        private static void Sqlite_ReadOnlyReadsTypesAndRefusesWrites()
        {
            string path = Path.Combine(TempDir("sqlite_ro"), "t.db");
            using (var writer = new SqliteDatabase(path, readOnly: false))
            {
                writer.Execute("CREATE TABLE t (i INTEGER, d REAL, n REAL, s TEXT);");
                writer.Execute("INSERT INTO t VALUES (7, 2.5, NULL, 'Þór 🙂');");
            }

            using var reader = new SqliteDatabase(path, readOnly: true);
            using (SqliteStatement statement = reader.Prepare("SELECT i, d, n, s FROM t WHERE i = ?;"))
            {
                statement.Bind(1, (object)7L);
                Check(statement.Step(), "Row found with object binding");
                Check(statement.ColumnInt64(0) == 7, "Integer column read");
                Check(Math.Abs(statement.ColumnDouble(1) - 2.5) < 1e-9, "Double column read");
                Check(statement.ColumnIsNull(2), "Null column detected");
                Check(!statement.ColumnIsNull(1), "Non-null column not reported as null");
                Check(statement.ColumnText(3) == "Þór 🙂", "UTF-8 text read");
            }

            bool refused = false;
            try { reader.Execute("INSERT INTO t VALUES (1, 1, 1, 'x');"); }
            catch (SqliteException) { refused = true; }
            Check(refused, "Read-only connection refuses writes");
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile errors `The type or namespace name 'Sqlite' does not exist in the namespace 'ActivityViewer.Core'` (and `SqliteRuntime`/`SqliteDatabase` not found).

- [ ] **Step 6: Implement the SQLite layer**

`src/ActivityViewer.Core/Sqlite/SqliteNative.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace ActivityViewer.Core.Sqlite
{
    internal static class SqliteNative
    {
        internal const int Ok = 0;
        internal const int Row = 100;
        internal const int Done = 101;
        internal const int TypeNull = 5;
        internal const int OpenReadOnly = 0x1;
        internal const int OpenReadWrite = 0x2;
        internal const int OpenCreate = 0x4;
        internal const int OpenFullMutex = 0x10000;
        internal static readonly IntPtr Transient = new IntPtr(-1);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OpenV2Fn(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CloseV2Fn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ExecFn(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr errorMessage);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FreeFn(IntPtr pointer);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ErrMsgFn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PrepareV2Fn(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindInt64Fn(IntPtr statement, int index, long value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindDoubleFn(IntPtr statement, int index, double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindTextFn(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindNullFn(IntPtr statement, int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StepFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FinalizeFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate long ColumnInt64Fn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate double ColumnDoubleFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ColumnTypeFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ColumnTextFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ColumnBytesFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BusyTimeoutFn(IntPtr db, int milliseconds);

        internal static OpenV2Fn OpenV2 = null!;
        internal static CloseV2Fn CloseV2 = null!;
        internal static ExecFn Exec = null!;
        internal static FreeFn Free = null!;
        internal static ErrMsgFn ErrMsg = null!;
        internal static PrepareV2Fn PrepareV2 = null!;
        internal static BindInt64Fn BindInt64 = null!;
        internal static BindDoubleFn BindDouble = null!;
        internal static BindTextFn BindText = null!;
        internal static BindNullFn BindNull = null!;
        internal static StepFn Step = null!;
        internal static FinalizeFn FinalizeStatement = null!;
        internal static ColumnInt64Fn ColumnInt64 = null!;
        internal static ColumnDoubleFn ColumnDouble = null!;
        internal static ColumnTypeFn ColumnType = null!;
        internal static ColumnTextFn ColumnText = null!;
        internal static ColumnBytesFn ColumnBytes = null!;
        internal static BusyTimeoutFn BusyTimeout = null!;

        private static readonly object LoadLock = new object();

        internal static bool IsLoaded { get; private set; }

        internal static void Load(string libraryPath)
        {
            lock (LoadLock)
            {
                if (IsLoaded) return;

                IntPtr library = NativeLibrary.Load(libraryPath);
                OpenV2 = Get<OpenV2Fn>(library, "sqlite3_open_v2");
                CloseV2 = Get<CloseV2Fn>(library, "sqlite3_close_v2");
                Exec = Get<ExecFn>(library, "sqlite3_exec");
                Free = Get<FreeFn>(library, "sqlite3_free");
                ErrMsg = Get<ErrMsgFn>(library, "sqlite3_errmsg");
                PrepareV2 = Get<PrepareV2Fn>(library, "sqlite3_prepare_v2");
                BindInt64 = Get<BindInt64Fn>(library, "sqlite3_bind_int64");
                BindDouble = Get<BindDoubleFn>(library, "sqlite3_bind_double");
                BindText = Get<BindTextFn>(library, "sqlite3_bind_text");
                BindNull = Get<BindNullFn>(library, "sqlite3_bind_null");
                Step = Get<StepFn>(library, "sqlite3_step");
                FinalizeStatement = Get<FinalizeFn>(library, "sqlite3_finalize");
                ColumnInt64 = Get<ColumnInt64Fn>(library, "sqlite3_column_int64");
                ColumnDouble = Get<ColumnDoubleFn>(library, "sqlite3_column_double");
                ColumnType = Get<ColumnTypeFn>(library, "sqlite3_column_type");
                ColumnText = Get<ColumnTextFn>(library, "sqlite3_column_text");
                ColumnBytes = Get<ColumnBytesFn>(library, "sqlite3_column_bytes");
                BusyTimeout = Get<BusyTimeoutFn>(library, "sqlite3_busy_timeout");
                IsLoaded = true;
            }
        }

        private static T Get<T>(IntPtr library, string name) where T : Delegate
        {
            return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
        }
    }
}
```

`src/ActivityViewer.Core/Sqlite/SqliteDatabase.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ActivityViewer.Core.Sqlite
{
    internal sealed class SqliteException : Exception
    {
        internal SqliteException(string message) : base(message) { }
    }

    internal sealed class SqliteDatabase : IDisposable
    {
        private IntPtr _handle;

        internal SqliteDatabase(string path, bool readOnly)
        {
            int flags = readOnly
                ? SqliteNative.OpenReadOnly | SqliteNative.OpenFullMutex
                : SqliteNative.OpenReadWrite | SqliteNative.OpenCreate | SqliteNative.OpenFullMutex;
            int result = SqliteNative.OpenV2(ToUtf8Z(path), out _handle, flags, IntPtr.Zero);
            if (result != SqliteNative.Ok)
            {
                string message = ErrorMessage();
                SqliteNative.CloseV2(_handle);
                _handle = IntPtr.Zero;
                throw new SqliteException("Não foi possível abrir " + path + ": " + message);
            }

            SqliteNative.BusyTimeout(_handle, 5000);
        }

        internal void Execute(string sql)
        {
            int result = SqliteNative.Exec(_handle, ToUtf8Z(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr error);
            if (result == SqliteNative.Ok) return;

            string message = error != IntPtr.Zero ? FromUtf8Z(error) : ErrorMessage();
            if (error != IntPtr.Zero) SqliteNative.Free(error);
            throw new SqliteException(message);
        }

        internal SqliteStatement Prepare(string sql)
        {
            byte[] bytes = ToUtf8Z(sql);
            int result = SqliteNative.PrepareV2(_handle, bytes, bytes.Length, out IntPtr statement, IntPtr.Zero);
            if (result != SqliteNative.Ok) throw new SqliteException(ErrorMessage());
            return new SqliteStatement(this, statement);
        }

        internal long ScalarInt64(string sql)
        {
            using SqliteStatement statement = Prepare(sql);
            return statement.Step() ? statement.ColumnInt64(0) : 0;
        }

        internal string ScalarText(string sql)
        {
            using SqliteStatement statement = Prepare(sql);
            return statement.Step() ? statement.ColumnText(0) : "";
        }

        internal string ErrorMessage() => FromUtf8Z(SqliteNative.ErrMsg(_handle));

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            SqliteNative.CloseV2(_handle);
            _handle = IntPtr.Zero;
        }

        internal static byte[] ToUtf8Z(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] result = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        internal static string FromUtf8Z(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            return Marshal.PtrToStringUTF8(pointer) ?? "";
        }
    }

    internal sealed class SqliteStatement : IDisposable
    {
        private static readonly byte[] EmptyText = { 0 };

        private readonly SqliteDatabase _database;
        private IntPtr _handle;

        internal SqliteStatement(SqliteDatabase database, IntPtr handle)
        {
            _database = database;
            _handle = handle;
        }

        internal void Bind(int index, long value) => Check(SqliteNative.BindInt64(_handle, index, value));

        internal void Bind(int index, double value) => Check(SqliteNative.BindDouble(_handle, index, value));

        internal void Bind(int index, string? value)
        {
            if (value == null)
            {
                Check(SqliteNative.BindNull(_handle, index));
                return;
            }

            byte[] bytes = value.Length == 0 ? EmptyText : Encoding.UTF8.GetBytes(value);
            int length = value.Length == 0 ? 0 : bytes.Length;
            Check(SqliteNative.BindText(_handle, index, bytes, length, SqliteNative.Transient));
        }

        internal void Bind(int index, object value)
        {
            switch (value)
            {
                case long l: Bind(index, l); break;
                case int i: Bind(index, (long)i); break;
                case double d: Bind(index, d); break;
                case string s: Bind(index, s); break;
                default: throw new ArgumentException("Tipo de parâmetro não suportado: " + value.GetType().Name);
            }
        }

        internal bool Step()
        {
            int result = SqliteNative.Step(_handle);
            if (result == SqliteNative.Row) return true;
            if (result == SqliteNative.Done) return false;
            throw new SqliteException(_database.ErrorMessage());
        }

        internal long ColumnInt64(int column) => SqliteNative.ColumnInt64(_handle, column);

        internal double ColumnDouble(int column) => SqliteNative.ColumnDouble(_handle, column);

        internal bool ColumnIsNull(int column) => SqliteNative.ColumnType(_handle, column) == SqliteNative.TypeNull;

        internal string ColumnText(int column)
        {
            IntPtr text = SqliteNative.ColumnText(_handle, column);
            int bytes = SqliteNative.ColumnBytes(_handle, column);
            if (text == IntPtr.Zero || bytes == 0) return "";
            byte[] buffer = new byte[bytes];
            Marshal.Copy(text, buffer, 0, bytes);
            return Encoding.UTF8.GetString(buffer);
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            SqliteNative.FinalizeStatement(_handle);
            _handle = IntPtr.Zero;
        }

        private void Check(int result)
        {
            if (result != SqliteNative.Ok) throw new SqliteException(_database.ErrorMessage());
        }
    }
}
```

`src/ActivityViewer.Core/Sqlite/SqliteRuntime.cs`:

```csharp
using System;
using System.IO;
using System.Security.Cryptography;

namespace ActivityViewer.Core.Sqlite
{
    internal static class SqliteRuntime
    {
        private const string ResourceName = "e_sqlite3.dll";
        private static readonly object Gate = new object();

        internal static string? LoadedFrom { get; private set; }

        internal static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (SqliteNative.IsLoaded) return;
                string path = Extract(AppPaths.LocalDataRoot);
                SqliteNative.Load(path);
                LoadedFrom = path;
            }
        }

        internal static string Extract(string root)
        {
            byte[] bytes = ReadResource();
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string directory = Path.Combine(root, "native", hash);
            string path = Path.Combine(directory, ResourceName);
            if (File.Exists(path) && new FileInfo(path).Length == bytes.Length) return path;

            Directory.CreateDirectory(directory);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, bytes);
            try
            {
                File.Move(temp, path, true);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temp);
            }
            catch (UnauthorizedAccessException) when (File.Exists(path))
            {
                File.Delete(temp);
            }
            return path;
        }

        private static byte[] ReadResource()
        {
            using Stream? stream = typeof(SqliteRuntime).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) throw new InvalidOperationException("Recurso e_sqlite3.dll ausente no programa.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
```

Create the solution:

```powershell
cd "D:\UNITY VALHEIM\ActivityViewer"
dotnet new sln -n Vikings_ActivityViewer --format sln
dotnet sln Vikings_ActivityViewer.sln add src/ActivityViewer.Core/ActivityViewer.Core.csproj tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `12 checks passed.`

- [ ] **Step 8: Checkpoint**

Build has 0 warnings in the Core project; tests pass.

---

### Task 2: Result table, filters and the activity database

**Files:**
- Create: `src/ActivityViewer.Core/Data/ResultTable.cs`, `TimeFormat.cs`, `QueryFilter.cs`, `SqlBuilder.cs`, `QueryReader.cs`, `ActivityDatabase.cs`
- Create: `tests/ActivityViewer.Tests/Program.TestData.cs`, `Program.Database.cs`

**Interfaces:**
- Consumes: `SqliteDatabase`, `SqliteStatement`, `SqliteRuntime` (Task 1); mod writer `Vikings_ActivityLog.ActivityStore`, `ActivityRecord`, `ActivityItem`, `ActivityDamage`, `ActivityEventType` (ModWriter).
- Produces:
  - `public sealed class ResultColumn { ResultColumn(string name, Type type); string Name; Type Type; }`
  - `public sealed class ResultTable { const int MaxRows = 200000; ResultTable(params ResultColumn[] columns); IReadOnlyList<ResultColumn> Columns; List<object?[]> Rows; bool Truncated; TimeSpan Elapsed; int IndexOf(string name); object? Value(int row, string column); }`
  - `public static class TimeFormat { const string Pattern = "dd/MM/yyyy HH:mm:ss"; static DateTime ToLocal(long utcMs); static long ToUtcMs(DateTime local); }`
  - `public sealed class QueryFilter { string? PlatformId; DateTime? From; DateTime? To; int RowLimit = ResultTable.MaxRows; static QueryFilter All; }` (export re-runs a query with `RowLimit = int.MaxValue`)
  - `internal sealed class SqlBuilder { SqlBuilder(string select); SqlBuilder Where(string condition, params object[] values); SqlBuilder WhereIn(string column, IEnumerable<string> values); SqlBuilder ApplyFilter(QueryFilter filter, string alias); SqlBuilder Append(string tail); SqliteStatement Prepare(SqliteDatabase db); string Sql; }`
  - `internal static class QueryReader { static ResultTable Read(ActivityDatabase database, SqlBuilder sql, ResultTable table, Func<SqliteStatement, object?[]> map, int limit, CancellationToken token); }`
  - `public sealed class PlayerInfo { string PlatformId; string Name; string Display; }`
  - `public sealed class InvalidActivityDatabaseException : Exception`
  - `public sealed class ActivityDatabase : IDisposable { const int SupportedSchemaVersion = 1; static ActivityDatabase Open(string path); string Path; string QuickCheck(); IReadOnlyList<PlayerInfo> Players(); IReadOnlyList<string> DistinctTargets(params string[] events); IReadOnlyList<string> DistinctPrefabs(); internal T Run<T>(Func<SqliteDatabase, T> work); }`
  - Test helpers: `Rec(...)`, `BuildDb(string name, params ActivityRecord[] records) -> string`, `T0` constant.

- [ ] **Step 1: Write the test data builder**

`tests/ActivityViewer.Tests/Program.TestData.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private const long T0 = 1767225600000;

        private static Mod.ActivityRecord Rec(Mod.ActivityEventType type, long offsetMs, string platform = "Steam_1", string name = "Ragnar",
            float x = 10f, float y = 30f, float z = 10f, string target = "", int amount = 0, string details = "")
        {
            return new Mod.ActivityRecord
            {
                Type = type,
                TimeUtcMs = T0 + offsetMs,
                PlatformId = platform,
                PlayerName = name,
                PlayerId = platform == "Steam_1" ? 101 : 202,
                X = x,
                Y = y,
                Z = z,
                Target = target,
                Amount = amount,
                Details = details
            };
        }

        private static Mod.ActivityItem Item(string prefab, int count, int quality = 1, string source = "", long crafterId = 0, string crafterName = "")
        {
            return new Mod.ActivityItem { Prefab = prefab, Count = count, Quality = quality, Source = source, CrafterId = crafterId, CrafterName = crafterName };
        }

        private static string BuildDb(string name, params Mod.ActivityRecord[] records)
        {
            string path = Path.Combine(TempDir("db_" + name), name + ".db");
            using (var store = new Mod.ActivityStore(path))
            {
                if (records.Length > 0) store.WriteBatch(new List<Mod.ActivityRecord>(records));
            }
            return path;
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Database.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Sqlite;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunDatabase()
        {
            Database_OpensModDatabaseAndListsPlayers();
            Database_RefusesNewerSchema();
            Database_RefusesOtherFiles();
            Database_ReadsWalCopyWithoutShm();
            Database_Suggestions();
            Database_TimeFormatRoundTrip();
            Database_SqlBuilderAppliesFilter();
        }

        private static void Database_OpensModDatabaseAndListsPlayers()
        {
            string path = BuildDb("players",
                Rec(Mod.ActivityEventType.Ping, 0, "Steam_2", "Bjorn", amount: 40),
                Rec(Mod.ActivityEventType.Ping, 1000, "Steam_1", "Ragnar", amount: 50));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            IReadOnlyList<PlayerInfo> players = db.Players();
            Check(players.Count == 2, "Two players listed");
            Check(players[0].Name == "Bjorn" && players[1].Name == "Ragnar", "Players sorted by name");
            Check(players[1].Display == "Ragnar (Steam_1)", "Player display shows name and platform id");
            Check(db.QuickCheck() == "ok", "Quick check is ok");
        }

        private static void Database_RefusesNewerSchema()
        {
            string path = BuildDb("newer");
            using (var writer = new SqliteDatabase(path, readOnly: false)) writer.Execute("UPDATE schema_info SET version = 2;");
            string message = "";
            try { using ActivityDatabase db = ActivityDatabase.Open(path); }
            catch (InvalidActivityDatabaseException error) { message = error.Message; }
            Check(message == "Banco de uma versão mais nova do mod. Atualize o Viewer.", "Newer schema refused with the update message");
        }

        private static void Database_RefusesOtherFiles()
        {
            string dir = TempDir("otherfiles");
            string sqlite = Path.Combine(dir, "other.db");
            using (var writer = new SqliteDatabase(sqlite, readOnly: false)) writer.Execute("CREATE TABLE x (a INTEGER);");
            string text = Path.Combine(dir, "text.db");
            File.WriteAllText(text, "isto não é um banco de dados, apenas texto comum para o teste");

            string first = "";
            try { using ActivityDatabase db = ActivityDatabase.Open(sqlite); }
            catch (InvalidActivityDatabaseException error) { first = error.Message; }
            Check(first == "O arquivo não é um banco do Vikings_ActivityLog.", "SQLite file without schema_info refused");

            bool second = false;
            try { using ActivityDatabase db = ActivityDatabase.Open(text); }
            catch (InvalidActivityDatabaseException) { second = true; }
            Check(second, "Non-SQLite file refused with InvalidActivityDatabaseException");

            bool missing = false;
            try { using ActivityDatabase db = ActivityDatabase.Open(Path.Combine(dir, "missing.db")); }
            catch (FileNotFoundException) { missing = true; }
            Check(missing, "Missing file reported as FileNotFoundException");
        }

        private static void Database_ReadsWalCopyWithoutShm()
        {
            string dir = TempDir("walcopy");
            string source = Path.Combine(dir, "live.db");
            string copyDir = Path.Combine(dir, "copy");
            Directory.CreateDirectory(copyDir);
            string copy = Path.Combine(copyDir, "live.db");

            using (var store = new Mod.ActivityStore(source))
            {
                store.WriteBatch(new List<Mod.ActivityRecord> { Rec(Mod.ActivityEventType.Ping, 0, amount: 33) });
                CopyShared(source, copy);
                CopyShared(source + "-wal", copy + "-wal");
            }

            Check(!File.Exists(copy + "-shm"), "Copy starts without -shm");
            using ActivityDatabase db = ActivityDatabase.Open(copy);
            Check(db.Players().Count == 1, "Rows that exist only in the WAL are visible in the copy");
            Check(db.QuickCheck() == "ok", "WAL copy passes quick check");
        }

        private static void CopyShared(string from, string to)
        {
            using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using FileStream output = File.Create(to);
            input.CopyTo(output);
        }

        private static void Database_Suggestions()
        {
            Mod.ActivityRecord damage = Rec(Mod.ActivityEventType.Damage, 0, target: "Boar");
            damage.Damage = new Mod.ActivityDamage { Slash = 5f, Total = 5f };
            Mod.ActivityRecord pickup = Rec(Mod.ActivityEventType.Pickup, 10);
            pickup.Items.Add(Item("Wood", 10));
            pickup.Items.Add(Item("Stone", 2));
            string path = BuildDb("suggest", damage, pickup, Rec(Mod.ActivityEventType.Interact, 20, target: "piece_chest", details: "result:True"));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            IReadOnlyList<string> prefabs = db.DistinctPrefabs();
            Check(prefabs.Count == 2 && prefabs[0] == "Stone" && prefabs[1] == "Wood", "Distinct prefabs sorted");
            IReadOnlyList<string> targets = db.DistinctTargets("Damage");
            Check(targets.Count == 1 && targets[0] == "Boar", "Distinct damage targets");
            Check(db.DistinctTargets("Interact", "Use", "Text").Count == 1, "Distinct interaction targets");
        }

        private static void Database_TimeFormatRoundTrip()
        {
            DateTime local = TimeFormat.ToLocal(T0);
            Check(local.Kind == DateTimeKind.Local, "ToLocal returns local time");
            Check(TimeFormat.ToUtcMs(local) == T0, "Local time converts back to the same UTC ms");
        }

        private static void Database_SqlBuilderAppliesFilter()
        {
            var filter = new QueryFilter { PlatformId = "Steam_1", From = TimeFormat.ToLocal(T0), To = TimeFormat.ToLocal(T0 + 5000) };
            SqlBuilder sql = new SqlBuilder("SELECT e.id FROM events e").Where("e.amount >= ?", 3L).ApplyFilter(filter, "e").Append("ORDER BY e.id");
            Check(sql.Sql == "SELECT e.id FROM events e WHERE (e.amount >= ?) AND (e.platform_id = ?) AND (e.time_utc >= ?) AND (e.time_utc <= ?) ORDER BY e.id", "SQL assembled with filter conditions");

            string path = BuildDb("builder",
                Rec(Mod.ActivityEventType.Ping, 0, amount: 5),
                Rec(Mod.ActivityEventType.Ping, 1000, amount: 1),
                Rec(Mod.ActivityEventType.Ping, 9000, amount: 5),
                Rec(Mod.ActivityEventType.Ping, 2000, "Steam_2", "Bjorn", amount: 5));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            var table = new ResultTable(new ResultColumn("Id", typeof(long)));
            QueryReader.Read(db, sql, table, s => new object?[] { s.ColumnInt64(0) }, ResultTable.MaxRows, default);
            Check(table.Rows.Count == 1, "Filter keeps only matching player, period and amount");
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile errors `The type or namespace name 'Data' does not exist in the namespace 'ActivityViewer.Core'`.

- [ ] **Step 4: Implement the data basics**

`src/ActivityViewer.Core/Data/ResultTable.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ActivityViewer.Core.Data
{
    public sealed class ResultColumn
    {
        public ResultColumn(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        public Type Type { get; }
    }

    public sealed class ResultTable
    {
        public const int MaxRows = 200000;

        public ResultTable(params ResultColumn[] columns)
        {
            Columns = columns;
        }

        public IReadOnlyList<ResultColumn> Columns { get; }

        public List<object?[]> Rows { get; } = new List<object?[]>();

        public bool Truncated { get; set; }

        public TimeSpan Elapsed { get; set; }

        public int IndexOf(string name)
        {
            for (int i = 0; i < Columns.Count; i++)
                if (Columns[i].Name == name) return i;
            return -1;
        }

        public object? Value(int row, string column) => Rows[row][IndexOf(column)];
    }
}
```

`src/ActivityViewer.Core/Data/TimeFormat.cs`:

```csharp
using System;

namespace ActivityViewer.Core.Data
{
    public static class TimeFormat
    {
        public const string Pattern = "dd/MM/yyyy HH:mm:ss";

        public static DateTime ToLocal(long utcMs) => DateTimeOffset.FromUnixTimeMilliseconds(utcMs).LocalDateTime;

        public static long ToUtcMs(DateTime local) =>
            new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeMilliseconds();
    }
}
```

`src/ActivityViewer.Core/Data/QueryFilter.cs`:

```csharp
using System;

namespace ActivityViewer.Core.Data
{
    public sealed class QueryFilter
    {
        public static QueryFilter All { get; } = new QueryFilter();

        public string? PlatformId { get; init; }

        public DateTime? From { get; init; }

        public DateTime? To { get; init; }

        public int RowLimit { get; init; } = ResultTable.MaxRows;
    }
}
```

`src/ActivityViewer.Core/Data/SqlBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal sealed class SqlBuilder
    {
        private readonly StringBuilder _sql;
        private readonly List<object> _values = new List<object>();
        private bool _hasWhere;

        internal SqlBuilder(string select)
        {
            _sql = new StringBuilder(select);
        }

        internal string Sql => _sql.ToString();

        internal SqlBuilder Where(string condition, params object[] values)
        {
            _sql.Append(_hasWhere ? " AND (" : " WHERE (").Append(condition).Append(')');
            _hasWhere = true;
            _values.AddRange(values);
            return this;
        }

        internal SqlBuilder WhereIn(string column, IEnumerable<string> values)
        {
            var list = new List<object>(values);
            var marks = new StringBuilder();
            for (int i = 0; i < list.Count; i++) marks.Append(i == 0 ? "?" : ", ?");
            return Where(column + " IN (" + marks + ")", list.ToArray());
        }

        internal SqlBuilder ApplyFilter(QueryFilter filter, string alias)
        {
            if (!string.IsNullOrEmpty(filter.PlatformId)) Where(alias + ".platform_id = ?", filter.PlatformId);
            if (filter.From.HasValue) Where(alias + ".time_utc >= ?", TimeFormat.ToUtcMs(filter.From.Value));
            if (filter.To.HasValue) Where(alias + ".time_utc <= ?", TimeFormat.ToUtcMs(filter.To.Value));
            return this;
        }

        internal SqlBuilder Append(string tail)
        {
            _sql.Append(' ').Append(tail);
            return this;
        }

        internal SqliteStatement Prepare(SqliteDatabase database)
        {
            SqliteStatement statement = database.Prepare(_sql.ToString());
            for (int i = 0; i < _values.Count; i++) statement.Bind(i + 1, _values[i]);
            return statement;
        }
    }
}
```

`src/ActivityViewer.Core/Data/QueryReader.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal static class QueryReader
    {
        internal static ResultTable Read(ActivityDatabase database, SqlBuilder sql, ResultTable table,
            Func<SqliteStatement, object?[]> map, int limit, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            database.Run(db =>
            {
                using SqliteStatement statement = sql.Prepare(db);
                while (statement.Step())
                {
                    if ((table.Rows.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                    if (table.Rows.Count >= limit)
                    {
                        table.Truncated = true;
                        break;
                    }
                    table.Rows.Add(map(statement));
                }
                return 0;
            });
            table.Elapsed = watch.Elapsed;
            return table;
        }
    }
}
```

`src/ActivityViewer.Core/Data/ActivityDatabase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public sealed class InvalidActivityDatabaseException : Exception
    {
        public InvalidActivityDatabaseException(string message) : base(message) { }
    }

    public sealed class PlayerInfo
    {
        public PlayerInfo(string platformId, string name)
        {
            PlatformId = platformId;
            Name = name;
        }

        public string PlatformId { get; }

        public string Name { get; }

        public string Display => Name + " (" + PlatformId + ")";

        public override string ToString() => Display;
    }

    public sealed class ActivityDatabase : IDisposable
    {
        public const int SupportedSchemaVersion = 1;

        private readonly SqliteDatabase _db;
        private readonly object _gate = new object();

        private ActivityDatabase(string path, SqliteDatabase db)
        {
            Path = path;
            _db = db;
        }

        public string Path { get; }

        public static ActivityDatabase Open(string path)
        {
            SqliteRuntime.EnsureLoaded();
            if (!File.Exists(path)) throw new FileNotFoundException("Arquivo não encontrado.", path);

            SqliteDatabase db;
            try
            {
                db = new SqliteDatabase(path, readOnly: true);
            }
            catch (SqliteException error)
            {
                throw new InvalidActivityDatabaseException("O arquivo não é um banco SQLite válido: " + error.Message);
            }

            try
            {
                long tables;
                try
                {
                    tables = db.ScalarInt64("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_info';");
                }
                catch (SqliteException error)
                {
                    throw new InvalidActivityDatabaseException("O arquivo não é um banco SQLite válido: " + error.Message);
                }

                if (tables == 0) throw new InvalidActivityDatabaseException("O arquivo não é um banco do Vikings_ActivityLog.");
                long version = db.ScalarInt64("SELECT version FROM schema_info LIMIT 1;");
                if (version > SupportedSchemaVersion) throw new InvalidActivityDatabaseException("Banco de uma versão mais nova do mod. Atualize o Viewer.");
                return new ActivityDatabase(path, db);
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        public string QuickCheck() => Run(db => db.ScalarText("PRAGMA quick_check;"));

        public IReadOnlyList<PlayerInfo> Players()
        {
            return Run(db =>
            {
                var players = new List<PlayerInfo>();
                using SqliteStatement statement = db.Prepare("SELECT platform_id, last_name FROM players ORDER BY last_name COLLATE NOCASE, platform_id;");
                while (statement.Step()) players.Add(new PlayerInfo(statement.ColumnText(0), statement.ColumnText(1)));
                return players;
            });
        }

        public IReadOnlyList<string> DistinctTargets(params string[] events)
        {
            SqlBuilder sql = new SqlBuilder("SELECT DISTINCT e.target FROM events e")
                .WhereIn("e.event", events)
                .Where("e.target <> ''")
                .Append("ORDER BY e.target COLLATE NOCASE;");
            return ReadStrings(sql);
        }

        public IReadOnlyList<string> DistinctPrefabs()
        {
            return ReadStrings(new SqlBuilder("SELECT DISTINCT prefab FROM event_items WHERE prefab <> '' ORDER BY prefab COLLATE NOCASE;"));
        }

        internal T Run<T>(Func<SqliteDatabase, T> work)
        {
            lock (_gate) return work(_db);
        }

        public void Dispose()
        {
            lock (_gate) _db.Dispose();
        }

        private IReadOnlyList<string> ReadStrings(SqlBuilder sql)
        {
            return Run(db =>
            {
                var values = new List<string>();
                using SqliteStatement statement = sql.Prepare(db);
                while (statement.Step()) values.Add(statement.ColumnText(0));
                return values;
            });
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `30 checks passed.` (12 + 18)

- [ ] **Step 6: Checkpoint**

Tests pass.

---
### Task 3: Damage and item queries (tabs 1 and 2)

**Files:**
- Create: `src/ActivityViewer.Core/Data/DamageQuery.cs`, `RouteParser.cs`, `ItemQuery.cs`
- Create: `tests/ActivityViewer.Tests/Program.DamageAndItems.cs`

**Interfaces:**
- Consumes: `ActivityDatabase`, `ResultTable`, `ResultColumn`, `QueryFilter`, `SqlBuilder`, `QueryReader`, `TimeFormat` (Task 2).
- Produces:
  - `public sealed class DamageCriteria { double MinTotal; string Target; string Attacker; }`
  - `public static class DamageQuery { static ResultTable Run(ActivityDatabase database, QueryFilter filter, DamageCriteria criteria, CancellationToken token); }` — columns `Data, Jogador, ID, Evento, Alvo ou atacante, Total, Tipos, Vida após, X, Y, Z`.
  - `public static class RouteParser { const string Ground = "Chão"; static (string Origin, string Destination) Parse(string eventName, string target, string details); }`
  - `public sealed class ItemCriteria { string Prefab; int MinCount; IReadOnlyCollection<string> Events; }`
  - `public static class ItemQuery { static readonly IReadOnlyList<string> AllEvents; static ResultTable Run(ActivityDatabase database, QueryFilter filter, ItemCriteria criteria, CancellationToken token); }` — columns `Data, Jogador, ID, Evento, Item, Quantidade, Qualidade, Criador, Origem, Destino, X, Y, Z`.

- [ ] **Step 1: Write the failing tests**

`tests/ActivityViewer.Tests/Program.DamageAndItems.cs`:

```csharp
using System;
using ActivityViewer.Core.Data;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunDamageAndItems()
        {
            Damage_FiltersAndColumns();
            Items_FiltersAndRoutes();
            Route_ParsesMoveDetails();
        }

        private static void Damage_FiltersAndColumns()
        {
            Mod.ActivityRecord boar = Rec(Mod.ActivityEventType.Damage, 0, target: "Boar");
            boar.Damage = new Mod.ActivityDamage { Slash = 30f, Fire = 2f, Total = 32f };
            Mod.ActivityRecord troll = Rec(Mod.ActivityEventType.Damage, 1000, target: "Troll");
            troll.Damage = new Mod.ActivityDamage { Blunt = 5f, Total = 5f };
            Mod.ActivityRecord hurt = Rec(Mod.ActivityEventType.Damaged, 2000, target: "Greydwarf");
            hurt.Damage = new Mod.ActivityDamage { Pierce = 12f, Total = 12f, HealthAfter = 40f };
            Mod.ActivityRecord other = Rec(Mod.ActivityEventType.Damage, 3000, "Steam_2", "Bjorn", target: "Boar");
            other.Damage = new Mod.ActivityDamage { Slash = 8f, Total = 8f };
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("damage", boar, troll, hurt, other));

            ResultTable all = DamageQuery.Run(db, QueryFilter.All, new DamageCriteria(), default);
            Check(all.Rows.Count == 4, "Damage: all four damage rows");
            Check((string?)all.Value(0, "Alvo ou atacante") == "Boar" && (string?)all.Value(0, "Evento") == "Damage", "Damage: rows ordered by time");
            Check((string?)all.Value(0, "Tipos") == "Corte 30, Fogo 2", "Damage: non-zero damage types listed");
            Check(all.Value(0, "Vida após") == null && (double?)all.Value(2, "Vida após") == 40.0, "Damage: health after only for Damaged");

            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { MinTotal = 10 }, default).Rows.Count == 2, "Damage: minimum total");
            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Target = "boar" }, default).Rows.Count == 2, "Damage: target filter is case-insensitive and keeps only Damage");
            ResultTable attacker = DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Attacker = "GREY" }, default);
            Check(attacker.Rows.Count == 1 && (string?)attacker.Value(0, "Evento") == "Damaged", "Damage: attacker filter keeps only Damaged");
            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Target = "boar", Attacker = "grey" }, default).Rows.Count == 3, "Damage: target and attacker together give the union");
            Check(DamageQuery.Run(db, new QueryFilter { PlatformId = "Steam_2" }, new DamageCriteria(), default).Rows.Count == 1, "Damage: global player filter");
            var period = new QueryFilter { From = TimeFormat.ToLocal(T0 + 500), To = TimeFormat.ToLocal(T0 + 2500) };
            Check(DamageQuery.Run(db, period, new DamageCriteria(), default).Rows.Count == 2, "Damage: global period filter");
            ResultTable capped = DamageQuery.Run(db, new QueryFilter { RowLimit = 2 }, new DamageCriteria(), default);
            Check(capped.Rows.Count == 2 && capped.Truncated, "Damage: row limit truncates and flags the result");
        }

        private static void Items_FiltersAndRoutes()
        {
            Mod.ActivityRecord pickup = Rec(Mod.ActivityEventType.Pickup, 0);
            pickup.Items.Add(Item("Wood", 10));
            Mod.ActivityRecord move = Rec(Mod.ActivityEventType.Move, 1000, amount: 1, details: "from:Inventory to:Baú grande");
            move.Items.Add(Item("SwordIron", 1, 3, crafterId: 9, crafterName: "Bjorn"));
            Mod.ActivityRecord drop = Rec(Mod.ActivityEventType.Drop, 2000, target: "Inventory", amount: 5);
            drop.Items.Add(Item("Stone", 5));
            Mod.ActivityRecord craft = Rec(Mod.ActivityEventType.Craft, 3000, "Steam_2", "Bjorn");
            craft.Items.Add(Item("ArrowWood", 20));
            Mod.ActivityRecord consume = Rec(Mod.ActivityEventType.Consume, 4000, target: "Inventory");
            consume.Items.Add(Item("CookedMeat", 1));
            Mod.ActivityRecord snapshot = Rec(Mod.ActivityEventType.Inventory, 5000);
            snapshot.Items.Add(Item("Wood", 50));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("items", pickup, move, drop, craft, consume, snapshot));

            ResultTable all = ItemQuery.Run(db, QueryFilter.All, new ItemCriteria(), default);
            Check(all.Rows.Count == 5, "Items: five item rows, inventory snapshots excluded");
            Check((string?)all.Value(1, "Item") == "SwordIron" && (long?)all.Value(1, "Qualidade") == 3 && (string?)all.Value(1, "Criador") == "Bjorn", "Items: item, quality and crafter");
            Check((string?)all.Value(1, "Origem") == "Inventory" && (string?)all.Value(1, "Destino") == "Baú grande", "Items: move origin and destination");
            Check((string?)all.Value(2, "Origem") == "Inventory" && (string?)all.Value(2, "Destino") == RouteParser.Ground, "Items: drop goes to the ground");
            Check((string?)all.Value(0, "Origem") == RouteParser.Ground, "Items: pickup comes from the ground");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Prefab = "sword" }, default).Rows.Count == 1, "Items: prefab filter");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { MinCount = 10 }, default).Rows.Count == 2, "Items: minimum quantity");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Events = new[] { "Craft" } }, default).Rows.Count == 1, "Items: event type filter");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Events = Array.Empty<string>() }, default).Rows.Count == 0, "Items: no event types gives no rows");
            Check(ItemQuery.Run(db, new QueryFilter { PlatformId = "Steam_2" }, new ItemCriteria(), default).Rows.Count == 1, "Items: global player filter");
        }

        private static void Route_ParsesMoveDetails()
        {
            Check(RouteParser.Parse("MoveAll", "", "from:Baú de ferro to:Inventory") == ("Baú de ferro", "Inventory"), "Route: names with spaces");
            Check(RouteParser.Parse("Move", "", "texto inesperado") == ("", ""), "Route: unexpected details give empty route");
            Check(RouteParser.Parse("Consume", "Inventory", "") == ("Inventory", "Consumido"), "Route: consume");
            Check(RouteParser.Parse("Equip", "", "") == ("", ""), "Route: other events have no route");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile errors `The name 'DamageQuery' does not exist in the current context` (and `ItemQuery`, `RouteParser`, `DamageCriteria`, `ItemCriteria`).

- [ ] **Step 3: Implement the queries**

`src/ActivityViewer.Core/Data/DamageQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public sealed class DamageCriteria
    {
        public double MinTotal { get; init; }

        public string Target { get; init; } = "";

        public string Attacker { get; init; } = "";
    }

    public static class DamageQuery
    {
        private const int FirstTypeColumn = 6;
        private const int HealthColumn = 17;

        private static readonly string[] TypeNames =
        {
            "Dano", "Contundente", "Corte", "Perfuração", "Fogo", "Gelo", "Raio", "Veneno", "Espírito", "Corte de árvore", "Mineração"
        };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, DamageCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("ID", typeof(string)),
                new ResultColumn("Evento", typeof(string)),
                new ResultColumn("Alvo ou atacante", typeof(string)),
                new ResultColumn("Total", typeof(double)),
                new ResultColumn("Tipos", typeof(string)),
                new ResultColumn("Vida após", typeof(double)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, e.target, d.total, " +
                "d.damage, d.blunt, d.slash, d.pierce, d.fire, d.frost, d.lightning, d.poison, d.spirit, d.chop, d.pickaxe, " +
                "d.health_after, e.x, e.y, e.z FROM events e JOIN event_damage d ON d.event_id = e.id");
            AddEventCondition(sql, criteria);
            if (criteria.MinTotal > 0) sql.Where("d.total >= ?", criteria.MinTotal);
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        private static void AddEventCondition(SqlBuilder sql, DamageCriteria criteria)
        {
            bool hasTarget = criteria.Target.Length > 0;
            bool hasAttacker = criteria.Attacker.Length > 0;
            var parts = new List<string>();
            var values = new List<object>();

            if (hasTarget || !hasAttacker)
            {
                if (hasTarget)
                {
                    parts.Add("(e.event = 'Damage' AND instr(lower(e.target), lower(?)) > 0)");
                    values.Add(criteria.Target);
                }
                else parts.Add("e.event = 'Damage'");
            }

            if (hasAttacker || !hasTarget)
            {
                if (hasAttacker)
                {
                    parts.Add("(e.event = 'Damaged' AND instr(lower(e.target), lower(?)) > 0)");
                    values.Add(criteria.Attacker);
                }
                else parts.Add("e.event = 'Damaged'");
            }

            sql.Where(string.Join(" OR ", parts), values.ToArray());
        }

        private static object?[] Map(SqliteStatement s)
        {
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                s.ColumnText(3),
                s.ColumnText(4),
                s.ColumnDouble(5),
                Types(s),
                s.ColumnIsNull(HealthColumn) ? null : (object)s.ColumnDouble(HealthColumn),
                s.ColumnDouble(18),
                s.ColumnDouble(19),
                s.ColumnDouble(20)
            };
        }

        private static string Types(SqliteStatement s)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < TypeNames.Length; i++)
            {
                double value = s.ColumnDouble(FirstTypeColumn + i);
                if (value == 0) continue;
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(TypeNames[i]).Append(' ').Append(value.ToString("0.##", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
    }
}
```

`src/ActivityViewer.Core/Data/RouteParser.cs`:

```csharp
using System;

namespace ActivityViewer.Core.Data
{
    public static class RouteParser
    {
        public const string Ground = "Chão";

        public static (string Origin, string Destination) Parse(string eventName, string target, string details)
        {
            switch (eventName)
            {
                case "Move":
                case "MoveAll":
                case "StackAll":
                    return ParseMove(details);
                case "Drop":
                    return (target, Ground);
                case "Pickup":
                    return (Ground, "Inventário");
                case "Consume":
                    return (target, "Consumido");
                default:
                    return ("", "");
            }
        }

        private static (string Origin, string Destination) ParseMove(string details)
        {
            if (!details.StartsWith("from:", StringComparison.Ordinal)) return ("", "");
            int to = details.IndexOf(" to:", StringComparison.Ordinal);
            if (to < 0) return (details.Substring(5), "");
            return (details.Substring(5, to - 5), details.Substring(to + 4));
        }
    }
}
```

`src/ActivityViewer.Core/Data/ItemQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public sealed class ItemCriteria
    {
        public string Prefab { get; init; } = "";

        public int MinCount { get; init; }

        public IReadOnlyCollection<string> Events { get; init; } = ItemQuery.AllEvents;
    }

    public static class ItemQuery
    {
        public static readonly IReadOnlyList<string> AllEvents = new[]
        {
            "Pickup", "Drop", "Move", "MoveAll", "StackAll", "Craft", "Equip", "Unequip", "Consume"
        };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, ItemCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("ID", typeof(string)),
                new ResultColumn("Evento", typeof(string)),
                new ResultColumn("Item", typeof(string)),
                new ResultColumn("Quantidade", typeof(long)),
                new ResultColumn("Qualidade", typeof(long)),
                new ResultColumn("Criador", typeof(string)),
                new ResultColumn("Origem", typeof(string)),
                new ResultColumn("Destino", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));
            if (criteria.Events.Count == 0) return table;

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, i.prefab, i.count, i.quality, i.crafter_name, " +
                "e.target, e.details, e.x, e.y, e.z FROM event_items i JOIN events e ON e.id = i.event_id");
            sql.WhereIn("e.event", criteria.Events);
            if (criteria.MinCount > 0) sql.Where("i.count >= ?", (long)criteria.MinCount);
            if (criteria.Prefab.Length > 0) sql.Where("instr(lower(i.prefab), lower(?)) > 0", criteria.Prefab);
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id, i.rowid");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        private static object?[] Map(SqliteStatement s)
        {
            string eventName = s.ColumnText(3);
            (string origin, string destination) = RouteParser.Parse(eventName, s.ColumnText(8), s.ColumnText(9));
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                eventName,
                s.ColumnText(4),
                s.ColumnInt64(5),
                s.ColumnInt64(6),
                s.ColumnText(7),
                origin,
                destination,
                s.ColumnDouble(10),
                s.ColumnDouble(11),
                s.ColumnDouble(12)
            };
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `55 checks passed.` (30 + 11 damage + 10 items + 4 route)

- [ ] **Step 5: Checkpoint**

Tests pass.

---

### Task 4: Interaction query, action frequency and speed analyses (tabs 3, 4 and 5)

**Files:**
- Create: `src/ActivityViewer.Core/Analysis/ActionPoint.cs`, `FrequencyAnalyzer.cs`, `SpeedAnalyzer.cs`
- Create: `src/ActivityViewer.Core/Data/PointReader.cs`, `InteractionQuery.cs`, `FrequencyQuery.cs`, `SpeedQuery.cs`
- Create: `tests/ActivityViewer.Tests/Program.InteractionsAndAnalyses.cs`

**Interfaces:**
- Consumes: Task 2 data types.
- Produces:
  - `public sealed class ActionPoint { ActionPoint(long timeUtcMs, string platformId, string playerName, string eventName, string target, double x, double y, double z); long TimeUtcMs; string PlatformId; string PlayerName; string Event; string Target; double X, Y, Z; }`
  - `public sealed class FrequencyHit { long StartUtcMs; long EndUtcMs; string PlatformId; string PlayerName; int MaxCount; SortedSet<string> Events; SortedSet<string> Objects; double X, Y, Z; }`
  - `public static class FrequencyAnalyzer { const long WindowMs = 1000; static List<FrequencyHit> Analyze(IEnumerable<ActionPoint> points, int maxPerSecond); }`
  - `public sealed class SpeedHit { long FromUtcMs; long ToUtcMs; string PlatformId; string PlayerName; double Speed; double Distance; double Seconds; double FromX, FromZ, ToX, ToZ; }`
  - `public static class SpeedAnalyzer { const long MinElapsedMs = 1000; static readonly IReadOnlyCollection<string> PlayerPositionEvents; static readonly IReadOnlyCollection<string> BarrierEvents; static List<SpeedHit> Analyze(IEnumerable<ActionPoint> points, double maxSpeed); }`
  - `public enum InteractionResult { Any, Success, Failure }`, `public sealed class InteractionCriteria { string Object; InteractionResult Result; }`
  - `public static class InteractionQuery { static ResultTable Run(ActivityDatabase, QueryFilter, InteractionCriteria, CancellationToken); static (string Result, string Info) ParseDetails(string eventName, string details); }` — columns `Data, Jogador, ID, Evento, Objeto, Resultado, Info, Item usado, X, Y, Z`.
  - `public static class FrequencyQuery { static readonly IReadOnlyList<string> Events; static ResultTable Run(ActivityDatabase, QueryFilter, int maxPerSecond, CancellationToken); }` — columns `Data, Jogador, ID, Ações, Eventos, Objetos, X, Y, Z`.
  - `public static class SpeedQuery { static ResultTable Run(ActivityDatabase, QueryFilter, double maxSpeed, CancellationToken); }` — columns `Data, Jogador, ID, Velocidade, Distância, Segundos, De, Para`.

- [ ] **Step 1: Write the failing tests**

`tests/ActivityViewer.Tests/Program.InteractionsAndAnalyses.cs`:

```csharp
using System.Collections.Generic;
using ActivityViewer.Core.Analysis;
using ActivityViewer.Core.Data;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunInteractionsAndAnalyses()
        {
            Interactions_FiltersAndParsing();
            Frequency_Limits();
            Frequency_QueryReadsDatabase();
            Speed_Rules();
            Speed_QueryReadsDatabase();
        }

        private static void Interactions_FiltersAndParsing()
        {
            Mod.ActivityRecord use = Rec(Mod.ActivityEventType.Use, 2000, target: "fire_pit");
            use.Items.Add(Item("Wood", 1));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("interactions",
                Rec(Mod.ActivityEventType.Interact, 0, target: "piece_chest_wood", details: "result:True"),
                Rec(Mod.ActivityEventType.Interact, 1000, target: "door", details: "result:False info:owner:Bjorn active:True"),
                use,
                Rec(Mod.ActivityEventType.Text, 3000, target: "sign", details: "Olá; \"mundo\""),
                Rec(Mod.ActivityEventType.Ping, 4000, amount: 20)));

            ResultTable all = InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria(), default);
            Check(all.Rows.Count == 4, "Interactions: Interact, Use and Text only");
            Check((string?)all.Value(0, "Resultado") == "Sucesso", "Interactions: success result parsed");
            Check((string?)all.Value(1, "Resultado") == "Falha" && (string?)all.Value(1, "Info") == "owner:Bjorn active:True", "Interactions: failure and info parsed");
            Check((string?)all.Value(2, "Item usado") == "Wood", "Interactions: used item shown for Use");
            Check((string?)all.Value(3, "Info") == "Olá; \"mundo\"", "Interactions: text shown for Text");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Object = "CHEST" }, default).Rows.Count == 1, "Interactions: object filter");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Result = InteractionResult.Success }, default).Rows.Count == 1, "Interactions: success filter");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Result = InteractionResult.Failure }, default).Rows.Count == 1, "Interactions: failure filter");
            Check(InteractionQuery.ParseDetails("Interact", "algo inesperado") == ("", ""), "Interactions: unexpected details give empty result");
        }

        private static ActionPoint P(long ms, string player = "A", string eventName = "Pickup", string target = "Wood", double x = 10, double z = 10)
        {
            return new ActionPoint(T0 + ms, player, player == "A" ? "Ragnar" : "Bjorn", eventName, target, x, 30, z);
        }

        private static List<ActionPoint> Burst(long start, int count, long step, string player = "A")
        {
            var points = new List<ActionPoint>();
            for (int i = 0; i < count; i++) points.Add(P(start + i * step, player));
            return points;
        }

        private static void Frequency_Limits()
        {
            Check(FrequencyAnalyzer.Analyze(Burst(0, 7, 100), 7).Count == 0, "Frequency: exactly the limit is not reported");

            List<FrequencyHit> eight = FrequencyAnalyzer.Analyze(Burst(0, 8, 100), 7);
            Check(eight.Count == 1 && eight[0].MaxCount == 8 && eight[0].StartUtcMs == T0, "Frequency: one above the limit is reported once");

            List<ActionPoint> edge = Burst(0, 7, 100);
            edge.Add(P(1000));
            Check(FrequencyAnalyzer.Analyze(edge, 7).Count == 0, "Frequency: events 1000 ms apart are not in the same window");

            List<FrequencyHit> long20 = FrequencyAnalyzer.Analyze(Burst(0, 20, 100), 7);
            Check(long20.Count == 1 && long20[0].MaxCount == 10, "Frequency: a continuous burst is merged into one report");

            List<ActionPoint> two = Burst(0, 8, 100);
            two.AddRange(Burst(10000, 8, 100));
            Check(FrequencyAnalyzer.Analyze(two, 7).Count == 2, "Frequency: separate bursts are separate reports");

            List<ActionPoint> mixed = Burst(0, 5, 100, "A");
            mixed.AddRange(Burst(50, 5, 100, "B"));
            Check(FrequencyAnalyzer.Analyze(mixed, 7).Count == 0, "Frequency: players are counted separately");

            List<ActionPoint> kinds = Burst(0, 7, 100);
            kinds.Add(P(700, eventName: "Place", target: "piece_wall"));
            List<FrequencyHit> kindHits = FrequencyAnalyzer.Analyze(kinds, 7);
            Check(kindHits.Count == 1 && kindHits[0].Events.Contains("Place") && kindHits[0].Objects.Contains("piece_wall"), "Frequency: report lists events and objects");
        }

        private static void Frequency_QueryReadsDatabase()
        {
            var records = new List<Mod.ActivityRecord>();
            for (int i = 0; i < 8; i++) records.Add(Rec(Mod.ActivityEventType.Pickup, i * 100, target: ""));
            records.Add(Rec(Mod.ActivityEventType.Ping, 50, amount: 30));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("frequency", records.ToArray()));
            ResultTable table = FrequencyQuery.Run(db, QueryFilter.All, 7, default);
            Check(table.Rows.Count == 1 && (long?)table.Value(0, "Ações") == 8, "Frequency query: one report with eight actions");
        }

        private static void Speed_Rules()
        {
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(60000, eventName: "Ping", x: 110) }, 150).Count == 0, "Speed: normal movement is not reported");

            List<SpeedHit> fast = SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(2000, eventName: "Dodge", x: 1010) }, 150);
            Check(fast.Count == 1 && fast[0].Speed == 500 && fast[0].Distance == 1000 && fast[0].Seconds == 2, "Speed: impossible movement is reported");

            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(1000, eventName: "Teleport"), P(2000, eventName: "Dodge", x: 1010) }, 150).Count == 0, "Speed: teleport breaks the pair");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(500, eventName: "Dead"), P(2000, eventName: "Ping", x: 1010) }, 150).Count == 0, "Speed: death breaks the pair");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(300, eventName: "Dodge", x: 5000), P(3000, eventName: "Ping", x: 40) }, 150).Count == 0, "Speed: pairs under one second are not measured");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(1500, eventName: "Place", x: 5000), P(3000, eventName: "Ping", x: 40) }, 150).Count == 0, "Speed: target-position events are ignored");
            var zero = new ActionPoint(T0, "A", "Ragnar", "Ping", "", 0, 0, 0);
            Check(SpeedAnalyzer.Analyze(new[] { zero, P(2000, eventName: "Ping", x: 1000) }, 150).Count == 0, "Speed: zero positions are ignored");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, "A", "Ping"), P(2000, "B", "Ping", x: 5000) }, 150).Count == 0, "Speed: different players are never paired");
        }

        private static void Speed_QueryReadsDatabase()
        {
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("speed",
                Rec(Mod.ActivityEventType.Ping, 0, amount: 30),
                Rec(Mod.ActivityEventType.Dodge, 2000, x: 1010f)));
            ResultTable table = SpeedQuery.Run(db, QueryFilter.All, 150, default);
            Check(table.Rows.Count == 1 && (double?)table.Value(0, "Velocidade") == 500.0, "Speed query: one report at 500 u/s");
            Check((string?)table.Value(0, "De") == "10, 10" && (string?)table.Value(0, "Para") == "1010, 10", "Speed query: from and to positions (X, Z)");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile errors `The type or namespace name 'Analysis' does not exist in the namespace 'ActivityViewer.Core'`.

- [ ] **Step 3: Implement the analyzers**

`src/ActivityViewer.Core/Analysis/ActionPoint.cs`:

```csharp
namespace ActivityViewer.Core.Analysis
{
    public sealed class ActionPoint
    {
        public ActionPoint(long timeUtcMs, string platformId, string playerName, string eventName, string target, double x, double y, double z)
        {
            TimeUtcMs = timeUtcMs;
            PlatformId = platformId;
            PlayerName = playerName;
            Event = eventName;
            Target = target;
            X = x;
            Y = y;
            Z = z;
        }

        public long TimeUtcMs { get; }

        public string PlatformId { get; }

        public string PlayerName { get; }

        public string Event { get; }

        public string Target { get; }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }
}
```

`src/ActivityViewer.Core/Analysis/FrequencyAnalyzer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace ActivityViewer.Core.Analysis
{
    public sealed class FrequencyHit
    {
        public long StartUtcMs { get; set; }

        public long EndUtcMs { get; set; }

        public string PlatformId { get; set; } = "";

        public string PlayerName { get; set; } = "";

        public int MaxCount { get; set; }

        public SortedSet<string> Events { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public SortedSet<string> Objects { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    public static class FrequencyAnalyzer
    {
        public const long WindowMs = 1000;

        public static List<FrequencyHit> Analyze(IEnumerable<ActionPoint> points, int maxPerSecond)
        {
            var hits = new List<FrequencyHit>();
            var window = new Queue<ActionPoint>();
            string? player = null;
            FrequencyHit? open = null;

            foreach (ActionPoint point in points.OrderBy(p => p.PlatformId, StringComparer.Ordinal).ThenBy(p => p.TimeUtcMs))
            {
                if (point.PlatformId != player)
                {
                    player = point.PlatformId;
                    window.Clear();
                    open = null;
                }

                window.Enqueue(point);
                while (point.TimeUtcMs - window.Peek().TimeUtcMs >= WindowMs) window.Dequeue();
                if (window.Count <= maxPerSecond) continue;

                long start = window.Peek().TimeUtcMs;
                if (open == null || start > open.EndUtcMs)
                {
                    open = new FrequencyHit
                    {
                        StartUtcMs = start,
                        PlatformId = point.PlatformId,
                        PlayerName = point.PlayerName,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    };
                    hits.Add(open);
                }

                open.EndUtcMs = point.TimeUtcMs;
                open.MaxCount = Math.Max(open.MaxCount, window.Count);
                foreach (ActionPoint item in window)
                {
                    open.Events.Add(item.Event);
                    if (item.Target.Length > 0) open.Objects.Add(item.Target);
                }
            }

            hits.Sort((a, b) => a.StartUtcMs.CompareTo(b.StartUtcMs));
            return hits;
        }
    }
}
```

`src/ActivityViewer.Core/Analysis/SpeedAnalyzer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace ActivityViewer.Core.Analysis
{
    public sealed class SpeedHit
    {
        public long FromUtcMs { get; set; }

        public long ToUtcMs { get; set; }

        public string PlatformId { get; set; } = "";

        public string PlayerName { get; set; } = "";

        public double Speed { get; set; }

        public double Distance { get; set; }

        public double Seconds { get; set; }

        public double FromX { get; set; }

        public double FromZ { get; set; }

        public double ToX { get; set; }

        public double ToZ { get; set; }
    }

    public static class SpeedAnalyzer
    {
        public const long MinElapsedMs = 1000;

        public static readonly IReadOnlyCollection<string> PlayerPositionEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dodge", "Damaged", "Drop", "Consume", "Equip", "Unequip", "Craft", "Repair item", "Ping", "Inventory",
            "TrinketActivated", "Command", "Command remote", "Move", "MoveAll", "StackAll"
        };

        public static readonly IReadOnlyCollection<string> BarrierEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Teleport", "Spawned", "Dead", "Connected", "Disconnected"
        };

        public static List<SpeedHit> Analyze(IEnumerable<ActionPoint> points, double maxSpeed)
        {
            var hits = new List<SpeedHit>();
            string? player = null;
            ActionPoint? previous = null;

            foreach (ActionPoint point in points.OrderBy(p => p.PlatformId, StringComparer.Ordinal).ThenBy(p => p.TimeUtcMs))
            {
                if (point.PlatformId != player)
                {
                    player = point.PlatformId;
                    previous = null;
                }

                if (BarrierEvents.Contains(point.Event))
                {
                    previous = null;
                    continue;
                }

                if (!PlayerPositionEvents.Contains(point.Event)) continue;
                if (point.X == 0 && point.Y == 0 && point.Z == 0) continue;

                if (previous == null)
                {
                    previous = point;
                    continue;
                }

                long elapsed = point.TimeUtcMs - previous.TimeUtcMs;
                if (elapsed < MinElapsedMs) continue;

                double dx = point.X - previous.X;
                double dz = point.Z - previous.Z;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                double seconds = elapsed / 1000.0;
                double speed = distance / seconds;
                if (speed > maxSpeed)
                {
                    hits.Add(new SpeedHit
                    {
                        FromUtcMs = previous.TimeUtcMs,
                        ToUtcMs = point.TimeUtcMs,
                        PlatformId = point.PlatformId,
                        PlayerName = point.PlayerName,
                        Speed = Math.Round(speed, 1),
                        Distance = Math.Round(distance, 1),
                        Seconds = Math.Round(seconds, 1),
                        FromX = previous.X,
                        FromZ = previous.Z,
                        ToX = point.X,
                        ToZ = point.Z
                    });
                }

                previous = point;
            }

            hits.Sort((a, b) => a.ToUtcMs.CompareTo(b.ToUtcMs));
            return hits;
        }
    }
}
```

- [ ] **Step 4: Implement the queries**

`src/ActivityViewer.Core/Data/PointReader.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using ActivityViewer.Core.Analysis;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal static class PointReader
    {
        internal static List<ActionPoint> Read(ActivityDatabase database, QueryFilter filter, IEnumerable<string> events, CancellationToken token)
        {
            SqlBuilder sql = new SqlBuilder("SELECT e.time_utc, e.platform_id, e.player_name, e.event, e.target, e.x, e.y, e.z FROM events e")
                .WhereIn("e.event", events)
                .ApplyFilter(filter, "e")
                .Append("ORDER BY e.platform_id, e.time_utc, e.id");

            return database.Run(db =>
            {
                var points = new List<ActionPoint>();
                using SqliteStatement s = sql.Prepare(db);
                while (s.Step())
                {
                    if ((points.Count & 4095) == 0) token.ThrowIfCancellationRequested();
                    points.Add(new ActionPoint(s.ColumnInt64(0), s.ColumnText(1), s.ColumnText(2), s.ColumnText(3), s.ColumnText(4),
                        s.ColumnDouble(5), s.ColumnDouble(6), s.ColumnDouble(7)));
                }
                return points;
            });
        }
    }
}
```

`src/ActivityViewer.Core/Data/InteractionQuery.cs`:

```csharp
using System;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public enum InteractionResult
    {
        Any,
        Success,
        Failure
    }

    public sealed class InteractionCriteria
    {
        public string Object { get; init; } = "";

        public InteractionResult Result { get; init; } = InteractionResult.Any;
    }

    public static class InteractionQuery
    {
        public static readonly string[] Events = { "Interact", "Use", "Text" };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, InteractionCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("ID", typeof(string)),
                new ResultColumn("Evento", typeof(string)),
                new ResultColumn("Objeto", typeof(string)),
                new ResultColumn("Resultado", typeof(string)),
                new ResultColumn("Info", typeof(string)),
                new ResultColumn("Item usado", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, e.target, e.details, " +
                "(SELECT i.prefab FROM event_items i WHERE i.event_id = e.id LIMIT 1), e.x, e.y, e.z FROM events e");
            sql.WhereIn("e.event", Events);
            if (criteria.Object.Length > 0) sql.Where("instr(lower(e.target), lower(?)) > 0", criteria.Object);
            if (criteria.Result != InteractionResult.Any)
            {
                string prefix = criteria.Result == InteractionResult.Success ? "result:True" : "result:False";
                sql.Where("e.event = 'Interact' AND substr(e.details, 1, length(?)) = ?", prefix, prefix);
            }
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        public static (string Result, string Info) ParseDetails(string eventName, string details)
        {
            if (eventName == "Text") return ("", details);
            if (eventName != "Interact" || !details.StartsWith("result:", StringComparison.Ordinal)) return ("", "");

            int space = details.IndexOf(' ');
            string value = space < 0 ? details.Substring(7) : details.Substring(7, space - 7);
            string result = value == "True" ? "Sucesso" : value == "False" ? "Falha" : value;
            int info = details.IndexOf(" info:", StringComparison.Ordinal);
            return (result, info < 0 ? "" : details.Substring(info + 6));
        }

        private static object?[] Map(SqliteStatement s)
        {
            string eventName = s.ColumnText(3);
            (string result, string info) = ParseDetails(eventName, s.ColumnText(5));
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                eventName,
                s.ColumnText(4),
                result,
                info,
                s.ColumnIsNull(6) ? "" : s.ColumnText(6),
                s.ColumnDouble(7),
                s.ColumnDouble(8),
                s.ColumnDouble(9)
            };
        }
    }
}
```

`src/ActivityViewer.Core/Data/FrequencyQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ActivityViewer.Core.Analysis;

namespace ActivityViewer.Core.Data
{
    public static class FrequencyQuery
    {
        private const int MaxObjectsShown = 10;

        public static readonly IReadOnlyList<string> Events = new[] { "Pickup", "Place", "Remove", "Interact", "Drop", "Move" };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, int maxPerSecond, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("ID", typeof(string)),
                new ResultColumn("Ações", typeof(long)),
                new ResultColumn("Eventos", typeof(string)),
                new ResultColumn("Objetos", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            List<ActionPoint> points = PointReader.Read(database, filter, Events, token);
            token.ThrowIfCancellationRequested();
            foreach (FrequencyHit hit in FrequencyAnalyzer.Analyze(points, maxPerSecond))
            {
                if (table.Rows.Count >= filter.RowLimit)
                {
                    table.Truncated = true;
                    break;
                }

                string objects = string.Join(", ", hit.Objects.Take(MaxObjectsShown)) + (hit.Objects.Count > MaxObjectsShown ? ", ..." : "");
                table.Rows.Add(new object?[]
                {
                    TimeFormat.ToLocal(hit.StartUtcMs), hit.PlayerName, hit.PlatformId, (long)hit.MaxCount,
                    string.Join(", ", hit.Events), objects, hit.X, hit.Y, hit.Z
                });
            }

            table.Elapsed = watch.Elapsed;
            return table;
        }
    }
}
```

`src/ActivityViewer.Core/Data/SpeedQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using ActivityViewer.Core.Analysis;

namespace ActivityViewer.Core.Data
{
    public static class SpeedQuery
    {
        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, double maxSpeed, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("ID", typeof(string)),
                new ResultColumn("Velocidade", typeof(double)),
                new ResultColumn("Distância", typeof(double)),
                new ResultColumn("Segundos", typeof(double)),
                new ResultColumn("De", typeof(string)),
                new ResultColumn("Para", typeof(string)));

            IEnumerable<string> events = SpeedAnalyzer.PlayerPositionEvents.Concat(SpeedAnalyzer.BarrierEvents);
            List<ActionPoint> points = PointReader.Read(database, filter, events, token);
            token.ThrowIfCancellationRequested();
            foreach (SpeedHit hit in SpeedAnalyzer.Analyze(points, maxSpeed))
            {
                if (table.Rows.Count >= filter.RowLimit)
                {
                    table.Truncated = true;
                    break;
                }

                table.Rows.Add(new object?[]
                {
                    TimeFormat.ToLocal(hit.ToUtcMs), hit.PlayerName, hit.PlatformId, hit.Speed, hit.Distance, hit.Seconds,
                    Position(hit.FromX, hit.FromZ), Position(hit.ToX, hit.ToZ)
                });
            }

            table.Elapsed = watch.Elapsed;
            return table;
        }

        private static string Position(double x, double z) => string.Format(CultureInfo.InvariantCulture, "{0:0.#}, {1:0.#}", x, z);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `82 checks passed.` (55 + 9 interactions + 7 frequency + 1 frequency query + 8 speed + 2 speed query)

- [ ] **Step 6: Checkpoint**

Tests pass.

---
### Task 5: Export and error log

**Files:**
- Create: `src/ActivityViewer.Core/Export/ResultExporter.cs`, `src/ActivityViewer.Core/ErrorLog.cs`
- Create: `tests/ActivityViewer.Tests/Program.Export.cs`

**Interfaces:**
- Consumes: `ResultTable`, `TimeFormat` (Task 2), `AppPaths` (Task 1).
- Produces:
  - `public static class ResultExporter { static void Export(ResultTable table, string path); static void WriteCsv(ResultTable table, TextWriter writer); static void WriteText(ResultTable table, TextWriter writer); static string FormatValue(object? value); }`
  - `public static class ErrorLog { static string Write(Exception error, string context); }` (returns the log path, or "" if the log could not be written)

- [ ] **Step 1: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Export.cs`:

```csharp
using System;
using System.IO;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Export;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunExport()
        {
            Export_CsvEscapesAndUsesPtBr();
            Export_TextIsAligned();
            Export_FileHasUtf8Bom();
            ErrorLog_WritesEntry();
        }

        private static ResultTable ExportSample()
        {
            var table = new ResultTable(
                new ResultColumn("Data", typeof(DateTime)),
                new ResultColumn("Jogador", typeof(string)),
                new ResultColumn("Total", typeof(double)),
                new ResultColumn("Qtd", typeof(long)),
                new ResultColumn("Vida", typeof(double)));
            var when = new DateTime(2026, 10, 2, 13, 5, 9, DateTimeKind.Local);
            table.Rows.Add(new object?[] { when, "Þór; \"o\" 🙂\nlinha2", 35.5, 3L, null });
            table.Rows.Add(new object?[] { when, "Bjorn", 2.0, 10L, 1.25 });
            return table;
        }

        private static void Export_CsvEscapesAndUsesPtBr()
        {
            var writer = new StringWriter { NewLine = "\r\n" };
            ResultExporter.WriteCsv(ExportSample(), writer);
            string expected =
                "Data;Jogador;Total;Qtd;Vida\r\n" +
                "02/10/2026 13:05:09;\"Þór; \"\"o\"\" 🙂\nlinha2\";35,5;3;\r\n" +
                "02/10/2026 13:05:09;Bjorn;2;10;1,25\r\n";
            Check(writer.ToString() == expected, "Export: CSV quotes special text, uses ';' and decimal comma");
        }

        private static void Export_TextIsAligned()
        {
            var writer = new StringWriter { NewLine = "\n" };
            ResultExporter.WriteText(ExportSample(), writer);
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');
            Check(lines.Length == 4, "Export: TXT has header, separator and one line per row");
            Check(lines[2].Contains("linha2"), "Export: TXT keeps multi-line text on one line");
            int column = lines[0].IndexOf(" | ", StringComparison.Ordinal);
            Check(column > 0 && lines[2].IndexOf(" | ", StringComparison.Ordinal) == column && lines[3].IndexOf(" | ", StringComparison.Ordinal) == column, "Export: TXT columns aligned");
        }

        private static void Export_FileHasUtf8Bom()
        {
            string path = Path.Combine(TempDir("export"), "r.csv");
            ResultExporter.Export(ExportSample(), path);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "Export: file starts with UTF-8 BOM for Excel");
        }

        private static void ErrorLog_WritesEntry()
        {
            string path = ErrorLog.Write(new InvalidOperationException("falha de teste"), "Contexto do teste");
            Check(path.StartsWith(Path.Combine(AppPaths.LocalDataRoot, "logs"), StringComparison.OrdinalIgnoreCase) && File.Exists(path), "ErrorLog: file created under logs");
            string text = File.ReadAllText(path);
            Check(text.Contains("falha de teste") && text.Contains("Contexto do teste"), "ErrorLog: entry has message and context");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile errors `The type or namespace name 'Export' does not exist in the namespace 'ActivityViewer.Core'` and `The name 'ErrorLog' does not exist`.

- [ ] **Step 3: Implement export and error log**

`src/ActivityViewer.Core/Export/ResultExporter.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Core.Export
{
    public static class ResultExporter
    {
        private const int MaxTextWidth = 60;
        private const string TextSeparator = " | ";
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly char[] CsvSpecial = { ';', '"', '\n', '\r' };

        public static void Export(ResultTable table, string path)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) WriteCsv(table, writer);
            else WriteText(table, writer);
        }

        public static string FormatValue(object? value)
        {
            switch (value)
            {
                case null: return "";
                case DateTime date: return date.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture);
                case double number: return number.ToString("0.##", PtBr);
                case long integer: return integer.ToString(CultureInfo.InvariantCulture);
                default: return value.ToString() ?? "";
            }
        }

        public static void WriteCsv(ResultTable table, TextWriter writer)
        {
            writer.WriteLine(string.Join(";", table.Columns.Select(c => Escape(c.Name))));
            foreach (object?[] row in table.Rows)
                writer.WriteLine(string.Join(";", row.Select(v => Escape(FormatValue(v)))));
        }

        public static void WriteText(ResultTable table, TextWriter writer)
        {
            int count = table.Columns.Count;
            var widths = new int[count];
            for (int i = 0; i < count; i++) widths[i] = Math.Min(MaxTextWidth, table.Columns[i].Name.Length);
            foreach (object?[] row in table.Rows)
                for (int i = 0; i < count; i++) widths[i] = Math.Max(widths[i], Cell(row[i]).Length);

            writer.WriteLine(Line(table.Columns.Select(c => Clip(c.Name)).ToArray(), widths));
            writer.WriteLine(new string('-', widths.Sum() + TextSeparator.Length * (count - 1)));
            foreach (object?[] row in table.Rows)
                writer.WriteLine(Line(row.Select(Cell).ToArray(), widths));
        }

        private static string Escape(string value)
        {
            if (value.IndexOfAny(CsvSpecial) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Cell(object? value) => Clip(FormatValue(value).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' '));

        private static string Clip(string value) => value.Length <= MaxTextWidth ? value : value.Substring(0, MaxTextWidth - 3) + "...";

        private static string Line(string[] cells, int[] widths)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) builder.Append(TextSeparator);
                builder.Append(i == cells.Length - 1 ? cells[i] : cells[i].PadRight(widths[i]));
            }
            return builder.ToString();
        }
    }
}
```

`src/ActivityViewer.Core/ErrorLog.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ActivityViewer.Core
{
    public static class ErrorLog
    {
        private static readonly object Gate = new object();

        public static string Write(Exception error, string context)
        {
            try
            {
                string directory = Path.Combine(AppPaths.LocalDataRoot, "logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "viewer-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + context +
                    Environment.NewLine + error + Environment.NewLine + Environment.NewLine;
                lock (Gate) File.AppendAllText(path, entry, Encoding.UTF8);
                return path;
            }
            catch (IOException)
            {
                return "";
            }
            catch (UnauthorizedAccessException)
            {
                return "";
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `89 checks passed.` (82 + 7)

- [ ] **Step 5: Checkpoint**

Tests pass.

---

### Task 6: Profiles and password protection

**Files:**
- Modify: `src/ActivityViewer.Core/ActivityViewer.Core.csproj` (add the ProtectedData package)
- Create: `src/ActivityViewer.Core/Profiles/ServerProfile.cs`, `PasswordProtector.cs`, `ProfileStore.cs`, `ProfileJsonContext.cs`
- Create: `tests/ActivityViewer.Tests/Program.Profiles.cs`

**Interfaces:**
- Consumes: `AppPaths` (Task 1).
- Produces:
  - `public enum TransferProtocol { Ftp, Ftps, Sftp }`
  - `public sealed class ServerProfile { string Name; TransferProtocol Protocol; string Host; int Port; string User; string EncryptedPassword; string RemoteFolder; string TrustedCertificateThumbprint; string TrustedHostKeyFingerprint; static int DefaultPort(TransferProtocol); string? Validate(); ServerProfile Clone(); }`
  - `public static class PasswordProtector { static string Protect(string password); static bool TryUnprotect(string encrypted, out string password); }`
  - `public sealed class ProfileStore { ProfileStore(string filePath); static ProfileStore Default(); string FilePath; string? LastBackup; List<ServerProfile> Load(); void Save(IEnumerable<ServerProfile> profiles); }`

- [ ] **Step 1: Add the package**

In `src/ActivityViewer.Core/ActivityViewer.Core.csproj`, add before `</Project>`:

```xml
  <ItemGroup>
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.12" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Profiles.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using ActivityViewer.Core.Profiles;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunProfiles()
        {
            Password_RoundTrip();
            Password_ForeignBlobFails();
            Profiles_RoundTrip();
            Profiles_MissingFileIsEmpty();
            Profiles_CorruptFileIsBackedUp();
            Profiles_Validate();
        }

        private static void Password_RoundTrip()
        {
            string encrypted = PasswordProtector.Protect("s3nh@ çã 🙂");
            Check(PasswordProtector.TryUnprotect(encrypted, out string plain) && plain == "s3nh@ çã 🙂", "Password: round trip");
            Check(!encrypted.Contains("s3nh@"), "Password: encrypted text does not contain the password");
            Check(PasswordProtector.Protect("") == "" && !PasswordProtector.TryUnprotect("", out _), "Password: empty means no saved password");
        }

        private static void Password_ForeignBlobFails()
        {
            var random = new byte[64];
            new Random(7).NextBytes(random);
            Check(!PasswordProtector.TryUnprotect(Convert.ToBase64String(random), out _), "Password: blob from another PC/user fails");
            Check(!PasswordProtector.TryUnprotect("não é base64 !!", out _), "Password: invalid text fails");
        }

        private static void Profiles_RoundTrip()
        {
            var store = new ProfileStore(Path.Combine(TempDir("profiles"), "profiles.json"));
            var profiles = new List<ServerProfile>
            {
                new ServerProfile
                {
                    Name = "Vikings Brasil", Protocol = TransferProtocol.Sftp, Host = "sftp.exemplo.com", Port = 2222, User = "admin",
                    EncryptedPassword = PasswordProtector.Protect("segredo-123"), RemoteFolder = "/home/vh/SAVE/Vikings_ActivityLog",
                    TrustedHostKeyFingerprint = "SHA256:abc"
                },
                new ServerProfile { Name = "Teste", Protocol = TransferProtocol.Ftps, Host = "127.0.0.1", Port = 21, User = "u", RemoteFolder = "/", TrustedCertificateThumbprint = "AB12" }
            };
            store.Save(profiles);
            List<ServerProfile> loaded = store.Load();
            Check(loaded.Count == 2 && loaded[0].Name == "Vikings Brasil" && loaded[0].Protocol == TransferProtocol.Sftp && loaded[0].Port == 2222
                && loaded[0].RemoteFolder == "/home/vh/SAVE/Vikings_ActivityLog" && loaded[0].TrustedHostKeyFingerprint == "SHA256:abc"
                && loaded[1].Protocol == TransferProtocol.Ftps && loaded[1].TrustedCertificateThumbprint == "AB12", "Profiles: all fields round trip");
            string json = File.ReadAllText(store.FilePath);
            Check(!json.Contains("segredo-123"), "Profiles: JSON never contains the plain password");
            Check(json.Contains("\"Sftp\""), "Profiles: protocol stored as text");
        }

        private static void Profiles_MissingFileIsEmpty()
        {
            var store = new ProfileStore(Path.Combine(TempDir("profiles_missing"), "profiles.json"));
            Check(store.Load().Count == 0, "Profiles: missing file gives an empty list");
        }

        private static void Profiles_CorruptFileIsBackedUp()
        {
            string path = Path.Combine(TempDir("profiles_corrupt"), "profiles.json");
            File.WriteAllText(path, "{ isto não é json");
            var store = new ProfileStore(path);
            Check(store.Load().Count == 0 && !File.Exists(path), "Profiles: corrupt file gives an empty list and is moved away");
            Check(store.LastBackup != null && File.Exists(store.LastBackup), "Profiles: corrupt file is kept as a backup");
        }

        private static void Profiles_Validate()
        {
            var valid = new ServerProfile { Name = "A", Host = "h", Port = 21, User = "u", RemoteFolder = "/" };
            Check(valid.Validate() == null, "Profiles: valid profile has no error");
            ServerProfile noHost = valid.Clone();
            noHost.Host = " ";
            Check(noHost.Validate() == "Informe o host.", "Profiles: host required");
            ServerProfile badPort = valid.Clone();
            badPort.Port = 0;
            Check(badPort.Validate() == "Porta inválida (1 a 65535).", "Profiles: port range");
            Check(ServerProfile.DefaultPort(TransferProtocol.Sftp) == 22 && ServerProfile.DefaultPort(TransferProtocol.Ftps) == 21, "Profiles: default ports");
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile error `The type or namespace name 'Profiles' does not exist in the namespace 'ActivityViewer.Core'`.

- [ ] **Step 4: Implement profiles**

`src/ActivityViewer.Core/Profiles/ServerProfile.cs`:

```csharp
namespace ActivityViewer.Core.Profiles
{
    public enum TransferProtocol
    {
        Ftp,
        Ftps,
        Sftp
    }

    public sealed class ServerProfile
    {
        public string Name { get; set; } = "";

        public TransferProtocol Protocol { get; set; } = TransferProtocol.Ftp;

        public string Host { get; set; } = "";

        public int Port { get; set; } = 21;

        public string User { get; set; } = "";

        public string EncryptedPassword { get; set; } = "";

        public string RemoteFolder { get; set; } = "/";

        public string TrustedCertificateThumbprint { get; set; } = "";

        public string TrustedHostKeyFingerprint { get; set; } = "";

        public static int DefaultPort(TransferProtocol protocol) => protocol == TransferProtocol.Sftp ? 22 : 21;

        public string? Validate()
        {
            if (Name.Trim().Length == 0) return "Informe o nome do perfil.";
            if (Host.Trim().Length == 0) return "Informe o host.";
            if (Port < 1 || Port > 65535) return "Porta inválida (1 a 65535).";
            if (User.Trim().Length == 0) return "Informe o usuário.";
            if (RemoteFolder.Trim().Length == 0) return "Informe a pasta remota.";
            return null;
        }

        public ServerProfile Clone() => (ServerProfile)MemberwiseClone();

        public override string ToString() => Name;
    }
}
```

`src/ActivityViewer.Core/Profiles/PasswordProtector.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace ActivityViewer.Core.Profiles
{
    public static class PasswordProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Vikings_ActivityViewer.v1");

        public static string Protect(string password)
        {
            if (password.Length == 0) return "";
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static bool TryUnprotect(string encrypted, out string password)
        {
            password = "";
            if (encrypted.Length == 0) return false;
            try
            {
                byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
                password = Encoding.UTF8.GetString(plain);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
```

`src/ActivityViewer.Core/Profiles/ProfileJsonContext.cs`:

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ActivityViewer.Core.Profiles
{
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(List<ServerProfile>))]
    internal partial class ProfileJsonContext : JsonSerializerContext
    {
    }
}
```

`src/ActivityViewer.Core/Profiles/ProfileStore.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ActivityViewer.Core.Profiles
{
    public sealed class ProfileStore
    {
        public ProfileStore(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public string? LastBackup { get; private set; }

        public static ProfileStore Default() => new ProfileStore(Path.Combine(AppPaths.RoamingRoot, "profiles.json"));

        public List<ServerProfile> Load()
        {
            LastBackup = null;
            if (!File.Exists(FilePath)) return new List<ServerProfile>();

            try
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ListServerProfile) ?? new List<ServerProfile>();
            }
            catch (JsonException)
            {
                string backup = FilePath + ".corrompido-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                File.Move(FilePath, backup, true);
                LastBackup = backup;
                return new List<ServerProfile>();
            }
        }

        public void Save(IEnumerable<ServerProfile> profiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            string json = JsonSerializer.Serialize(new List<ServerProfile>(profiles), ProfileJsonContext.Default.ListServerProfile);
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, FilePath, true);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `104 checks passed.` (89 + 15)

- [ ] **Step 6: Checkpoint**

Tests pass.

---

### Task 7: Remote folders (FTP, FTPS, SFTP)

**Files:**
- Modify: `src/ActivityViewer.Core/ActivityViewer.Core.csproj` (add FluentFTP and SSH.NET)
- Create: `src/ActivityViewer.Core/Transfer/IRemoteFolder.cs`, `ProgressStream.cs`, `FtpRemoteFolder.cs`, `SftpRemoteFolder.cs`, `RemoteFolderFactory.cs`
- Create: `tests/ActivityViewer.Tests/Program.Transfer.cs`

**Interfaces:**
- Consumes: `ServerProfile`, `TransferProtocol` (Task 6).
- Produces:
  - `public sealed class RemoteFileInfo { RemoteFileInfo(string name, long size, DateTime modified); string Name; long Size; DateTime Modified; }`
  - `public interface ITrustPrompt { bool TrustCertificate(string host, string thumbprint, string subject, bool changed); bool TrustHostKey(string host, string fingerprint); }`
  - `public interface IRemoteFolder : IDisposable { bool ProfileChanged { get; } Task ConnectAsync(CancellationToken token); Task<IReadOnlyList<RemoteFileInfo>> ListDatabasesAsync(CancellationToken token); Task<RemoteFileInfo?> StatAsync(string fileName, CancellationToken token); Task DownloadAsync(string fileName, string localPath, IProgress<long>? progress, CancellationToken token); }`
  - `public static class RemotePath { static string Combine(string folder, string name); }`
  - `public sealed class InlineProgress<T> : IProgress<T> { InlineProgress(Action<T> report); }`
  - `internal sealed class ProgressStream : Stream { ProgressStream(Stream inner, IProgress<long>? progress); }`
  - `public sealed class HostKeyChangedException : Exception`
  - `public static class RemoteFolderFactory { static IRemoteFolder Create(ServerProfile profile, string password, ITrustPrompt prompt); }`
  - `FtpRemoteFolder` and `SftpRemoteFolder` update the profile's trusted thumbprint/fingerprint when the user accepts, and set `ProfileChanged = true`; the caller saves the profiles.

- [ ] **Step 1: Add the packages**

In `src/ActivityViewer.Core/ActivityViewer.Core.csproj`, add inside the `PackageReference` item group:

```xml
    <PackageReference Include="FluentFTP" Version="55.0.0" />
    <PackageReference Include="SSH.NET" Version="2026.0.0" />
```

- [ ] **Step 2: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Transfer.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Profiles;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private sealed class DenyAllPrompt : ITrustPrompt
        {
            public bool TrustCertificate(string host, string thumbprint, string subject, bool changed) => false;

            public bool TrustHostKey(string host, string fingerprint) => false;
        }

        static partial void RunTransfer()
        {
            Transfer_RemotePathCombine();
            Transfer_FactoryPicksImplementation();
            Transfer_ProgressStreamReportsTotals();
            Transfer_ConnectionRefusedRaisesError(TransferProtocol.Ftp);
            Transfer_ConnectionRefusedRaisesError(TransferProtocol.Sftp);
        }

        private static void Transfer_RemotePathCombine()
        {
            Check(RemotePath.Combine("/SAVE/Vikings_ActivityLog/", "a.db") == "/SAVE/Vikings_ActivityLog/a.db", "RemotePath: trailing slash");
            Check(RemotePath.Combine("", "a.db") == "a.db", "RemotePath: empty folder");
            Check(RemotePath.Combine("/", "a.db") == "/a.db", "RemotePath: root folder");
            Check(RemotePath.Combine("\\x\\y", "a.db") == "/x/y/a.db", "RemotePath: backslashes converted");
        }

        private static void Transfer_FactoryPicksImplementation()
        {
            var profile = new ServerProfile { Name = "t", Host = "127.0.0.1", User = "u", RemoteFolder = "/" };
            profile.Protocol = TransferProtocol.Sftp;
            using (IRemoteFolder sftp = RemoteFolderFactory.Create(profile, "p", new DenyAllPrompt())) Check(sftp is SftpRemoteFolder, "Factory: SFTP");
            profile.Protocol = TransferProtocol.Ftps;
            using (IRemoteFolder ftps = RemoteFolderFactory.Create(profile, "p", new DenyAllPrompt())) Check(ftps is FtpRemoteFolder, "Factory: FTPS uses the FTP client");
            profile.Protocol = TransferProtocol.Ftp;
            using (IRemoteFolder ftp = RemoteFolderFactory.Create(profile, "p", new DenyAllPrompt())) Check(ftp is FtpRemoteFolder, "Factory: FTP");
        }

        private static void Transfer_ProgressStreamReportsTotals()
        {
            long last = 0;
            int reports = 0;
            using var inner = new MemoryStream();
            using (var stream = new ProgressStream(inner, new InlineProgress<long>(value => { last = value; reports++; })))
            {
                var chunk = new byte[10];
                stream.Write(chunk, 0, 10);
                stream.Write(chunk, 0, 10);
                stream.WriteAsync(chunk, 0, 10).GetAwaiter().GetResult();
                Check(inner.Length == 30, "ProgressStream: writes pass through");
            }
            Check(last == 30 && reports == 3, "ProgressStream: reports cumulative bytes");
        }

        private static void Transfer_ConnectionRefusedRaisesError(TransferProtocol protocol)
        {
            var profile = new ServerProfile { Name = "t", Protocol = protocol, Host = "127.0.0.1", Port = 1, User = "u", RemoteFolder = "/" };
            bool failed = false;
            using IRemoteFolder remote = RemoteFolderFactory.Create(profile, "p", new DenyAllPrompt());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { Task.Run(() => remote.ConnectAsync(timeout.Token)).GetAwaiter().GetResult(); }
            catch (Exception error) when (!(error is OperationCanceledException)) { failed = true; }
            Check(failed, "Transfer: refused " + protocol + " connection raises an error");
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile error `The type or namespace name 'Transfer' does not exist in the namespace 'ActivityViewer.Core'`.

- [ ] **Step 4: Implement the transfer layer**

`src/ActivityViewer.Core/Transfer/IRemoteFolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActivityViewer.Core.Transfer
{
    public sealed class RemoteFileInfo
    {
        public RemoteFileInfo(string name, long size, DateTime modified)
        {
            Name = name;
            Size = size;
            Modified = modified;
        }

        public string Name { get; }

        public long Size { get; }

        public DateTime Modified { get; }
    }

    public interface ITrustPrompt
    {
        bool TrustCertificate(string host, string thumbprint, string subject, bool changed);

        bool TrustHostKey(string host, string fingerprint);
    }

    public interface IRemoteFolder : IDisposable
    {
        bool ProfileChanged { get; }

        Task ConnectAsync(CancellationToken token);

        Task<IReadOnlyList<RemoteFileInfo>> ListDatabasesAsync(CancellationToken token);

        Task<RemoteFileInfo?> StatAsync(string fileName, CancellationToken token);

        Task DownloadAsync(string fileName, string localPath, IProgress<long>? progress, CancellationToken token);
    }

    public sealed class HostKeyChangedException : Exception
    {
        public HostKeyChangedException(string message) : base(message) { }
    }

    public static class RemotePath
    {
        public static string Combine(string folder, string name)
        {
            string normalized = folder.Trim().Replace('\\', '/');
            if (normalized.Length == 0) return name;
            return normalized.TrimEnd('/') + "/" + name;
        }
    }

    public sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }
}
```

`src/ActivityViewer.Core/Transfer/ProgressStream.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ActivityViewer.Core.Transfer
{
    internal sealed class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly IProgress<long>? _progress;
        private long _written;

        internal ProgressStream(Stream inner, IProgress<long>? progress)
        {
            _inner = inner;
            _progress = progress;
        }

        public override bool CanRead => false;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => true;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Advance(count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
            Advance(buffer.Length);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            Advance(count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            Advance(buffer.Length);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }

        private void Advance(int count)
        {
            _written += count;
            _progress?.Report(_written);
        }
    }
}
```

`src/ActivityViewer.Core/Transfer/FtpRemoteFolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Profiles;
using FluentFTP;
using FluentFTP.Exceptions;

namespace ActivityViewer.Core.Transfer
{
    public sealed class FtpRemoteFolder : IRemoteFolder
    {
        private readonly ServerProfile _profile;
        private readonly ITrustPrompt _prompt;
        private readonly AsyncFtpClient _client;

        public FtpRemoteFolder(ServerProfile profile, string password, ITrustPrompt prompt)
        {
            _profile = profile;
            _prompt = prompt;
            _client = new AsyncFtpClient(profile.Host, profile.User, password, profile.Port);
            _client.Config.EncryptionMode = profile.Protocol == TransferProtocol.Ftps ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
            _client.Config.ConnectTimeout = 15000;
            _client.Config.ReadTimeout = 30000;
            _client.Config.DataConnectionReadTimeout = 30000;
            _client.ValidateCertificate += (control, e) => e.Accept = Validate(e.PolicyErrors, e.Certificate);
        }

        public bool ProfileChanged { get; private set; }

        public Task ConnectAsync(CancellationToken token) => _client.Connect(token);

        public async Task<IReadOnlyList<RemoteFileInfo>> ListDatabasesAsync(CancellationToken token)
        {
            FtpListItem[] items = await _client.GetListing(_profile.RemoteFolder, token).ConfigureAwait(false);
            return items
                .Where(i => i.Type == FtpObjectType.File && i.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(i => new RemoteFileInfo(i.Name, i.Size, i.Modified))
                .ToList();
        }

        public async Task<RemoteFileInfo?> StatAsync(string fileName, CancellationToken token)
        {
            string path = RemotePath.Combine(_profile.RemoteFolder, fileName);
            if (!await _client.FileExists(path, token).ConfigureAwait(false)) return null;
            long size = await _client.GetFileSize(path, -1, token).ConfigureAwait(false);
            DateTime modified;
            try
            {
                modified = await _client.GetModifiedTime(path, token).ConfigureAwait(false);
            }
            catch (FtpCommandException)
            {
                modified = DateTime.MinValue;
            }
            return new RemoteFileInfo(fileName, size, modified);
        }

        public async Task DownloadAsync(string fileName, string localPath, IProgress<long>? progress, CancellationToken token)
        {
            string path = RemotePath.Combine(_profile.RemoteFolder, fileName);
            IProgress<FtpProgress>? adapter = progress == null ? null : new InlineProgress<FtpProgress>(p => progress.Report(p.TransferredBytes));
            FtpStatus status = await _client.DownloadFile(localPath, path, FtpLocalExists.Overwrite, FtpVerify.None, adapter, token).ConfigureAwait(false);
            if (status != FtpStatus.Success) throw new IOException("Falha ao baixar " + path + ".");
        }

        public void Dispose() => _client.Dispose();

        private bool Validate(SslPolicyErrors errors, X509Certificate certificate)
        {
            if (errors == SslPolicyErrors.None) return true;

            string thumbprint = certificate.GetCertHashString();
            if (string.Equals(thumbprint, _profile.TrustedCertificateThumbprint, StringComparison.OrdinalIgnoreCase)) return true;

            bool changed = _profile.TrustedCertificateThumbprint.Length > 0;
            if (!_prompt.TrustCertificate(_profile.Host, thumbprint, certificate.Subject, changed)) return false;

            _profile.TrustedCertificateThumbprint = thumbprint;
            ProfileChanged = true;
            return true;
        }
    }
}
```

`src/ActivityViewer.Core/Transfer/SftpRemoteFolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Profiles;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace ActivityViewer.Core.Transfer
{
    public sealed class SftpRemoteFolder : IRemoteFolder
    {
        private readonly ServerProfile _profile;
        private readonly ITrustPrompt _prompt;
        private readonly SftpClient _client;
        private bool _hostKeyChanged;

        public SftpRemoteFolder(ServerProfile profile, string password, ITrustPrompt prompt)
        {
            _profile = profile;
            _prompt = prompt;
            _client = new SftpClient(profile.Host, profile.Port, profile.User, password);
            _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
            _client.OperationTimeout = TimeSpan.FromSeconds(30);
            _client.HostKeyReceived += OnHostKeyReceived;
        }

        public bool ProfileChanged { get; private set; }

        public async Task ConnectAsync(CancellationToken token)
        {
            try
            {
                await _client.ConnectAsync(token).ConfigureAwait(false);
            }
            catch (SshConnectionException) when (_hostKeyChanged)
            {
                throw new HostKeyChangedException(
                    "A chave do servidor SFTP mudou desde a última conexão. Isso pode indicar um ataque. " +
                    "Se a mudança for esperada (servidor reinstalado), limpe a chave confiável no perfil.");
            }
        }

        public async Task<IReadOnlyList<RemoteFileInfo>> ListDatabasesAsync(CancellationToken token)
        {
            var files = new List<RemoteFileInfo>();
            await foreach (ISftpFile file in _client.ListDirectoryAsync(_profile.RemoteFolder, token).ConfigureAwait(false))
            {
                if (file.IsRegularFile && file.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    files.Add(new RemoteFileInfo(file.Name, file.Length, file.LastWriteTimeUtc));
            }
            files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return files;
        }

        public Task<RemoteFileInfo?> StatAsync(string fileName, CancellationToken token)
        {
            string path = RemotePath.Combine(_profile.RemoteFolder, fileName);
            return Task.Run<RemoteFileInfo?>(() =>
            {
                if (!_client.Exists(path)) return null;
                SftpFileAttributes attributes = _client.GetAttributes(path);
                return new RemoteFileInfo(fileName, attributes.Size, attributes.LastWriteTimeUtc);
            }, token);
        }

        public async Task DownloadAsync(string fileName, string localPath, IProgress<long>? progress, CancellationToken token)
        {
            string path = RemotePath.Combine(_profile.RemoteFolder, fileName);
            using var stream = new ProgressStream(File.Create(localPath), progress);
            await _client.DownloadFileAsync(path, stream, token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_client.IsConnected) _client.Disconnect();
            _client.Dispose();
        }

        private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
        {
            string fingerprint = e.FingerPrintSHA256;
            if (_profile.TrustedHostKeyFingerprint.Length > 0)
            {
                bool same = string.Equals(fingerprint, _profile.TrustedHostKeyFingerprint, StringComparison.Ordinal);
                _hostKeyChanged = !same;
                e.CanTrust = same;
                return;
            }

            bool trusted = _prompt.TrustHostKey(_profile.Host, fingerprint);
            if (trusted)
            {
                _profile.TrustedHostKeyFingerprint = fingerprint;
                ProfileChanged = true;
            }
            e.CanTrust = trusted;
        }
    }
}
```

`src/ActivityViewer.Core/Transfer/RemoteFolderFactory.cs`:

```csharp
using ActivityViewer.Core.Profiles;

namespace ActivityViewer.Core.Transfer
{
    public static class RemoteFolderFactory
    {
        public static IRemoteFolder Create(ServerProfile profile, string password, ITrustPrompt prompt)
        {
            return profile.Protocol == TransferProtocol.Sftp
                ? new SftpRemoteFolder(profile, password, prompt)
                : new FtpRemoteFolder(profile, password, prompt);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `115 checks passed.` (104 + 4 path + 3 factory + 2 progress + 2 refused)

- [ ] **Step 6: Checkpoint**

Tests pass. Build shows no warnings from the Core project.

---

### Task 8: Local cache and safe download

**Files:**
- Create: `src/ActivityViewer.Core/Cache/CachePaths.cs`, `CachedCopy.cs`, `SafeDownloader.cs`
- Create: `tests/ActivityViewer.Tests/Program.Download.cs`

**Interfaces:**
- Consumes: `IRemoteFolder`, `RemoteFileInfo`, `InlineProgress<T>` (Task 7); `ActivityDatabase` (Task 2); `TimeFormat` (Task 2); `AppPaths` (Task 1).
- Produces:
  - `public static class CachePaths { static string SafeName(string value); static string ProfileFolder(string profileName); static string WorldFolder(string profileName, string worldFile); static IReadOnlyList<string> CachedWorlds(string profileName); }`
  - `public sealed class CachedCopy { string WorldFile; string Folder; DateTime DownloadedLocal; long DatabaseSize; long WalSize; string DatabasePathIn(string worldFolder); string Describe(); static CachedCopy? Load(string worldFolder); void Save(string worldFolder); }`
  - `public sealed class DownloadProgress { int Attempt; long Received; long Total; }`
  - `public sealed class DownloadResult { bool Success; CachedCopy? Copy; string Message; int Attempts; }` — on failure `Copy` is the previous copy (or null).
  - `public sealed class SafeDownloader { const int MaxAttempts = 3; SafeDownloader(); SafeDownloader(Func<string, string> quickCheck); Task<DownloadResult> DownloadAsync(IRemoteFolder remote, string worldFile, string worldFolder, IProgress<DownloadProgress>? progress, CancellationToken token); static void CleanupOldCopies(string worldFolder, string keepFolder); static string DefaultQuickCheck(string path); }`

- [ ] **Step 1: Write the failing tests**

`tests/ActivityViewer.Tests/Program.Download.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Cache;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Transfer;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private sealed class FakeRemote : IRemoteFolder
        {
            internal readonly Dictionary<string, (byte[] Data, DateTime Modified)> Files = new Dictionary<string, (byte[] Data, DateTime Modified)>();
            internal Action<string, int>? OnDownloaded;
            internal int Downloads;
            internal byte[] CheckpointedDb = Array.Empty<byte>();

            public bool ProfileChanged => false;

            public Task ConnectAsync(CancellationToken token) => Task.CompletedTask;

            public Task<IReadOnlyList<RemoteFileInfo>> ListDatabasesAsync(CancellationToken token)
            {
                IReadOnlyList<RemoteFileInfo> list = Files.Where(f => f.Key.EndsWith(".db")).Select(f => new RemoteFileInfo(f.Key, f.Value.Data.Length, f.Value.Modified)).ToList();
                return Task.FromResult(list);
            }

            public Task<RemoteFileInfo?> StatAsync(string fileName, CancellationToken token)
            {
                RemoteFileInfo? info = Files.TryGetValue(fileName, out var file) ? new RemoteFileInfo(fileName, file.Data.Length, file.Modified) : null;
                return Task.FromResult(info);
            }

            public Task DownloadAsync(string fileName, string localPath, IProgress<long>? progress, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!Files.TryGetValue(fileName, out var file)) throw new IOException("Arquivo não existe no servidor: " + fileName);
                File.WriteAllBytes(localPath, file.Data);
                progress?.Report(file.Data.Length);
                Downloads++;
                OnDownloaded?.Invoke(fileName, Downloads);
                return Task.CompletedTask;
            }

            public void Dispose() { }
        }

        static partial void RunDownload()
        {
            Download_Succeeds();
            Download_RetriesWhenDatabaseChanges();
            Download_WalVanishesThenRetries();
            Download_KeepsPreviousCopyAfterThreeFailures();
            Download_QuickCheckFailureRetries();
            Download_MissingWorld();
            Download_Cancelled();
            Cache_NamesAndWorldList();
        }

        private static FakeRemote LiveRemote()
        {
            string dir = TempDir("live_source");
            string path = Path.Combine(dir, "world.db");
            var remote = new FakeRemote();
            DateTime modified = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
            using (var store = new Mod.ActivityStore(path))
            {
                store.WriteBatch(new List<Mod.ActivityRecord> { Rec(Mod.ActivityEventType.Ping, 0, amount: 25) });
                remote.Files["world.db"] = (ReadShared(path), modified);
                remote.Files["world.db-wal"] = (ReadShared(path + "-wal"), modified);
            }
            remote.CheckpointedDb = File.ReadAllBytes(path);
            return remote;
        }

        private static byte[] ReadShared(string path)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            input.CopyTo(memory);
            return memory.ToArray();
        }

        private static string FreshWorldFolder(string profile)
        {
            string folder = CachePaths.WorldFolder(profile, "world.db");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            return folder;
        }

        private static int CopyFolders(string worldFolder) =>
            Directory.Exists(worldFolder) ? Directory.GetDirectories(worldFolder, "copy-*").Length : 0;

        private static DownloadResult Download(FakeRemote remote, string worldFolder, SafeDownloader? downloader = null, CancellationToken token = default)
        {
            return (downloader ?? new SafeDownloader()).DownloadAsync(remote, "world.db", worldFolder, null, token).GetAwaiter().GetResult();
        }

        private static void Download_Succeeds()
        {
            string folder = FreshWorldFolder("Servidor 1");
            DownloadResult result = Download(LiveRemote(), folder);
            Check(result.Success && result.Attempts == 1 && result.Copy != null, "Download: succeeds on the first attempt");
            using (ActivityDatabase db = ActivityDatabase.Open(result.Copy!.DatabasePathIn(folder)))
                Check(db.Players().Count == 1, "Download: copy contains rows that were only in the WAL");
            Check(CachedCopy.Load(folder)?.Folder == result.Copy.Folder, "Download: current.json points to the new copy");
            Check(result.Copy.WalSize > 0, "Download: WAL size recorded");
        }

        private static void Download_RetriesWhenDatabaseChanges()
        {
            string folder = FreshWorldFolder("Servidor 2");
            FakeRemote remote = LiveRemote();
            bool changed = false;
            remote.OnDownloaded = (name, count) =>
            {
                if (name != "world.db" || changed) return;
                changed = true;
                var file = remote.Files["world.db"];
                remote.Files["world.db"] = (file.Data, file.Modified.AddSeconds(1));
            };
            DownloadResult result = Download(remote, folder);
            Check(result.Success && result.Attempts == 2, "Download: a checkpoint during the download causes one retry");
            Check(CopyFolders(folder) == 1, "Download: failed attempt leaves no copy folder");
        }

        private static void Download_WalVanishesThenRetries()
        {
            string folder = FreshWorldFolder("Servidor 3");
            FakeRemote remote = LiveRemote();
            bool vanished = false;
            remote.OnDownloaded = (name, count) =>
            {
                if (name != "world.db" || vanished) return;
                vanished = true;
                remote.Files.Remove("world.db-wal");
                var file = remote.Files["world.db"];
                remote.Files["world.db"] = (remote.CheckpointedDb, file.Modified.AddSeconds(1));
            };
            DownloadResult result = Download(remote, folder);
            Check(result.Success && result.Attempts == 2, "Download: WAL disappearing mid-download is retried");
            Check(result.Copy != null && result.Copy.WalSize == 0, "Download: second attempt has no WAL");
        }

        private static void Download_KeepsPreviousCopyAfterThreeFailures()
        {
            string folder = FreshWorldFolder("Servidor 4");
            FakeRemote remote = LiveRemote();
            DownloadResult first = Download(remote, folder);
            remote.OnDownloaded = (name, count) =>
            {
                if (name != "world.db") return;
                var file = remote.Files["world.db"];
                remote.Files["world.db"] = (file.Data, file.Modified.AddSeconds(1));
            };
            DownloadResult second = Download(remote, folder);
            Check(!second.Success && second.Attempts == 3, "Download: gives up after three attempts");
            Check(second.Message.StartsWith("Não foi possível obter uma cópia consistente após 3 tentativas. Usando a cópia de "), "Download: failure message names the previous copy");
            Check(second.Copy != null && second.Copy.Folder == first.Copy!.Folder && CachedCopy.Load(folder)?.Folder == first.Copy.Folder, "Download: previous copy stays current");
            Check(CopyFolders(folder) == 1, "Download: failed attempts leave only the previous copy");
        }

        private static void Download_QuickCheckFailureRetries()
        {
            string folder = FreshWorldFolder("Servidor 5");
            int calls = 0;
            var downloader = new SafeDownloader(path => ++calls <= 2 ? "*** página corrompida" : "ok");
            DownloadResult result = Download(LiveRemote(), folder, downloader);
            Check(result.Success && result.Attempts == 3, "Download: failed integrity check is retried");
        }

        private static void Download_MissingWorld()
        {
            string folder = FreshWorldFolder("Servidor 6");
            DownloadResult result = Download(new FakeRemote(), folder);
            Check(!result.Success && result.Attempts == 1 && result.Message.Contains("não encontrado"), "Download: missing world fails without retrying");
        }

        private static void Download_Cancelled()
        {
            string folder = FreshWorldFolder("Servidor 7");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            bool cancelled = false;
            try { Download(LiveRemote(), folder, null, cancel.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Download: cancellation is reported");
            Check(CopyFolders(folder) == 0, "Download: cancellation leaves no copy folder");
        }

        private static void Cache_NamesAndWorldList()
        {
            Check(CachePaths.SafeName("a/b:c") == "a_b_c" && CachePaths.SafeName("  ") == "_", "Cache: unsafe names are sanitized");
            IReadOnlyList<string> worlds = CachePaths.CachedWorlds("Servidor 1");
            Check(worlds.Count == 1 && worlds[0] == "world.db", "Cache: cached worlds listed for a profile");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile error `The type or namespace name 'Cache' does not exist in the namespace 'ActivityViewer.Core'`.

- [ ] **Step 3: Implement the cache**

`src/ActivityViewer.Core/Cache/CachePaths.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ActivityViewer.Core.Cache
{
    public static class CachePaths
    {
        public static string SafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder();
            foreach (char c in value.Trim()) builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return builder.Length == 0 ? "_" : builder.ToString();
        }

        public static string ProfileFolder(string profileName) => Path.Combine(AppPaths.LocalDataRoot, "cache", SafeName(profileName));

        public static string WorldFolder(string profileName, string worldFile) =>
            Path.Combine(ProfileFolder(profileName), SafeName(Path.GetFileNameWithoutExtension(worldFile)));

        public static IReadOnlyList<string> CachedWorlds(string profileName)
        {
            var worlds = new List<string>();
            string folder = ProfileFolder(profileName);
            if (!Directory.Exists(folder)) return worlds;

            foreach (string worldFolder in Directory.GetDirectories(folder))
            {
                CachedCopy? copy = CachedCopy.Load(worldFolder);
                if (copy != null) worlds.Add(copy.WorldFile);
            }
            worlds.Sort(StringComparer.OrdinalIgnoreCase);
            return worlds;
        }
    }
}
```

`src/ActivityViewer.Core/Cache/CachedCopy.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Core.Cache
{
    public sealed class CachedCopy
    {
        private const string FileName = "current.json";

        public string WorldFile { get; set; } = "";

        public string Folder { get; set; } = "";

        public DateTime DownloadedLocal { get; set; }

        public long DatabaseSize { get; set; }

        public long WalSize { get; set; }

        public string DatabasePathIn(string worldFolder) => Path.Combine(worldFolder, Folder, WorldFile);

        public string Describe()
        {
            double megabytes = (DatabaseSize + WalSize) / (1024.0 * 1024.0);
            return "Cópia de " + DownloadedLocal.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture) + " (" +
                megabytes.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " MB)";
        }

        public static CachedCopy? Load(string worldFolder)
        {
            string path = Path.Combine(worldFolder, FileName);
            if (!File.Exists(path)) return null;
            try
            {
                CachedCopy? copy = JsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8), CacheJsonContext.Default.CachedCopy);
                if (copy == null || copy.Folder.Length == 0 || copy.WorldFile.Length == 0) return null;
                return File.Exists(copy.DatabasePathIn(worldFolder)) ? copy : null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Save(string worldFolder)
        {
            Directory.CreateDirectory(worldFolder);
            string path = Path.Combine(worldFolder, FileName);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, CacheJsonContext.Default.CachedCopy), new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(CachedCopy))]
    internal partial class CacheJsonContext : JsonSerializerContext
    {
    }
}
```

`src/ActivityViewer.Core/Cache/SafeDownloader.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer.Core.Cache
{
    public sealed class DownloadProgress
    {
        public DownloadProgress(int attempt, long received, long total)
        {
            Attempt = attempt;
            Received = received;
            Total = total;
        }

        public int Attempt { get; }

        public long Received { get; }

        public long Total { get; }
    }

    public sealed class DownloadResult
    {
        public DownloadResult(bool success, CachedCopy? copy, string message, int attempts)
        {
            Success = success;
            Copy = copy;
            Message = message;
            Attempts = attempts;
        }

        public bool Success { get; }

        public CachedCopy? Copy { get; }

        public string Message { get; }

        public int Attempts { get; }
    }

    public sealed class SafeDownloader
    {
        public const int MaxAttempts = 3;

        private readonly Func<string, string> _quickCheck;

        public SafeDownloader() : this(DefaultQuickCheck) { }

        public SafeDownloader(Func<string, string> quickCheck)
        {
            _quickCheck = quickCheck;
        }

        public static string DefaultQuickCheck(string path)
        {
            try
            {
                using ActivityDatabase database = ActivityDatabase.Open(path);
                return database.QuickCheck();
            }
            catch (InvalidActivityDatabaseException error)
            {
                return error.Message;
            }
        }

        public async Task<DownloadResult> DownloadAsync(IRemoteFolder remote, string worldFile, string worldFolder,
            IProgress<DownloadProgress>? progress, CancellationToken token)
        {
            string walFile = worldFile + "-wal";
            string lastProblem = "";

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                string copyName = "copy-" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + attempt;
                string copyFolder = Path.Combine(worldFolder, copyName);
                bool promoted = false;
                try
                {
                    RemoteFileInfo? before = await remote.StatAsync(worldFile, token).ConfigureAwait(false);
                    if (before == null) return new DownloadResult(false, CachedCopy.Load(worldFolder), "Mundo não encontrado no servidor: " + worldFile + ".", attempt);
                    RemoteFileInfo? walBefore = await remote.StatAsync(walFile, token).ConfigureAwait(false);
                    long total = before.Size + (walBefore?.Size ?? 0);

                    Directory.CreateDirectory(copyFolder);
                    string localDb = Path.Combine(copyFolder, worldFile);
                    await remote.DownloadAsync(worldFile, localDb, Relay(progress, attempt, 0, total), token).ConfigureAwait(false);

                    long walSize = 0;
                    if (walBefore != null)
                    {
                        try
                        {
                            await remote.DownloadAsync(walFile, localDb + "-wal", Relay(progress, attempt, before.Size, total), token).ConfigureAwait(false);
                            walSize = new FileInfo(localDb + "-wal").Length;
                        }
                        catch (Exception error) when (!(error is OperationCanceledException))
                        {
                            lastProblem = "O arquivo -wal mudou durante o download (" + error.Message + ").";
                            continue;
                        }
                    }

                    RemoteFileInfo? after = await remote.StatAsync(worldFile, token).ConfigureAwait(false);
                    if (after == null || after.Size != before.Size || after.Modified != before.Modified)
                    {
                        lastProblem = "O banco mudou durante o download.";
                        continue;
                    }

                    string check = _quickCheck(localDb);
                    if (check != "ok")
                    {
                        lastProblem = "Verificação de integridade falhou: " + check;
                        continue;
                    }

                    var copy = new CachedCopy
                    {
                        WorldFile = worldFile,
                        Folder = copyName,
                        DownloadedLocal = DateTime.Now,
                        DatabaseSize = new FileInfo(localDb).Length,
                        WalSize = walSize
                    };
                    copy.Save(worldFolder);
                    promoted = true;
                    CleanupOldCopies(worldFolder, copyName);
                    return new DownloadResult(true, copy, "Cópia baixada com sucesso.", attempt);
                }
                finally
                {
                    if (!promoted) TryDelete(copyFolder);
                }
            }

            CachedCopy? previous = CachedCopy.Load(worldFolder);
            string fallback = previous != null
                ? "Usando a cópia de " + previous.DownloadedLocal.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture) + "."
                : "Nenhuma cópia anterior disponível.";
            return new DownloadResult(false, previous,
                "Não foi possível obter uma cópia consistente após 3 tentativas. " + fallback + " Último problema: " + lastProblem, MaxAttempts);
        }

        public static void CleanupOldCopies(string worldFolder, string keepFolder)
        {
            if (!Directory.Exists(worldFolder)) return;
            foreach (string folder in Directory.GetDirectories(worldFolder, "copy-*"))
                if (!string.Equals(Path.GetFileName(folder), keepFolder, StringComparison.OrdinalIgnoreCase)) TryDelete(folder);
        }

        private static IProgress<long>? Relay(IProgress<DownloadProgress>? progress, int attempt, long offset, long total)
        {
            if (progress == null) return null;
            return new InlineProgress<long>(bytes => progress.Report(new DownloadProgress(attempt, offset + bytes, total)));
        }

        private static void TryDelete(string folder)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `133 checks passed.` (115 + 18)

- [ ] **Step 5: Checkpoint**

Tests pass.

---
### Task 9: Input parsing and the WPF shell (top bar, filters, profiles, download, result grid)

**Files:**
- Create: `src/ActivityViewer.Core/InputParser.cs`, `tests/ActivityViewer.Tests/Program.Input.cs`
- Create: `src/ActivityViewer/ActivityViewer.csproj`, `src/ActivityViewer/viking_icon.ico` (copied)
- Create: `src/ActivityViewer/App.xaml`, `App.xaml.cs`, `Themes/Dark.xaml`
- Create: `src/ActivityViewer/IQueryHost.cs`, `WpfTrustPrompt.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`
- Create: `src/ActivityViewer/Windows/PasswordWindow.xaml`, `.xaml.cs`, `Windows/ProfilesWindow.xaml`, `.xaml.cs`
- Create: `src/ActivityViewer/Controls/ResultView.xaml`, `.xaml.cs`

**Interfaces:**
- Consumes: everything public from Tasks 1–8.
- Produces:
  - `public static class InputParser { static bool TryParseNumber(string text, out double value); static bool TryParseInt(string text, out int value); static bool TryParseStart(string text, out DateTime? value); static bool TryParseEnd(string text, out DateTime? value); }` — empty text is valid (number 0, date null); "Até" with a date only means the end of that day.
  - `public interface IQueryHost { Task RunQueryAsync(string status, Func<ActivityDatabase, QueryFilter, CancellationToken, ResultTable> query, ResultView target); Task ExportAsync(ResultView source, string path); void ShowError(string message); }`
  - `public interface ITabPage { void Attach(IQueryHost host); void OnDatabaseChanged(ActivityDatabase? database); }`
  - `public partial class ResultView : UserControl { IQueryHost? Host; ResultTable? Table; ActivityDatabase? Database; Func<CancellationToken, ResultTable>? FullQuery; static DataTable ToDataTable(ResultTable table); void Show(ResultTable table, DataTable data, ActivityDatabase database, Func<CancellationToken, ResultTable> fullQuery); void Clear(); }`
  - `MainWindow` implements `IQueryHost`; `RegisterPages()` is empty here and filled in Task 10.

- [ ] **Step 1: Write the failing input tests**

`tests/ActivityViewer.Tests/Program.Input.cs`:

```csharp
using System;
using ActivityViewer.Core;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunInput()
        {
            Input_ParsesBrazilianFormats();
        }

        private static void Input_ParsesBrazilianFormats()
        {
            Check(InputParser.TryParseNumber("2,5", out double comma) && comma == 2.5, "Input: decimal comma");
            Check(InputParser.TryParseNumber("2.5", out double dot) && dot == 2.5, "Input: decimal point");
            Check(InputParser.TryParseNumber("  ", out double empty) && empty == 0, "Input: empty number is zero");
            Check(!InputParser.TryParseNumber("abc", out _), "Input: text is not a number");
            Check(!InputParser.TryParseNumber("-3", out _), "Input: negative numbers refused");
            Check(InputParser.TryParseInt("7", out int seven) && seven == 7, "Input: integer");
            Check(!InputParser.TryParseInt("7,5", out _), "Input: decimal refused where an integer is expected");

            Check(InputParser.TryParseStart("01/10/2026 14:30", out DateTime? start) && start == new DateTime(2026, 10, 1, 14, 30, 0) && start.Value.Kind == DateTimeKind.Local, "Input: date and time");
            Check(InputParser.TryParseStart("1/10/2026", out DateTime? day) && day == new DateTime(2026, 10, 1), "Input: date without time starts at midnight");
            Check(InputParser.TryParseStart("", out DateTime? none) && none == null, "Input: empty date means no limit");
            Check(!InputParser.TryParseStart("31/02/2026", out _), "Input: impossible date refused");
            Check(InputParser.TryParseEnd("01/10/2026", out DateTime? end) && end == new DateTime(2026, 10, 1, 23, 59, 59, 999), "Input: 'Até' with a date only covers the whole day");
            Check(InputParser.TryParseEnd("01/10/2026 10:00", out DateTime? endTime) && endTime == new DateTime(2026, 10, 1, 10, 0, 0), "Input: 'Até' with a time is exact");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: compile error `The name 'InputParser' does not exist in the current context`.

- [ ] **Step 3: Implement the parser**

`src/ActivityViewer.Core/InputParser.cs`:

```csharp
using System;
using System.Globalization;

namespace ActivityViewer.Core
{
    public static class InputParser
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly string[] DateTimeFormats = { "d/M/yyyy H:mm:ss", "d/M/yyyy H:mm" };
        private static readonly string[] DateFormats = { "d/M/yyyy" };

        public static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;
            return double.TryParse(trimmed.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseInt(string text, out int value)
        {
            value = 0;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;
            return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseStart(string text, out DateTime? value) => TryParseDate(text, false, out value);

        public static bool TryParseEnd(string text, out DateTime? value) => TryParseDate(text, true, out value);

        private static bool TryParseDate(string text, bool endOfDay, out DateTime? value)
        {
            value = null;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;

            if (DateTime.TryParseExact(trimmed, DateTimeFormats, PtBr, DateTimeStyles.None, out DateTime full))
            {
                value = DateTime.SpecifyKind(full, DateTimeKind.Local);
                return true;
            }

            if (DateTime.TryParseExact(trimmed, DateFormats, PtBr, DateTimeStyles.None, out DateTime day))
            {
                DateTime local = DateTime.SpecifyKind(day, DateTimeKind.Local);
                value = endOfDay ? local.AddDays(1).AddMilliseconds(-1) : local;
                return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `146 checks passed.` (133 + 13)

- [ ] **Step 5: Create the WPF project**

```powershell
$root = "D:\UNITY VALHEIM\ActivityViewer"
New-Item -ItemType Directory -Force "$root\src\ActivityViewer\Themes", "$root\src\ActivityViewer\Windows", "$root\src\ActivityViewer\Controls", "$root\src\ActivityViewer\Tabs" | Out-Null
Copy-Item "E:\##SERVIDOR VIKINGS BRASIL\AnalisadorLogs\3.0 - log de dano\viking_icon.ico" "$root\src\ActivityViewer\viking_icon.ico"
```

`src/ActivityViewer/ActivityViewer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <RootNamespace>ActivityViewer</RootNamespace>
    <AssemblyName>Vikings_ActivityViewer</AssemblyName>
    <ApplicationIcon>viking_icon.ico</ApplicationIcon>
    <Version>1.0.0</Version>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <DebugType>embedded</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ActivityViewer.Core\ActivityViewer.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Resource Include="viking_icon.ico" />
  </ItemGroup>
</Project>
```

```powershell
cd "D:\UNITY VALHEIM\ActivityViewer"
dotnet sln Vikings_ActivityViewer.sln add src/ActivityViewer/ActivityViewer.csproj
```

- [ ] **Step 6: App and theme**

`src/ActivityViewer/App.xaml`:

```xml
<Application x:Class="ActivityViewer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml"
             DispatcherUnhandledException="OnDispatcherUnhandledException">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Dark.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

`src/ActivityViewer/App.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Threading;
using ActivityViewer.Core;

namespace ActivityViewer
{
    public partial class App : Application
    {
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string log = ErrorLog.Write(e.Exception, "Erro não tratado na interface");
            MessageBox.Show("Ocorreu um erro inesperado:\n\n" + e.Exception.Message + (log.Length > 0 ? "\n\nDetalhes em:\n" + log : ""),
                "Vikings Activity Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
```

`src/ActivityViewer/Themes/Dark.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="BgBrush" Color="#1E1F22" />
    <SolidColorBrush x:Key="PanelBrush" Color="#2B2D31" />
    <SolidColorBrush x:Key="ControlBrush" Color="#383A40" />
    <SolidColorBrush x:Key="BorderBrush" Color="#4E5058" />
    <SolidColorBrush x:Key="TextBrush" Color="#E6E6E6" />
    <SolidColorBrush x:Key="MutedBrush" Color="#A0A3A8" />
    <SolidColorBrush x:Key="AccentBrush" Color="#3B82F6" />
    <SolidColorBrush x:Key="SelectionBrush" Color="#2F4F7F" />
    <SolidColorBrush x:Key="AltRowBrush" Color="#26282C" />

    <Style x:Key="BaseButton" TargetType="Button">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="Padding" Value="12,4" />
        <Setter Property="MinHeight" Value="26" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="1" CornerRadius="3" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource TextBrush}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource SelectionBrush}" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.45" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType="Button" BasedOn="{StaticResource BaseButton}" />
    <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
        <Setter Property="Background" Value="{StaticResource AccentBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}" />
        <Setter Property="Foreground" Value="White" />
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="CaretBrush" Value="{StaticResource TextBrush}" />
        <Setter Property="SelectionBrush" Value="{StaticResource AccentBrush}" />
        <Setter Property="Padding" Value="4,3" />
        <Setter Property="MinHeight" Value="26" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
    </Style>
    <Style TargetType="PasswordBox">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="CaretBrush" Value="{StaticResource TextBrush}" />
        <Setter Property="Padding" Value="4,3" />
        <Setter Property="MinHeight" Value="26" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
    </Style>
    <Style x:Key="ComboEditBox" TargetType="TextBox">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="CaretBrush" Value="{StaticResource TextBrush}" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <ScrollViewer x:Name="PART_ContentHost" Margin="4,0,0,0" VerticalAlignment="Center" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <ControlTemplate x:Key="ComboToggle" TargetType="ToggleButton">
        <Border x:Name="Bd" Background="{StaticResource ControlBrush}" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" CornerRadius="3">
            <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,8,0" Data="M 0 0 L 4 4 L 8 0 Z" Fill="{StaticResource MutedBrush}" />
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource TextBrush}" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>
    <Style TargetType="ComboBox">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="MinHeight" Value="26" />
        <Setter Property="Padding" Value="6,3,24,3" />
        <Setter Property="MaxDropDownHeight" Value="360" />
        <Setter Property="VirtualizingPanel.IsVirtualizing" Value="True" />
        <Setter Property="ItemsPanel">
            <Setter.Value>
                <ItemsPanelTemplate>
                    <VirtualizingStackPanel />
                </ItemsPanelTemplate>
            </Setter.Value>
        </Setter>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ComboBox">
                    <Grid>
                        <ToggleButton Template="{StaticResource ComboToggle}" Focusable="False" ClickMode="Press"
                                      IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" />
                        <ContentPresenter x:Name="ContentSite" IsHitTestVisible="False" Margin="{TemplateBinding Padding}" VerticalAlignment="Center"
                                          Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                          ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" />
                        <TextBox x:Name="PART_EditableTextBox" Style="{StaticResource ComboEditBox}" Visibility="Hidden" Margin="1,1,22,1"
                                 IsReadOnly="{TemplateBinding IsReadOnly}" />
                        <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False">
                            <Border Background="{StaticResource PanelBrush}" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1"
                                    MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                                <ScrollViewer>
                                    <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained" />
                                </ScrollViewer>
                            </Border>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsEditable" Value="True">
                            <Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible" />
                            <Setter TargetName="ContentSite" Property="Visibility" Value="Hidden" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.5" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType="ComboBoxItem">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Padding" Value="6,3" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ComboBoxItem">
                    <Border x:Name="Bd" Background="Transparent" Padding="{TemplateBinding Padding}">
                        <ContentPresenter />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource ControlBrush}" />
                        </Trigger>
                        <Trigger Property="IsHighlighted" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource SelectionBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="ListBox">
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    </Style>
    <Style TargetType="ListBoxItem">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Padding" Value="6,4" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="Bd" Background="Transparent" Padding="{TemplateBinding Padding}">
                        <ContentPresenter />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource AltRowBrush}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource SelectionBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="CheckBox">
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
    </Style>

    <Style TargetType="TabControl">
        <Setter Property="Background" Value="{StaticResource PanelBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="Padding" Value="0" />
    </Style>
    <Style TargetType="TabItem">
        <Setter Property="Foreground" Value="{StaticResource MutedBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TabItem">
                    <Border x:Name="Bd" Background="{StaticResource BgBrush}" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1,1,1,0"
                            CornerRadius="4,4,0,0" Padding="14,6" Margin="0,0,2,0">
                        <ContentPresenter ContentSource="Header" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource PanelBrush}" />
                            <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="DataGrid">
        <Setter Property="Background" Value="{StaticResource PanelBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="RowBackground" Value="{StaticResource PanelBrush}" />
        <Setter Property="AlternatingRowBackground" Value="{StaticResource AltRowBrush}" />
        <Setter Property="GridLinesVisibility" Value="Horizontal" />
        <Setter Property="HorizontalGridLinesBrush" Value="{StaticResource AltRowBrush}" />
        <Setter Property="HeadersVisibility" Value="Column" />
    </Style>
    <Style TargetType="DataGridColumnHeader">
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="BorderThickness" Value="0,0,1,1" />
        <Setter Property="Padding" Value="6,4" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>
    <Style TargetType="DataGridCell">
        <Setter Property="BorderThickness" Value="0" />
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource SelectionBrush}" />
                <Setter Property="Foreground" Value="White" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="ProgressBar">
        <Setter Property="Foreground" Value="{StaticResource AccentBrush}" />
        <Setter Property="Background" Value="{StaticResource ControlBrush}" />
        <Setter Property="BorderThickness" Value="0" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 7: Host interfaces and trust prompt**

`src/ActivityViewer/IQueryHost.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Controls;
using ActivityViewer.Core.Data;

namespace ActivityViewer
{
    public interface IQueryHost
    {
        Task RunQueryAsync(string status, Func<ActivityDatabase, QueryFilter, CancellationToken, ResultTable> query, ResultView target);

        Task ExportAsync(ResultView source, string path);

        void ShowError(string message);
    }

    public interface ITabPage
    {
        void Attach(IQueryHost host);

        void OnDatabaseChanged(ActivityDatabase? database);
    }
}
```

`src/ActivityViewer/WpfTrustPrompt.cs`:

```csharp
using System.Windows;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer
{
    internal sealed class WpfTrustPrompt : ITrustPrompt
    {
        private readonly Window _owner;

        internal WpfTrustPrompt(Window owner)
        {
            _owner = owner;
        }

        public bool TrustCertificate(string host, string thumbprint, string subject, bool changed)
        {
            string intro = changed
                ? "ATENÇÃO: o certificado do servidor MUDOU desde a última conexão.\n\n"
                : "O certificado TLS do servidor não é reconhecido pelo Windows.\n\n";
            return Ask(intro + "Servidor: " + host + "\nCertificado: " + subject + "\nImpressão digital: " + thumbprint + "\n\nConfiar neste certificado?");
        }

        public bool TrustHostKey(string host, string fingerprint)
        {
            return Ask("Primeira conexão SFTP com este servidor.\n\nServidor: " + host + "\nChave do servidor (SHA256): " + fingerprint + "\n\nConfiar nesta chave?");
        }

        private bool Ask(string text)
        {
            return _owner.Dispatcher.Invoke(() =>
                MessageBox.Show(_owner, text, "Confirmar servidor", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
        }
    }
}
```

- [ ] **Step 8: Result view**

`src/ActivityViewer/Controls/ResultView.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Controls.ResultView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <DockPanel Margin="0,0,0,6">
            <Button x:Name="ExportButton" DockPanel.Dock="Right" Content="Exportar..." Click="OnExport" IsEnabled="False" Margin="6,0,0,0" />
            <Button x:Name="CopyIdButton" DockPanel.Dock="Right" Content="Copiar ID" Click="OnCopyId" IsEnabled="False" />
            <TextBlock x:Name="Summary" VerticalAlignment="Center" Foreground="{StaticResource MutedBrush}" Text="Nenhuma busca realizada." />
        </DockPanel>
        <DataGrid x:Name="ResultGrid" Grid.Row="1" AutoGenerateColumns="True" IsReadOnly="True" CanUserAddRows="False" SelectionMode="Single"
                  EnableRowVirtualization="True" EnableColumnVirtualization="True" VirtualizingPanel.VirtualizationMode="Recycling"
                  AutoGeneratingColumn="OnAutoGeneratingColumn" SelectionChanged="OnSelectionChanged" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Controls/ResultView.xaml.cs`:

```csharp
using System;
using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ActivityViewer.Core.Data;
using Microsoft.Win32;

namespace ActivityViewer.Controls
{
    public partial class ResultView : UserControl
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public ResultView()
        {
            InitializeComponent();
        }

        public IQueryHost? Host { get; set; }

        public ResultTable? Table { get; private set; }

        public ActivityDatabase? Database { get; private set; }

        public Func<CancellationToken, ResultTable>? FullQuery { get; private set; }

        public static DataTable ToDataTable(ResultTable table)
        {
            var data = new DataTable();
            foreach (ResultColumn column in table.Columns) data.Columns.Add(column.Name, column.Type);
            data.BeginLoadData();
            foreach (object?[] row in table.Rows)
            {
                var values = new object[row.Length];
                for (int i = 0; i < row.Length; i++) values[i] = row[i] ?? DBNull.Value;
                data.Rows.Add(values);
            }
            data.EndLoadData();
            return data;
        }

        public void Show(ResultTable table, DataTable data, ActivityDatabase database, Func<CancellationToken, ResultTable> fullQuery)
        {
            Table = table;
            Database = database;
            FullQuery = fullQuery;
            ResultGrid.ItemsSource = data.DefaultView;
            Summary.Text = table.Rows.Count.ToString("N0", PtBr) + " linha(s) em " + table.Elapsed.TotalSeconds.ToString("0.00", PtBr) + " s" +
                (table.Truncated ? ". Resultado muito grande, refine o filtro (mostrando as primeiras " + table.Rows.Count.ToString("N0", PtBr) + "; Exportar grava tudo)." : ".");
            ExportButton.IsEnabled = true;
            CopyIdButton.IsEnabled = false;
        }

        public void Clear()
        {
            Table = null;
            Database = null;
            FullQuery = null;
            ResultGrid.ItemsSource = null;
            Summary.Text = "Nenhuma busca realizada.";
            ExportButton.IsEnabled = false;
            CopyIdButton.IsEnabled = false;
        }

        private void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!(e.Column is DataGridTextColumn text) || !(text.Binding is Binding binding)) return;
            if (e.PropertyType == typeof(DateTime)) binding.StringFormat = TimeFormat.Pattern;
            else if (e.PropertyType == typeof(double)) binding.StringFormat = "0.##";
            binding.ConverterCulture = PtBr;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CopyIdButton.IsEnabled = ResultGrid.SelectedItem is DataRowView row && row.Row.Table.Columns.Contains("ID");
        }

        private void OnCopyId(object sender, RoutedEventArgs e)
        {
            if (!(ResultGrid.SelectedItem is DataRowView row) || !row.Row.Table.Columns.Contains("ID")) return;
            string id = row["ID"]?.ToString() ?? "";
            if (id.Length == 0) return;
            try
            {
                Clipboard.SetText(id);
                Summary.Text = "ID copiado: " + id;
            }
            catch (ExternalException)
            {
                Host?.ShowError("Não foi possível copiar para a área de transferência. Tente novamente.");
            }
        }

        private async void OnExport(object sender, RoutedEventArgs e)
        {
            if (Table == null || Host == null) return;
            var dialog = new SaveFileDialog
            {
                Title = "Exportar resultado",
                Filter = "CSV para Excel (*.csv)|*.csv|Texto (*.txt)|*.txt",
                FileName = "relatorio-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            await Host.ExportAsync(this, dialog.FileName);
        }
    }
}
```

- [ ] **Step 9: Password and profiles windows**

`src/ActivityViewer/Windows/PasswordWindow.xaml`:

```xml
<Window x:Class="ActivityViewer.Windows.PasswordWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Senha" SizeToContent="WidthAndHeight" ResizeMode="NoResize" WindowStartupLocation="CenterOwner" ShowInTaskbar="False"
        Background="{StaticResource BgBrush}" Foreground="{StaticResource TextBrush}" FontFamily="Segoe UI" FontSize="13">
    <StackPanel Margin="16" Width="360">
        <TextBlock x:Name="MessageText" TextWrapping="Wrap" Margin="0,0,0,10" />
        <PasswordBox x:Name="PasswordInput" />
        <CheckBox x:Name="SaveCheck" Content="Salvar senha neste PC (criptografada pelo Windows)" IsChecked="True" Margin="0,10,0,0" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="OK" IsDefault="True" Style="{StaticResource AccentButton}" Click="OnOk" Width="90" />
            <Button Content="Cancelar" IsCancel="True" Width="90" Margin="8,0,0,0" />
        </StackPanel>
    </StackPanel>
</Window>
```

`src/ActivityViewer/Windows/PasswordWindow.xaml.cs`:

```csharp
using System.Windows;

namespace ActivityViewer.Windows
{
    public partial class PasswordWindow : Window
    {
        public PasswordWindow(string profileName, bool savedPasswordFailed)
        {
            InitializeComponent();
            MessageText.Text = (savedPasswordFailed ? "A senha salva não pôde ser lida neste PC ou usuário do Windows. " : "") +
                "Digite a senha do perfil \"" + profileName + "\".";
            Loaded += (sender, e) => PasswordInput.Focus();
        }

        public string Password => PasswordInput.Password;

        public bool SavePassword => SaveCheck.IsChecked == true;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (PasswordInput.Password.Length == 0)
            {
                MessageBox.Show(this, "Digite a senha.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
    }
}
```

`src/ActivityViewer/Windows/ProfilesWindow.xaml`:

```xml
<Window x:Class="ActivityViewer.Windows.ProfilesWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Perfis de servidor" Width="800" Height="540" MinWidth="700" MinHeight="480" WindowStartupLocation="CenterOwner" ShowInTaskbar="False"
        Background="{StaticResource BgBrush}" Foreground="{StaticResource TextBrush}" FontFamily="Segoe UI" FontSize="13">
    <Grid Margin="12">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <DockPanel>
            <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,8,0,0">
                <Button Content="Novo" Click="OnNew" Width="100" />
                <Button Content="Excluir" Click="OnDelete" Width="100" Margin="8,0,0,0" />
            </StackPanel>
            <ListBox x:Name="ProfileList" DisplayMemberPath="Name" SelectionChanged="OnSelectionChanged" />
        </DockPanel>
        <Grid Grid.Column="1" Margin="16,0,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <TextBlock Text="Nome:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <TextBox x:Name="NameBox" Grid.Column="1" Margin="0,0,0,8" />

            <TextBlock Grid.Row="1" Text="Protocolo:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <ComboBox x:Name="ProtocolBox" Grid.Row="1" Grid.Column="1" Width="140" HorizontalAlignment="Left" Margin="0,0,0,8" SelectionChanged="OnProtocolChanged" />

            <TextBlock Grid.Row="2" Text="Host:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <TextBox x:Name="HostBox" Grid.Row="2" Grid.Column="1" Margin="0,0,0,8" />

            <TextBlock Grid.Row="3" Text="Porta:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <TextBox x:Name="PortBox" Grid.Row="3" Grid.Column="1" Width="90" HorizontalAlignment="Left" Margin="0,0,0,8" />

            <TextBlock Grid.Row="4" Text="Usuário:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <TextBox x:Name="UserBox" Grid.Row="4" Grid.Column="1" Margin="0,0,0,8" />

            <TextBlock Grid.Row="5" Text="Senha:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <StackPanel Grid.Row="5" Grid.Column="1" Margin="0,0,0,8">
                <PasswordBox x:Name="PasswordInput" />
                <TextBlock x:Name="PasswordHint" Foreground="{StaticResource MutedBrush}" FontSize="12" Margin="0,2,0,0" />
            </StackPanel>

            <CheckBox x:Name="SaveCheck" Grid.Row="6" Grid.Column="1" Content="Salvar senha neste PC (criptografada pelo Windows)" IsChecked="True" Margin="0,0,0,8" />

            <TextBlock Grid.Row="7" Text="Pasta remota:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <StackPanel Grid.Row="7" Grid.Column="1" Margin="0,0,0,8">
                <TextBox x:Name="FolderBox" />
                <TextBlock Foreground="{StaticResource MutedBrush}" FontSize="12" Margin="0,2,0,0" TextWrapping="Wrap"
                           Text="Pasta do -savedir do servidor seguida de /Vikings_ActivityLog. Ex.: /SAVE/Vikings_ActivityLog" />
            </StackPanel>

            <TextBlock Grid.Row="8" Text="Confiança:" VerticalAlignment="Center" Margin="0,0,10,8" />
            <DockPanel Grid.Row="8" Grid.Column="1" Margin="0,0,0,8">
                <Button DockPanel.Dock="Right" Content="Limpar confiança" Click="OnClearTrust" Margin="8,0,0,0" />
                <TextBlock x:Name="TrustText" Foreground="{StaticResource MutedBrush}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
            </DockPanel>

            <DockPanel Grid.Row="10" Grid.ColumnSpan="2">
                <Button DockPanel.Dock="Right" Content="Fechar" IsCancel="True" Width="100" Margin="8,0,0,0" />
                <Button DockPanel.Dock="Right" Content="Salvar" Style="{StaticResource AccentButton}" Click="OnSave" Width="100" Margin="8,0,0,0" />
                <Button x:Name="TestButton" DockPanel.Dock="Right" Content="Testar conexão" Click="OnTest" />
                <TextBlock x:Name="StatusText" Foreground="{StaticResource MutedBrush}" VerticalAlignment="Center" />
            </DockPanel>
        </Grid>
    </Grid>
</Window>
```

`src/ActivityViewer/Windows/ProfilesWindow.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Profiles;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer.Windows
{
    public partial class ProfilesWindow : Window
    {
        private readonly ProfileStore _store;
        private readonly List<ServerProfile> _profiles;
        private ServerProfile? _current;
        private string _certificate = "";
        private string _hostKey = "";
        private bool _filling;

        public ProfilesWindow(ProfileStore store)
        {
            InitializeComponent();
            _store = store;
            _profiles = store.Load();
            ProtocolBox.ItemsSource = new[] { TransferProtocol.Ftp, TransferProtocol.Ftps, TransferProtocol.Sftp };
            if (_profiles.Count == 0) StartNew();
            else RefreshList(_profiles[0]);
        }

        public string? SelectedName { get; private set; }

        private void RefreshList(ServerProfile? select)
        {
            ProfileList.ItemsSource = null;
            ProfileList.ItemsSource = _profiles;
            ProfileList.SelectedItem = select;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileList.SelectedItem is ServerProfile profile) Fill(profile, profile);
        }

        private void Fill(ServerProfile values, ServerProfile? current)
        {
            _filling = true;
            _current = current;
            NameBox.Text = values.Name;
            ProtocolBox.SelectedItem = values.Protocol;
            HostBox.Text = values.Host;
            PortBox.Text = values.Port.ToString(CultureInfo.InvariantCulture);
            UserBox.Text = values.User;
            PasswordInput.Password = "";
            SaveCheck.IsChecked = true;
            FolderBox.Text = values.RemoteFolder;
            _certificate = values.TrustedCertificateThumbprint;
            _hostKey = values.TrustedHostKeyFingerprint;
            PasswordHint.Text = values.EncryptedPassword.Length > 0 ? "Senha salva. Deixe em branco para manter." : "Nenhuma senha salva.";
            UpdateTrustText();
            StatusText.Text = "";
            _filling = false;
        }

        private void StartNew()
        {
            ProfileList.SelectedItem = null;
            Fill(new ServerProfile { RemoteFolder = "/SAVE/Vikings_ActivityLog" }, null);
            NameBox.Focus();
        }

        private void OnNew(object sender, RoutedEventArgs e) => StartNew();

        private void OnProtocolChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || !(ProtocolBox.SelectedItem is TransferProtocol protocol)) return;
            string port = PortBox.Text.Trim();
            if (port.Length == 0 || port == "21" || port == "22") PortBox.Text = ServerProfile.DefaultPort(protocol).ToString(CultureInfo.InvariantCulture);
        }

        private ServerProfile? ReadForm()
        {
            if (!int.TryParse(PortBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int port)) port = 0;
            var profile = new ServerProfile
            {
                Name = NameBox.Text.Trim(),
                Protocol = ProtocolBox.SelectedItem is TransferProtocol protocol ? protocol : TransferProtocol.Ftp,
                Host = HostBox.Text.Trim(),
                Port = port,
                User = UserBox.Text.Trim(),
                RemoteFolder = FolderBox.Text.Trim(),
                EncryptedPassword = _current?.EncryptedPassword ?? "",
                TrustedCertificateThumbprint = _certificate,
                TrustedHostKeyFingerprint = _hostKey
            };

            string? error = profile.Validate();
            if (error == null && _profiles.Any(other => !ReferenceEquals(other, _current) && string.Equals(other.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
                error = "Já existe um perfil com esse nome.";
            if (error != null)
            {
                MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (PasswordInput.Password.Length > 0)
                profile.EncryptedPassword = SaveCheck.IsChecked == true ? PasswordProtector.Protect(PasswordInput.Password) : "";
            else if (SaveCheck.IsChecked != true)
                profile.EncryptedPassword = "";
            return profile;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = ReadForm();
            if (profile == null) return;
            if (_current == null) _profiles.Add(profile);
            else _profiles[_profiles.IndexOf(_current)] = profile;
            _store.Save(_profiles);
            SelectedName = profile.Name;
            RefreshList(profile);
            StatusText.Text = "Perfil salvo.";
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (!(ProfileList.SelectedItem is ServerProfile profile)) return;
            string question = "Excluir o perfil \"" + profile.Name + "\"? As cópias já baixadas não são apagadas.";
            if (MessageBox.Show(this, question, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _profiles.Remove(profile);
            _store.Save(_profiles);
            if (SelectedName == profile.Name) SelectedName = null;
            if (_profiles.Count == 0) StartNew();
            else RefreshList(_profiles[0]);
        }

        private void OnClearTrust(object sender, RoutedEventArgs e)
        {
            _certificate = "";
            _hostKey = "";
            UpdateTrustText();
            StatusText.Text = "Confiança removida. Clique em Salvar.";
        }

        private void UpdateTrustText()
        {
            TrustText.Text = _certificate.Length > 0 ? "Certificado confiável: " + _certificate
                : _hostKey.Length > 0 ? "Chave SFTP confiável: " + _hostKey
                : "Nenhum certificado ou chave confiável salvo.";
        }

        private async void OnTest(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = ReadForm();
            if (profile == null) return;
            string password = PasswordInput.Password;
            if (password.Length == 0 && !PasswordProtector.TryUnprotect(profile.EncryptedPassword, out password))
            {
                MessageBox.Show(this, "Digite a senha para testar a conexão.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestButton.IsEnabled = false;
            StatusText.Text = "Testando conexão...";
            try
            {
                var prompt = new WpfTrustPrompt(this);
                IReadOnlyList<RemoteFileInfo> files = await Task.Run(async () =>
                {
                    using IRemoteFolder remote = RemoteFolderFactory.Create(profile, password, prompt);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await remote.ConnectAsync(timeout.Token);
                    return await remote.ListDatabasesAsync(timeout.Token);
                });

                bool trustChanged = profile.TrustedCertificateThumbprint != _certificate || profile.TrustedHostKeyFingerprint != _hostKey;
                _certificate = profile.TrustedCertificateThumbprint;
                _hostKey = profile.TrustedHostKeyFingerprint;
                UpdateTrustText();

                string list = files.Count == 0
                    ? "Nenhum arquivo .db encontrado em " + profile.RemoteFolder + "."
                    : files.Count + " mundo(s): " + string.Join(", ", files.Select(f => f.Name));
                MessageBox.Show(this, "Conexão OK.\n\n" + list + (trustChanged ? "\n\nClique em Salvar para guardar a confiança neste servidor." : ""),
                    Title, MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "Conexão OK.";
            }
            catch (Exception error)
            {
                ErrorLog.Write(error, "Teste de conexão com " + profile.Host);
                MessageBox.Show(this, "Falha na conexão:\n\n" + error.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Falha na conexão.";
            }
            finally
            {
                TestButton.IsEnabled = true;
            }
        }
    }
}
```

- [ ] **Step 10: Main window**

`src/ActivityViewer/MainWindow.xaml`:

```xml
<Window x:Class="ActivityViewer.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Vikings Activity Viewer" Width="1400" Height="820" MinWidth="1000" MinHeight="600" WindowStartupLocation="CenterScreen"
        Icon="viking_icon.ico" Background="{StaticResource BgBrush}" Foreground="{StaticResource TextBrush}" FontFamily="Segoe UI" FontSize="13"
        Loaded="OnLoaded" Closing="OnClosing">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Border x:Name="TopBar" Background="{StaticResource PanelBrush}" CornerRadius="4" Padding="10,8">
            <WrapPanel VerticalAlignment="Center">
                <TextBlock Text="Perfil:" VerticalAlignment="Center" Margin="0,0,6,0" />
                <ComboBox x:Name="ProfileBox" Width="200" DisplayMemberPath="Name" SelectionChanged="OnProfileChanged" />
                <Button Content="Perfis..." Click="OnManageProfiles" Margin="6,0,18,0" />
                <TextBlock Text="Mundo:" VerticalAlignment="Center" Margin="0,0,6,0" />
                <ComboBox x:Name="WorldBox" Width="200" SelectionChanged="OnWorldChanged" />
                <Button Content="Listar mundos" Click="OnListWorlds" Margin="6,0,0,0" />
                <Button Content="Atualizar" Style="{StaticResource AccentButton}" Click="OnRefresh" Margin="6,0,0,0" />
                <Button Content="Abrir arquivo local..." Click="OnOpenLocal" Margin="18,0,0,0" />
                <TextBlock x:Name="CopyInfo" Foreground="{StaticResource MutedBrush}" VerticalAlignment="Center" Margin="18,0,0,0" />
            </WrapPanel>
        </Border>

        <Border x:Name="FilterBar" Grid.Row="1" Background="{StaticResource PanelBrush}" CornerRadius="4" Padding="10,8" Margin="0,8,0,0">
            <WrapPanel VerticalAlignment="Center">
                <TextBlock Text="Jogador:" VerticalAlignment="Center" Margin="0,0,6,0" />
                <ComboBox x:Name="PlayerBox" Width="300" IsEditable="True" IsTextSearchEnabled="True" />
                <TextBlock Text="De:" VerticalAlignment="Center" Margin="18,0,6,0" />
                <TextBox x:Name="FromBox" Width="140" ToolTip="dd/MM/aaaa ou dd/MM/aaaa HH:mm (vazio = sem limite)" />
                <TextBlock Text="Até:" VerticalAlignment="Center" Margin="12,0,6,0" />
                <TextBox x:Name="ToBox" Width="140" ToolTip="dd/MM/aaaa ou dd/MM/aaaa HH:mm (só a data = até o fim do dia)" />
                <Button Content="Limpar filtros" Click="OnClearFilters" Margin="18,0,0,0" />
            </WrapPanel>
        </Border>

        <TabControl x:Name="Tabs" Grid.Row="2" Margin="0,8,0,0" />

        <Grid Grid.Row="3" Margin="0,8,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock x:Name="StatusText" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
            <ProgressBar x:Name="BusyBar" Grid.Column="1" Width="220" Height="8" Margin="10,0" Visibility="Collapsed" />
            <Button x:Name="CancelButton" Grid.Column="2" Content="Cancelar" Click="OnCancel" IsEnabled="False" />
        </Grid>
    </Grid>
</Window>
```

`src/ActivityViewer/MainWindow.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Cache;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Export;
using ActivityViewer.Core.Profiles;
using ActivityViewer.Core.Transfer;
using ActivityViewer.Windows;
using Microsoft.Win32;

namespace ActivityViewer
{
    public sealed class PlayerChoice
    {
        public PlayerChoice(string? platformId, string display)
        {
            PlatformId = platformId;
            Display = display;
        }

        public string? PlatformId { get; }

        public string Display { get; }

        public override string ToString() => Display;
    }

    public partial class MainWindow : Window, IQueryHost
    {
        private const string AppTitle = "Vikings Activity Viewer";
        private const string AllPlayers = "Todos os jogadores";
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private readonly ProfileStore _store = ProfileStore.Default();
        private readonly List<ITabPage> _pages = new List<ITabPage>();
        private List<ServerProfile> _profiles = new List<ServerProfile>();
        private ActivityDatabase? _database;
        private CancellationTokenSource? _cancel;
        private bool _busy;
        private bool _fillingWorlds;

        public MainWindow()
        {
            InitializeComponent();
            RegisterPages();
            foreach (ITabPage page in _pages) page.Attach(this);
        }

        private void RegisterPages()
        {
        }

        private ServerProfile? SelectedProfile => ProfileBox.SelectedItem as ServerProfile;

        private string? SelectedWorld => WorldBox.SelectedItem as string;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadProfiles(null);
            if (_store.LastBackup != null)
                MessageBox.Show(this, "O arquivo de perfis estava inválido e foi guardado como:\n" + _store.LastBackup, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            if (_database == null) SetStatus("Escolha um perfil e um mundo, ou abra um arquivo local.");
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            _cancel?.Cancel();
            CloseDatabase();
        }

        private void LoadProfiles(string? selectName)
        {
            _profiles = _store.Load();
            ProfileBox.ItemsSource = _profiles;
            ServerProfile? target = _profiles.FirstOrDefault(p => p.Name == selectName) ?? _profiles.FirstOrDefault();
            ProfileBox.SelectedItem = target;
            if (target == null) FillWorlds(Array.Empty<string>());
        }

        private void SaveProfiles() => _store.Save(_profiles);

        private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            FillWorlds(profile == null ? Array.Empty<string>() : CachePaths.CachedWorlds(profile.Name));
        }

        private void FillWorlds(IEnumerable<string> worlds)
        {
            string? previous = SelectedWorld;
            List<string> list = worlds.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
            _fillingWorlds = true;
            WorldBox.ItemsSource = list;
            _fillingWorlds = false;
            string? select = list.FirstOrDefault(w => string.Equals(w, previous, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
            if (select == null)
            {
                CloseDatabase();
                CopyInfo.Text = "";
                return;
            }
            if (WorldBox.SelectedItem as string == select) OpenCachedWorld();
            else WorldBox.SelectedItem = select;
        }

        private void OnWorldChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_fillingWorlds || WorldBox.SelectedItem == null) return;
            OpenCachedWorld();
        }

        private void OpenCachedWorld()
        {
            ServerProfile? profile = SelectedProfile;
            string? world = SelectedWorld;
            if (profile == null || world == null) return;

            string folder = CachePaths.WorldFolder(profile.Name, world);
            CachedCopy? copy = CachedCopy.Load(folder);
            if (copy == null)
            {
                CloseDatabase();
                CopyInfo.Text = "Sem cópia local. Clique em Atualizar.";
                SetStatus("O mundo " + world + " ainda não foi baixado.");
                return;
            }
            if (OpenDatabase(copy.DatabasePathIn(folder), copy.Describe())) SafeDownloader.CleanupOldCopies(folder, copy.Folder);
        }

        private bool OpenDatabase(string path, string description)
        {
            CloseDatabase();
            try
            {
                _database = ActivityDatabase.Open(path);
            }
            catch (Exception error) when (error is InvalidActivityDatabaseException || error is FileNotFoundException)
            {
                ShowError(error.Message);
                return false;
            }
            catch (Exception error)
            {
                ReportUnexpected(error, "Abrir banco " + path);
                return false;
            }

            var players = new List<PlayerChoice> { new PlayerChoice(null, AllPlayers) };
            players.AddRange(_database.Players().Select(p => new PlayerChoice(p.PlatformId, p.Display)));
            PlayerBox.ItemsSource = players;
            PlayerBox.SelectedIndex = 0;
            CopyInfo.Text = description;
            Title = AppTitle + " - " + Path.GetFileName(path);
            foreach (ITabPage page in _pages) page.OnDatabaseChanged(_database);
            SetStatus("Banco aberto: " + (players.Count - 1) + " jogador(es).");
            return true;
        }

        private void CloseDatabase()
        {
            if (_database == null) return;
            foreach (ITabPage page in _pages) page.OnDatabaseChanged(null);
            _database.Dispose();
            _database = null;
            PlayerBox.ItemsSource = null;
            Title = AppTitle;
        }

        private string? GetPassword(ServerProfile profile)
        {
            if (PasswordProtector.TryUnprotect(profile.EncryptedPassword, out string saved)) return saved;

            var dialog = new PasswordWindow(profile.Name, profile.EncryptedPassword.Length > 0) { Owner = this };
            if (dialog.ShowDialog() != true) return null;
            if (dialog.SavePassword)
            {
                profile.EncryptedPassword = PasswordProtector.Protect(dialog.Password);
                SaveProfiles();
            }
            return dialog.Password;
        }

        private async void OnListWorlds(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            if (profile == null)
            {
                ShowError("Escolha ou crie um perfil primeiro (botão Perfis...).");
                return;
            }
            string? password = GetPassword(profile);
            if (password == null) return;

            IReadOnlyList<RemoteFileInfo>? found = await RunTransferAsync("Conectando a " + profile.Host + "...", profile, password,
                (remote, token) => remote.ListDatabasesAsync(token));
            if (found == null) return;

            FillWorlds(found.Select(f => f.Name).Concat(CachePaths.CachedWorlds(profile.Name)));
            SetStatus(found.Count == 0 ? "Nenhum mundo encontrado em " + profile.RemoteFolder + "." : found.Count + " mundo(s) encontrado(s) no servidor.");
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            string? world = SelectedWorld;
            if (profile == null || world == null)
            {
                ShowError("Escolha um perfil e um mundo. Use \"Listar mundos\" para ver os mundos do servidor.");
                return;
            }
            string? password = GetPassword(profile);
            if (password == null) return;

            string folder = CachePaths.WorldFolder(profile.Name, world);
            var progress = new Progress<DownloadProgress>(ShowDownloadProgress);
            DownloadResult? result = await RunTransferAsync("Baixando " + world + "...", profile, password,
                (remote, token) => new SafeDownloader().DownloadAsync(remote, world, folder, progress, token));
            if (result == null) return;

            if (result.Success && result.Copy != null)
            {
                if (OpenDatabase(result.Copy.DatabasePathIn(folder), result.Copy.Describe())) SafeDownloader.CleanupOldCopies(folder, result.Copy.Folder);
                SetStatus(result.Message);
                return;
            }

            MessageBox.Show(this, result.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus(result.Message);
            if (_database == null && result.Copy != null) OpenDatabase(result.Copy.DatabasePathIn(folder), result.Copy.Describe());
        }

        private void ShowDownloadProgress(DownloadProgress p)
        {
            if (!_busy) return;
            BusyBar.IsIndeterminate = p.Total <= 0;
            if (p.Total > 0)
            {
                BusyBar.Maximum = p.Total;
                BusyBar.Value = Math.Min(p.Received, p.Total);
            }
            SetStatus("Baixando (tentativa " + p.Attempt + "/" + SafeDownloader.MaxAttempts + "): " + Megabytes(p.Received) + " de " + Megabytes(p.Total) + " MB");
        }

        private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", PtBr);

        private async Task<T?> RunTransferAsync<T>(string status, ServerProfile profile, string password,
            Func<IRemoteFolder, CancellationToken, Task<T>> work) where T : class
        {
            if (_busy) return null;
            CancellationTokenSource cancel = BeginBusy(status);
            var prompt = new WpfTrustPrompt(this);
            bool profileChanged = false;
            try
            {
                return await Task.Run(async () =>
                {
                    using IRemoteFolder remote = RemoteFolderFactory.Create(profile, password, prompt);
                    try
                    {
                        await remote.ConnectAsync(cancel.Token);
                        return await work(remote, cancel.Token);
                    }
                    finally
                    {
                        profileChanged = remote.ProfileChanged;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                SetStatus("Operação cancelada.");
                return null;
            }
            catch (Exception error)
            {
                ErrorLog.Write(error, "Conexão com " + profile.Host);
                ShowError("Falha na conexão com " + profile.Host + ":\n\n" + error.Message);
                return null;
            }
            finally
            {
                if (profileChanged) SaveProfiles();
                EndBusy();
            }
        }

        private void OnOpenLocal(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Abrir banco do Vikings_ActivityLog", Filter = "Banco SQLite (*.db)|*.db|Todos os arquivos (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            OpenDatabase(dialog.FileName, "Arquivo local: " + dialog.FileName);
        }

        private void OnManageProfiles(object sender, RoutedEventArgs e)
        {
            string? current = SelectedProfile?.Name;
            var window = new ProfilesWindow(_store) { Owner = this };
            window.ShowDialog();
            LoadProfiles(window.SelectedName ?? current);
        }

        private void OnClearFilters(object sender, RoutedEventArgs e)
        {
            PlayerBox.SelectedIndex = PlayerBox.Items.Count > 0 ? 0 : -1;
            FromBox.Text = "";
            ToBox.Text = "";
        }

        private bool TryGetFilter(out QueryFilter filter)
        {
            filter = QueryFilter.All;
            string? platformId = null;
            string typed = PlayerBox.Text.Trim();
            if (PlayerBox.SelectedItem is PlayerChoice choice) platformId = choice.PlatformId;
            else if (typed.Length > 0 && typed != AllPlayers)
            {
                ShowError("Jogador não encontrado: " + typed + ". Escolha um jogador da lista.");
                return false;
            }

            if (!InputParser.TryParseStart(FromBox.Text, out DateTime? from))
            {
                ShowError("Data inválida em \"De\". Use dd/MM/aaaa ou dd/MM/aaaa HH:mm.");
                return false;
            }
            if (!InputParser.TryParseEnd(ToBox.Text, out DateTime? to))
            {
                ShowError("Data inválida em \"Até\". Use dd/MM/aaaa ou dd/MM/aaaa HH:mm.");
                return false;
            }
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                ShowError("A data em \"De\" é posterior à data em \"Até\".");
                return false;
            }

            filter = new QueryFilter { PlatformId = platformId, From = from, To = to };
            return true;
        }

        public async Task RunQueryAsync(string status, Func<ActivityDatabase, QueryFilter, CancellationToken, ResultTable> query, ResultView target)
        {
            ActivityDatabase? database = _database;
            if (database == null)
            {
                ShowError("Abra um banco primeiro: escolha um mundo, clique em Atualizar ou abra um arquivo local.");
                return;
            }
            if (_busy)
            {
                SetStatus("Aguarde a operação atual terminar.");
                return;
            }
            if (!TryGetFilter(out QueryFilter filter)) return;

            CancellationTokenSource cancel = BeginBusy(status);
            try
            {
                ResultTable table = await Task.Run(() => query(database, filter, cancel.Token));
                DataTable data = await Task.Run(() => ResultView.ToDataTable(table));
                var full = new QueryFilter { PlatformId = filter.PlatformId, From = filter.From, To = filter.To, RowLimit = int.MaxValue };
                target.Show(table, data, database, token => query(database, full, token));
                SetStatus(table.Rows.Count.ToString("N0", PtBr) + " linha(s) em " + table.Elapsed.TotalSeconds.ToString("0.00", PtBr) + " s.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Busca cancelada.");
            }
            catch (Exception error)
            {
                ReportUnexpected(error, status);
            }
            finally
            {
                EndBusy();
            }
        }

        public async Task ExportAsync(ResultView source, string path)
        {
            ResultTable? table = source.Table;
            if (table == null) return;
            if (_busy)
            {
                SetStatus("Aguarde a operação atual terminar.");
                return;
            }
            if (table.Truncated && !ReferenceEquals(source.Database, _database))
            {
                ShowError("O banco foi trocado depois desta busca. Refaça a busca antes de exportar.");
                return;
            }

            CancellationTokenSource cancel = BeginBusy("Exportando...");
            try
            {
                Func<CancellationToken, ResultTable>? fullQuery = source.FullQuery;
                await Task.Run(() =>
                {
                    ResultTable complete = table.Truncated && fullQuery != null ? fullQuery(cancel.Token) : table;
                    ResultExporter.Export(complete, path);
                });
                SetStatus("Exportado: " + path);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Exportação cancelada.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ShowError("Não foi possível gravar o arquivo:\n\n" + error.Message);
            }
            catch (Exception error)
            {
                ReportUnexpected(error, "Exportar " + path);
            }
            finally
            {
                EndBusy();
            }
        }

        private CancellationTokenSource BeginBusy(string status)
        {
            _busy = true;
            _cancel = new CancellationTokenSource();
            TopBar.IsEnabled = false;
            FilterBar.IsEnabled = false;
            CancelButton.IsEnabled = true;
            BusyBar.Visibility = Visibility.Visible;
            BusyBar.IsIndeterminate = true;
            SetStatus(status);
            return _cancel;
        }

        private void EndBusy()
        {
            _busy = false;
            _cancel?.Dispose();
            _cancel = null;
            TopBar.IsEnabled = true;
            FilterBar.IsEnabled = true;
            CancelButton.IsEnabled = false;
            BusyBar.Visibility = Visibility.Collapsed;
            BusyBar.IsIndeterminate = false;
            BusyBar.Value = 0;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            _cancel?.Cancel();
            SetStatus("Cancelando...");
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus(message.Split('\n')[0]);
        }

        private void ReportUnexpected(Exception error, string context)
        {
            string log = ErrorLog.Write(error, context);
            MessageBox.Show(this, "Erro inesperado: " + error.Message + (log.Length > 0 ? "\n\nDetalhes em:\n" + log : ""), AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Erro: " + error.Message);
        }

        private void SetStatus(string text) => StatusText.Text = text;
    }
}
```

- [ ] **Step 11: Build and smoke-run**

Run: `dotnet build src/ActivityViewer/ActivityViewer.csproj -c Debug`
Expected: `0 Erro(s)` / `0 Error(s)` and `0 Aviso(s)` / `0 Warning(s)`.

Run (PowerShell):

```powershell
$exe = Get-ChildItem "D:\UNITY VALHEIM\ActivityViewer\src\ActivityViewer\bin\Debug" -Recurse -Filter Vikings_ActivityViewer.exe | Select-Object -First 1
$p = Start-Process $exe.FullName -PassThru
Start-Sleep -Seconds 6
"alive=" + (-not $p.HasExited)
Stop-Process $p -ErrorAction SilentlyContinue
Get-ChildItem "$env:LOCALAPPDATA\Vikings_ActivityViewer\logs" -ErrorAction SilentlyContinue
```

Expected: `alive=True`; no log file from this run.

- [ ] **Step 12: Checkpoint**

Tests: `146 checks passed.`; app builds and starts.

---

### Task 10: The five tabs

**Files:**
- Create: `src/ActivityViewer/Tabs/DamageTab.xaml`, `.xaml.cs`, `ItemsTab.xaml`, `.xaml.cs`, `FrequencyTab.xaml`, `.xaml.cs`, `SpeedTab.xaml`, `.xaml.cs`, `InteractionsTab.xaml`, `.xaml.cs`
- Modify: `src/ActivityViewer/MainWindow.xaml` (TabControl), `src/ActivityViewer/MainWindow.xaml.cs` (`RegisterPages`)

**Interfaces:**
- Consumes: `IQueryHost`, `ITabPage`, `ResultView` (Task 9); `DamageQuery`, `ItemQuery`, `FrequencyQuery`, `SpeedQuery`, `InteractionQuery`, criteria types (Tasks 3–4); `InputParser` (Task 9).

- [ ] **Step 1: Damage tab**

`src/ActivityViewer/Tabs/DamageTab.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Tabs.DamageTab"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ActivityViewer.Controls">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="Dano mín.:" VerticalAlignment="Center" Margin="0,0,6,0" />
            <TextBox x:Name="MinBox" Width="70" Text="0" />
            <TextBlock Text="Alvo:" VerticalAlignment="Center" Margin="14,0,6,0" />
            <ComboBox x:Name="TargetBox" Width="220" IsEditable="True" />
            <TextBlock Text="Atacante:" VerticalAlignment="Center" Margin="14,0,6,0" />
            <ComboBox x:Name="AttackerBox" Width="220" IsEditable="True" />
            <Button Content="Buscar" Style="{StaticResource AccentButton}" Click="OnSearch" Margin="14,0,0,0" />
        </WrapPanel>
        <controls:ResultView x:Name="Results" Grid.Row="1" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Tabs/DamageTab.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Tabs
{
    public partial class DamageTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public DamageTab()
        {
            InitializeComponent();
        }

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            TargetBox.ItemsSource = database?.DistinctTargets("Damage");
            AttackerBox.ItemsSource = database?.DistinctTargets("Damaged");
        }

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            if (!InputParser.TryParseNumber(MinBox.Text, out double min))
            {
                _host.ShowError("Dano mínimo inválido. Use um número, por exemplo 50 ou 12,5.");
                return;
            }
            var criteria = new DamageCriteria { MinTotal = min, Target = TargetBox.Text.Trim(), Attacker = AttackerBox.Text.Trim() };
            await _host.RunQueryAsync("Buscando dano...", (db, filter, token) => DamageQuery.Run(db, filter, criteria, token), Results);
        }
    }
}
```

- [ ] **Step 2: Items tab**

`src/ActivityViewer/Tabs/ItemsTab.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Tabs.ItemsTab"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ActivityViewer.Controls">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="Item:" VerticalAlignment="Center" Margin="0,0,6,0" />
            <ComboBox x:Name="PrefabBox" Width="260" IsEditable="True" />
            <TextBlock Text="Qtd. mín.:" VerticalAlignment="Center" Margin="14,0,6,0" />
            <TextBox x:Name="MinBox" Width="70" Text="0" />
            <Button Content="Buscar" Style="{StaticResource AccentButton}" Click="OnSearch" Margin="14,0,0,0" />
        </WrapPanel>
        <WrapPanel x:Name="EventsPanel" Grid.Row="1" Margin="0,0,0,8" />
        <controls:ResultView x:Name="Results" Grid.Row="2" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Tabs/ItemsTab.xaml.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Tabs
{
    public partial class ItemsTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public ItemsTab()
        {
            InitializeComponent();
            foreach (string name in ItemQuery.AllEvents)
                EventsPanel.Children.Add(new CheckBox { Content = name, IsChecked = true, Margin = new Thickness(0, 0, 14, 0) });
        }

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            PrefabBox.ItemsSource = database?.DistinctPrefabs();
        }

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            if (!InputParser.TryParseInt(MinBox.Text, out int min))
            {
                _host.ShowError("Quantidade mínima inválida. Use um número inteiro.");
                return;
            }
            List<string> events = EventsPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToList();
            if (events.Count == 0)
            {
                _host.ShowError("Marque pelo menos um tipo de evento.");
                return;
            }
            var criteria = new ItemCriteria { Prefab = PrefabBox.Text.Trim(), MinCount = min, Events = events };
            await _host.RunQueryAsync("Buscando itens...", (db, filter, token) => ItemQuery.Run(db, filter, criteria, token), Results);
        }
    }
}
```

- [ ] **Step 3: Frequency tab**

`src/ActivityViewer/Tabs/FrequencyTab.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Tabs.FrequencyTab"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ActivityViewer.Controls">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="Ações/seg máx.:" VerticalAlignment="Center" Margin="0,0,6,0" />
            <TextBox x:Name="LimitBox" Width="60" Text="7" />
            <Button Content="Buscar" Style="{StaticResource AccentButton}" Click="OnSearch" Margin="14,0,0,0" />
            <TextBlock Foreground="{StaticResource MutedBrush}" VerticalAlignment="Center" Margin="14,0,0,0"
                       Text="Conta Pickup, Place, Remove, Interact, Drop e Move de cada jogador em janelas de 1 segundo." />
        </WrapPanel>
        <controls:ResultView x:Name="Results" Grid.Row="1" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Tabs/FrequencyTab.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Tabs
{
    public partial class FrequencyTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public FrequencyTab()
        {
            InitializeComponent();
        }

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database) => Results.Clear();

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            if (!InputParser.TryParseInt(LimitBox.Text, out int limit) || limit < 1)
            {
                _host.ShowError("Limite inválido. Use um número inteiro a partir de 1.");
                return;
            }
            await _host.RunQueryAsync("Analisando ações por segundo...", (db, filter, token) => FrequencyQuery.Run(db, filter, limit, token), Results);
        }
    }
}
```

- [ ] **Step 4: Speed tab**

`src/ActivityViewer/Tabs/SpeedTab.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Tabs.SpeedTab"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ActivityViewer.Controls">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="Velocidade máx. (u/s):" VerticalAlignment="Center" Margin="0,0,6,0" />
            <TextBox x:Name="LimitBox" Width="70" Text="150" />
            <Button Content="Buscar" Style="{StaticResource AccentButton}" Click="OnSearch" Margin="14,0,0,0" />
            <TextBlock Foreground="{StaticResource MutedBrush}" VerticalAlignment="Center" Margin="14,0,0,0"
                       Text="Usa só eventos com a posição do próprio jogador; ignora teleporte, morte, respawn e reconexão." />
        </WrapPanel>
        <controls:ResultView x:Name="Results" Grid.Row="1" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Tabs/SpeedTab.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Tabs
{
    public partial class SpeedTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public SpeedTab()
        {
            InitializeComponent();
        }

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database) => Results.Clear();

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            if (!InputParser.TryParseNumber(LimitBox.Text, out double limit) || limit <= 0)
            {
                _host.ShowError("Velocidade inválida. Use um número maior que zero, por exemplo 150.");
                return;
            }
            await _host.RunQueryAsync("Analisando velocidade...", (db, filter, token) => SpeedQuery.Run(db, filter, limit, token), Results);
        }
    }
}
```

- [ ] **Step 5: Interactions tab**

`src/ActivityViewer/Tabs/InteractionsTab.xaml`:

```xml
<UserControl x:Class="ActivityViewer.Tabs.InteractionsTab"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ActivityViewer.Controls">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="Objeto:" VerticalAlignment="Center" Margin="0,0,6,0" />
            <ComboBox x:Name="ObjectBox" Width="260" IsEditable="True" />
            <TextBlock Text="Resultado:" VerticalAlignment="Center" Margin="14,0,6,0" />
            <ComboBox x:Name="ResultBox" Width="120" SelectedIndex="0">
                <ComboBoxItem Content="Todos" />
                <ComboBoxItem Content="Sucesso" />
                <ComboBoxItem Content="Falha" />
            </ComboBox>
            <Button Content="Buscar" Style="{StaticResource AccentButton}" Click="OnSearch" Margin="14,0,0,0" />
        </WrapPanel>
        <controls:ResultView x:Name="Results" Grid.Row="1" />
    </Grid>
</UserControl>
```

`src/ActivityViewer/Tabs/InteractionsTab.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core.Data;

namespace ActivityViewer.Tabs
{
    public partial class InteractionsTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public InteractionsTab()
        {
            InitializeComponent();
        }

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            ObjectBox.ItemsSource = database?.DistinctTargets(InteractionQuery.Events);
        }

        private async void OnSearch(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            InteractionResult result = ResultBox.SelectedIndex == 1 ? InteractionResult.Success
                : ResultBox.SelectedIndex == 2 ? InteractionResult.Failure
                : InteractionResult.Any;
            var criteria = new InteractionCriteria { Object = ObjectBox.Text.Trim(), Result = result };
            await _host.RunQueryAsync("Buscando interações...", (db, filter, token) => InteractionQuery.Run(db, filter, criteria, token), Results);
        }
    }
}
```

- [ ] **Step 6: Register the tabs in the main window**

In `src/ActivityViewer/MainWindow.xaml`, add `xmlns:tabs="clr-namespace:ActivityViewer.Tabs"` to the `<Window>` element and replace:

```xml
        <TabControl x:Name="Tabs" Grid.Row="2" Margin="0,8,0,0" />
```

with:

```xml
        <TabControl x:Name="Tabs" Grid.Row="2" Margin="0,8,0,0">
            <TabItem Header="Dano">
                <tabs:DamageTab x:Name="DamagePage" />
            </TabItem>
            <TabItem Header="Itens">
                <tabs:ItemsTab x:Name="ItemsPage" />
            </TabItem>
            <TabItem Header="Ações/seg">
                <tabs:FrequencyTab x:Name="FrequencyPage" />
            </TabItem>
            <TabItem Header="Velocidade">
                <tabs:SpeedTab x:Name="SpeedPage" />
            </TabItem>
            <TabItem Header="Interações">
                <tabs:InteractionsTab x:Name="InteractionsPage" />
            </TabItem>
        </TabControl>
```

In `src/ActivityViewer/MainWindow.xaml.cs`, replace:

```csharp
        private void RegisterPages()
        {
        }
```

with:

```csharp
        private void RegisterPages()
        {
            _pages.Add(DamagePage);
            _pages.Add(ItemsPage);
            _pages.Add(FrequencyPage);
            _pages.Add(SpeedPage);
            _pages.Add(InteractionsPage);
        }
```

- [ ] **Step 7: Build and smoke-run**

Run: `dotnet build src/ActivityViewer/ActivityViewer.csproj -c Debug`
Expected: 0 errors, 0 warnings.

Run the same PowerShell smoke check as Task 9 Step 11. Expected: `alive=True`, no new log file.

Run: `dotnet run --project tests/ActivityViewer.Tests/ActivityViewer.Tests.csproj`
Expected: `146 checks passed.`

- [ ] **Step 8: Checkpoint**

App builds and starts with five tabs; tests pass.

---

### Task 11: Single-file build, README and manual verification

**Files:**
- Create: `README.md`
- Output: `publish/Vikings_ActivityViewer.exe`

- [ ] **Step 1: Publish**

Run:

```powershell
cd "D:\UNITY VALHEIM\ActivityViewer"
dotnet publish src/ActivityViewer/ActivityViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
Get-ChildItem publish | Select-Object Name, Length
```

Expected: `publish\Vikings_ActivityViewer.exe` exists (roughly 60–90 MB) and is the only `.exe`/`.dll` in the folder.

- [ ] **Step 2: Smoke-run the published exe**

```powershell
$p = Start-Process "D:\UNITY VALHEIM\ActivityViewer\publish\Vikings_ActivityViewer.exe" -PassThru
Start-Sleep -Seconds 8
"alive=" + (-not $p.HasExited)
Stop-Process $p -ErrorAction SilentlyContinue
```

Expected: `alive=True`.

- [ ] **Step 3: Write the README**

`README.md`:

```markdown
# Vikings_ActivityViewer

Programa para Windows que lê o banco SQLite gerado pelo mod **Vikings_ActivityLog** e permite investigar a atividade dos jogadores.

## Instalação

Copie `Vikings_ActivityViewer.exe` para qualquer pasta e execute. Não é preciso instalar o .NET.

## Primeiro uso

1. Clique em **Perfis...** e crie um perfil para o servidor:
   - Protocolo: FTP, FTPS ou SFTP.
   - Host, porta, usuário e senha da hospedagem.
   - Pasta remota: a pasta do `-savedir` do servidor seguida de `/Vikings_ActivityLog` (ex.: `/SAVE/Vikings_ActivityLog`).
   - Use **Testar conexão** para conferir; ele lista os mundos (`.db`) encontrados.
2. Na janela principal, escolha o perfil, clique em **Listar mundos**, escolha o mundo e clique em **Atualizar**.
3. O programa baixa uma cópia consistente do banco (até 3 tentativas se o servidor estiver gravando) e abre a cópia.

A senha fica salva criptografada pelo Windows e só pode ser lida pelo seu usuário neste PC.
Cada admin cria os próprios perfis no próprio PC.

## Filtros

- **Jogador** e **período** (De / Até, formato `dd/MM/aaaa` ou `dd/MM/aaaa HH:mm`) valem para todas as abas.
- "Até" com apenas a data inclui o dia inteiro. Campo vazio = sem limite.

## Abas

- **Dano**: dano causado (Damage) e sofrido (Damaged), com tipos de dano e vida restante.
- **Itens**: itens pegos, largados, movidos, craftados, equipados e consumidos, com origem e destino.
- **Ações/seg**: jogadores com mais ações por segundo do que o limite (macro/cheat).
- **Velocidade**: deslocamentos impossíveis entre posições do próprio jogador (ignora teleporte, morte e respawn).
- **Interações**: interações com objetos, uso de itens e textos escritos.

Em todas as abas: ordenar clicando no cabeçalho, **Copiar ID** (SteamID da linha) e **Exportar** (CSV para Excel ou TXT, sempre com o resultado completo).

## Arquivos do programa

- Perfis: `%AppData%\Vikings_ActivityViewer\profiles.json`
- Cópias baixadas: `%LocalAppData%\Vikings_ActivityViewer\cache\`
- Registro de erros: `%LocalAppData%\Vikings_ActivityViewer\logs\`
```

- [ ] **Step 4: Manual verification (with the user)**

Each item is done by opening the published exe:

1. **Local file:** "Abrir arquivo local..." on a `.db` from the server that has events (play a few minutes with a client connected so the mod records events). Every tab returns rows; sorting by a column works; "Copiar ID" puts the SteamID in the clipboard; "Exportar" CSV opens correctly in Excel (accents, decimal comma).
2. **FTP / FTPS / SFTP:** create a profile for the real host, "Testar conexão" lists the world, "Atualizar" downloads and opens it. For FTPS with a self-signed certificate and for the first SFTP connection, the trust question appears once and is not asked again after saving.
3. **Wrong password:** shows the server message; the previous copy stays open.
4. **Refresh while players are online:** download succeeds (or retries) and the copy date in the top bar changes.
5. **Filters:** player filter, "De"/"Até" with and without time, invalid date message.
6. **Cancel:** start a download or a large search and click "Cancelar": status shows the cancellation and the app keeps working.

Record the result of each item in the ledger. Items that need a server the user does not have are recorded as pending.

- [ ] **Step 5: Checkpoint**

Tests pass (`146 checks passed.`), publish produced the single exe, README written, manual checks recorded.
