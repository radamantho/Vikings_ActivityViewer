using System.Collections.Generic;
using System.Globalization;

namespace ActivityViewer.Core.Localization
{
    public enum Language
    {
        Portuguese,
        English
    }

    public static class Lang
    {
        private static IReadOnlyDictionary<string, string> _table = Texts.Portuguese;

        public static Language Current { get; private set; } = Language.Portuguese;

        public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("pt-BR");

        public static void Use(Language language)
        {
            Current = language;
            _table = language == Language.English ? Texts.English : Texts.Portuguese;
            Culture = CultureInfo.GetCultureInfo(language == Language.English ? "en-US" : "pt-BR");
        }

        public static string T(string key) => _table.TryGetValue(key, out string? value) ? value : "[" + key + "]";

        public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);

        public static Language FromCulture(CultureInfo culture) =>
            culture.TwoLetterISOLanguageName == "pt" ? Language.Portuguese : Language.English;
    }
}
