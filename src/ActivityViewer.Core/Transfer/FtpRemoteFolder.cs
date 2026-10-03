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

using ActivityViewer.Core.Localization;

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
            if (status != FtpStatus.Success) throw new IOException(Lang.F("transfer.downloadFailed", path));
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
