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
