using System.Windows;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer
{
    internal sealed class WpfTrustPrompt : ITrustPrompt
    {
        private readonly Window _owner;

        internal WpfTrustPrompt(Window owner)
        {
            _owner = owner;
        }

        public bool TrustCertificate(string host, string thumbprint, string subject, bool changed)
        {
            string intro = Lang.T(changed ? "trust.changed" : "trust.unknown");
            return Ask(intro + Lang.F("trust.certificate", host, subject, thumbprint));
        }

        public bool TrustHostKey(string host, string fingerprint)
        {
            return Ask(Lang.F("trust.hostKey", host, fingerprint));
        }

        private bool Ask(string text)
        {
            return _owner.Dispatcher.Invoke(() =>
                MessageBox.Show(_owner, text, Lang.T("trust.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
        }
    }
}
