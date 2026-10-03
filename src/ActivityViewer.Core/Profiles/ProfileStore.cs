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
