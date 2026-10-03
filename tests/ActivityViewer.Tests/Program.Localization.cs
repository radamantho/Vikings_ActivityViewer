using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunLocalization()
        {
            Localization_TablesAreComplete();
            Localization_SwitchesLanguage();
            Localization_DetectsWindowsLanguage();
            Localization_ResultsInEnglish();
            Localization_FormatsInEnglish();
            Localization_MessagesInEnglish();
            Settings_LoadAndSave();
        }

        private static void Settings_LoadAndSave()
        {
            string dir = TempDir("settings");
            var store = new ActivityViewer.Core.Settings.SettingsStore(System.IO.Path.Combine(dir, "settings.json"));
            Check(store.Load(CultureInfo.GetCultureInfo("en-US")).Language == Language.English, "Settings: first run follows the Windows language (English)");
            Check(store.Load(CultureInfo.GetCultureInfo("pt-BR")).Language == Language.Portuguese, "Settings: first run follows the Windows language (Portuguese)");

            store.Save(new ActivityViewer.Core.Settings.AppSettings { Language = Language.English });
            Check(store.Load(CultureInfo.GetCultureInfo("pt-BR")).Language == Language.English, "Settings: saved language wins over Windows language");

            System.IO.File.WriteAllText(store.FilePath, "{\"Language\":\"Portuguese\"}");
            Check(store.Load(CultureInfo.GetCultureInfo("en-US")).Language == Language.Portuguese, "Settings: file written by the installer is read");

            System.IO.File.WriteAllText(store.FilePath, "{ quebrado");
            Check(store.Load(CultureInfo.GetCultureInfo("en-US")).Language == Language.English, "Settings: corrupt file falls back to the Windows language");
        }

        private static void Localization_MessagesInEnglish()
        {
            string newer = BuildDb("english_newer");
            using (var writer = new ActivityViewer.Core.Sqlite.SqliteDatabase(newer, readOnly: false)) writer.Execute("UPDATE schema_info SET version = 2;");

            InEnglish(() =>
            {
                string message = "";
                try { using ActivityViewer.Core.Data.ActivityDatabase db = ActivityViewer.Core.Data.ActivityDatabase.Open(newer); }
                catch (ActivityViewer.Core.Data.InvalidActivityDatabaseException error) { message = error.Message; }
                Check(message == "This database comes from a newer version of the mod. Update the Viewer.", "Localization: database errors in English");

                var profile = new ActivityViewer.Core.Profiles.ServerProfile { Name = "A", Host = " ", User = "u", RemoteFolder = "/" };
                Check(profile.Validate() == "Enter the host.", "Localization: profile validation in English");

                string folder = FreshWorldFolder("English");
                ActivityViewer.Core.Cache.DownloadResult result = Download(new FakeRemote(), folder);
                Check(result.Message == "World not found on the server: world.db.", "Localization: download messages in English");
            });
        }

        private static void Localization_FormatsInEnglish()
        {
            InEnglish(() =>
            {
                Check(ActivityViewer.Core.Data.TimeFormat.Pattern == "yyyy-MM-dd HH:mm:ss", "Localization: ISO date pattern in English");

                var writer = new System.IO.StringWriter { NewLine = "\r\n" };
                ActivityViewer.Core.Export.ResultExporter.WriteCsv(ExportSample(), writer);
                string expected =
                    "Time,Player,Total,Quantity,Health after\r\n" +
                    "2026-10-02 13:05:09,\"Þór; \"\"o\"\" 🙂\nlinha2\",35.5,3,\r\n" +
                    "2026-10-02 13:05:09,Bjorn,2,10,1.25\r\n";
                Check(writer.ToString() == expected, "Localization: English CSV uses ',' and decimal point");

                Check(ActivityViewer.Core.InputParser.TryParseNumber("2.5", out double dot) && dot == 2.5, "Localization: English accepts decimal point");
                Check(!ActivityViewer.Core.InputParser.TryParseNumber("1,000", out _), "Localization: English refuses comma numbers");
                Check(ActivityViewer.Core.InputParser.TryParseStart("2026-10-01 14:30", out System.DateTime? start) && start == new System.DateTime(2026, 10, 1, 14, 30, 0), "Localization: English date and time");
                Check(ActivityViewer.Core.InputParser.TryParseEnd("2026-10-01", out System.DateTime? end) && end == new System.DateTime(2026, 10, 1, 23, 59, 59, 999), "Localization: English date-only end covers the day");
                Check(!ActivityViewer.Core.InputParser.TryParseStart("01/10/2026", out _), "Localization: English refuses dd/MM/yyyy");

                var copy = new ActivityViewer.Core.Cache.CachedCopy { DownloadedLocal = new System.DateTime(2026, 10, 2, 13, 5, 9), DatabaseSize = 1048576, WalSize = 524288 };
                Check(copy.Describe() == "Copy from 2026-10-02 13:05:09 (1.5 MB)", "Localization: cached copy description in English");
            });

            var portugueseCopy = new ActivityViewer.Core.Cache.CachedCopy { DownloadedLocal = new System.DateTime(2026, 10, 2, 13, 5, 9), DatabaseSize = 1048576, WalSize = 524288 };
            Check(portugueseCopy.Describe() == "Cópia de 02/10/2026 13:05:09 (1,5 MB)", "Localization: cached copy description in Portuguese");
        }

        private static void Localization_ResultsInEnglish()
        {
            Vikings_ActivityLog.ActivityRecord boar = Rec(Vikings_ActivityLog.ActivityEventType.Damage, 0, target: "Boar");
            boar.Damage = new Vikings_ActivityLog.ActivityDamage { Slash = 30f, Fire = 2.5f, Total = 32.5f };
            using ActivityViewer.Core.Data.ActivityDatabase db = ActivityViewer.Core.Data.ActivityDatabase.Open(BuildDb("english", boar));

            InEnglish(() =>
            {
                ActivityViewer.Core.Data.ResultTable table = ActivityViewer.Core.Data.DamageQuery.Run(db, ActivityViewer.Core.Data.QueryFilter.All, new ActivityViewer.Core.Data.DamageCriteria(), default);
                Check(table.Columns[0].Header == "Time" && table.Columns[table.IndexOf("Target")].Header == "Target or attacker", "Localization: column headers in English, keys unchanged");
                Check((string?)table.Value(0, "Types") == "Slash 30 · Fire 2.5", "Localization: damage types in English");
                Check(ActivityViewer.Core.Data.RouteParser.Parse("Drop", "Inventory", "") == ("Inventory", "Ground"), "Localization: route values in English");
                Check(ActivityViewer.Core.Data.InteractionQuery.ParseDetails("Interact", "result:True") == ("Success", ""), "Localization: interaction result in English");
            });

            ActivityViewer.Core.Data.ResultTable portuguese = ActivityViewer.Core.Data.DamageQuery.Run(db, ActivityViewer.Core.Data.QueryFilter.All, new ActivityViewer.Core.Data.DamageCriteria(), default);
            Check(portuguese.Columns[0].Header == "Data" && portuguese.Columns[portuguese.IndexOf("Target")].Header == "Alvo ou atacante", "Localization: column headers in Portuguese");
            Check((string?)portuguese.Value(0, "Types") == "Corte 30 · Fogo 2,5", "Localization: damage types use the Portuguese decimal comma");
        }

        private static void InEnglish(System.Action action)
        {
            Lang.Use(Language.English);
            try { action(); }
            finally { Lang.Use(Language.Portuguese); }
        }

        private static void Localization_TablesAreComplete()
        {
            IReadOnlyDictionary<string, string> pt = Texts.Portuguese;
            IReadOnlyDictionary<string, string> en = Texts.English;
            List<string> missingInEnglish = pt.Keys.Where(k => !en.ContainsKey(k)).ToList();
            List<string> missingInPortuguese = en.Keys.Where(k => !pt.ContainsKey(k)).ToList();
            Check(missingInEnglish.Count == 0, "Localization: every Portuguese key exists in English " + string.Join(",", missingInEnglish));
            Check(missingInPortuguese.Count == 0, "Localization: every English key exists in Portuguese " + string.Join(",", missingInPortuguese));
            Check(pt.Values.All(v => v.Trim().Length > 0) && en.Values.All(v => v.Trim().Length > 0), "Localization: no empty texts");

            var placeholder = new Regex(@"\{(\d+)\}");
            List<string> mismatched = pt.Keys.Where(en.ContainsKey).Where(k =>
                !placeholder.Matches(pt[k]).Select(m => m.Value).Distinct().OrderBy(v => v)
                    .SequenceEqual(placeholder.Matches(en[k]).Select(m => m.Value).Distinct().OrderBy(v => v))).ToList();
            Check(mismatched.Count == 0, "Localization: placeholders match in both languages " + string.Join(",", mismatched));
        }

        private static void Localization_SwitchesLanguage()
        {
            Check(Lang.Current == Language.Portuguese && Lang.T("lang.name") == "Português", "Localization: Portuguese is the default");
            InEnglish(() =>
            {
                Check(Lang.T("lang.name") == "English" && Lang.Culture.Name == "en-US", "Localization: English texts and culture");
                Check(Lang.F("common.value", 1.5) == "Value 1.5", "Localization: formatting uses the English culture");
            });
            Check(Lang.F("common.value", 1.5) == "Valor 1,5", "Localization: formatting uses the Portuguese culture");
            Check(Lang.T("missing.key") == "[missing.key]", "Localization: unknown keys are visible, not blank");
        }

        private static void Localization_DetectsWindowsLanguage()
        {
            Check(Lang.FromCulture(CultureInfo.GetCultureInfo("pt-BR")) == Language.Portuguese && Lang.FromCulture(CultureInfo.GetCultureInfo("pt-PT")) == Language.Portuguese, "Localization: Portuguese Windows uses Portuguese");
            Check(Lang.FromCulture(CultureInfo.GetCultureInfo("en-US")) == Language.English && Lang.FromCulture(CultureInfo.GetCultureInfo("es-ES")) == Language.English, "Localization: other languages use English");
        }
    }
}
