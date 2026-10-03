using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Settings
{
    public sealed class AppSettings
    {
        public Language Language { get; set; } = Language.Portuguese;

        public string CacheRoot { get; set; } = "";
    }

    public sealed class SettingsStore
    {
        public SettingsStore(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public static SettingsStore Default() => new SettingsStore(Path.Combine(AppPaths.RoamingRoot, "settings.json"));

        public AppSettings Load(CultureInfo windowsCulture)
        {
            var fallback = new AppSettings { Language = Lang.FromCulture(windowsCulture) };
            if (!File.Exists(FilePath)) return fallback;
            try
            {
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath, Encoding.UTF8), SettingsJsonContext.Default.AppSettings) ?? fallback;
            }
            catch (JsonException)
            {
                return fallback;
            }
            catch (IOException)
            {
                return fallback;
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings), new UTF8Encoding(false));
            File.Move(temp, FilePath, true);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(AppSettings))]
    internal partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
