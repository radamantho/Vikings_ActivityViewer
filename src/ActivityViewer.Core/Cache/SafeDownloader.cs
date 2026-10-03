using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Transfer;

using ActivityViewer.Core.Localization;

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
                    if (before == null) return new DownloadResult(false, CachedCopy.Load(worldFolder), Lang.F("download.worldNotFound", worldFile), attempt);
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
                            lastProblem = Lang.F("download.walChanged", error.Message);
                            continue;
                        }
                    }

                    RemoteFileInfo? after = await remote.StatAsync(worldFile, token).ConfigureAwait(false);
                    if (after == null || after.Size != before.Size || after.Modified != before.Modified)
                    {
                        lastProblem = Lang.T("download.dbChanged");
                        continue;
                    }

                    string check = _quickCheck(localDb);
                    if (check != "ok")
                    {
                        lastProblem = Lang.F("download.checkFailed", check);
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
                    return new DownloadResult(true, copy, Lang.T("download.success"), attempt);
                }
                finally
                {
                    if (!promoted) TryDelete(copyFolder);
                }
            }

            CachedCopy? previous = CachedCopy.Load(worldFolder);
            string fallback = previous != null
                ? Lang.F("download.usingPrevious", previous.DownloadedLocal.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture))
                : Lang.T("download.noPrevious");
            return new DownloadResult(false, previous,
                Lang.F("download.failed", fallback, lastProblem), MaxAttempts);
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
