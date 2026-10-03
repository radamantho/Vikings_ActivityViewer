using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Cache
{
    public sealed class CachedCopy
    {
        private const string FileName = "current.json";

        public string WorldFile { get; set; } = "";

        public string Folder { get; set; } = "";

        public DateTime DownloadedLocal { get; set; }

        public long DatabaseSize { get; set; }

        public long WalSize { get; set; }

        public string DatabasePathIn(string worldFolder) => Path.Combine(worldFolder, Folder, WorldFile);

        public string Describe()
        {
            double megabytes = (DatabaseSize + WalSize) / (1024.0 * 1024.0);
            return Lang.F("cache.describe", DownloadedLocal.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture), megabytes);
        }

        public static CachedCopy? Load(string worldFolder)
        {
            string path = Path.Combine(worldFolder, FileName);
            if (!File.Exists(path)) return null;
            try
            {
                CachedCopy? copy = JsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8), CacheJsonContext.Default.CachedCopy);
                if (copy == null || copy.Folder.Length == 0 || copy.WorldFile.Length == 0) return null;
                return File.Exists(copy.DatabasePathIn(worldFolder)) ? copy : null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Save(string worldFolder)
        {
            Directory.CreateDirectory(worldFolder);
            string path = Path.Combine(worldFolder, FileName);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, CacheJsonContext.Default.CachedCopy), new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(CachedCopy))]
    internal partial class CacheJsonContext : JsonSerializerContext
    {
    }
}
