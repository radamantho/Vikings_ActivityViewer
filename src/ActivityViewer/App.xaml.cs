using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using ActivityViewer.Core;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Settings;

namespace ActivityViewer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppSettings settings = SettingsStore.Default().Load(CultureInfo.CurrentUICulture);
            Lang.Use(settings.Language);
            ActivityViewer.Core.Cache.CachePaths.UseRoot(settings.CacheRoot);
            DarkTitleBar.Register();
            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string log = ErrorLog.Write(e.Exception, Lang.T("log.unhandled"));
            MessageBox.Show(Lang.F("app.unhandled", e.Exception.Message) + (log.Length > 0 ? Lang.F("main.detailsIn", log) : ""),
                "Vikings Activity Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
