using System.Windows;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Windows
{
    public partial class PasswordWindow : Window
    {
        public PasswordWindow(string profileName, bool savedPasswordFailed)
        {
            InitializeComponent();
            MessageText.Text = (savedPasswordFailed ? Lang.T("password.savedFailed") : "") + Lang.F("password.prompt", profileName);
            Loaded += (sender, e) => PasswordInput.Focus();
        }

        public string Password => PasswordInput.Password;

        public bool SavePassword => SaveCheck.IsChecked == true;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (PasswordInput.Password.Length == 0)
            {
                MessageBox.Show(this, Lang.T("password.empty"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
    }
}
