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
