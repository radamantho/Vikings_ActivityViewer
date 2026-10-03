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
