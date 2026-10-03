using ActivityViewer.Core.Localization;

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
            if (Name.Trim().Length == 0) return Lang.T("profile.nameRequired");
            if (Host.Trim().Length == 0) return Lang.T("profile.hostRequired");
            if (Port < 1 || Port > 65535) return Lang.T("profile.portInvalid");
            if (User.Trim().Length == 0) return Lang.T("profile.userRequired");
            if (RemoteFolder.Trim().Length == 0) return Lang.T("profile.folderRequired");
            return null;
        }

        public ServerProfile Clone() => (ServerProfile)MemberwiseClone();

        public override string ToString() => Name;
    }
}
