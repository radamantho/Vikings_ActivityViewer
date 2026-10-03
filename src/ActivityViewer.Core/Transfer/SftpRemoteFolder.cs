using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Core.Profiles;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

using ActivityViewer.Core.Localization;

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
                throw new HostKeyChangedException(Lang.T("transfer.hostKeyChanged"));
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
