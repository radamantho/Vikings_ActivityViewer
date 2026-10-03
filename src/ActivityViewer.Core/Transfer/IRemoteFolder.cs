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
