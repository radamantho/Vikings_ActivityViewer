using System;
using System.IO;

namespace ActivityViewer.Core
{
    public static class AppPaths
    {
        private const string FolderName = "Vikings_ActivityViewer";

        private static string? _roamingOverride;
        private static string? _localOverride;

        public static string RoamingRoot =>
            _roamingOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        public static string LocalDataRoot =>
            _localOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

        public static void Override(string roaming, string local)
        {
            _roamingOverride = roaming;
            _localOverride = local;
        }
    }
}
