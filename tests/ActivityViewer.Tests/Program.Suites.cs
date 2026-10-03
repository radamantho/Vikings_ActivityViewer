namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private static void RunAll()
        {
            RunSqlite();
            RunDatabase();
            RunDamageAndItems();
            RunInteractionsAndAnalyses();
            RunExport();
            RunProfiles();
            RunTransfer();
            RunDownload();
            RunInput();
            RunLocalization();
            RunCacheMaintenance();
        }

        static partial void RunSqlite();
        static partial void RunDatabase();
        static partial void RunDamageAndItems();
        static partial void RunInteractionsAndAnalyses();
        static partial void RunExport();
        static partial void RunProfiles();
        static partial void RunTransfer();
        static partial void RunDownload();
        static partial void RunInput();
        static partial void RunLocalization();
        static partial void RunCacheMaintenance();
    }
}
