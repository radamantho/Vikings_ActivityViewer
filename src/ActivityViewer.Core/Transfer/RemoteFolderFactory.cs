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
