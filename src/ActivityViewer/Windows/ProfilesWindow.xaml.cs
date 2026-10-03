using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Profiles;
using ActivityViewer.Core.Transfer;

namespace ActivityViewer.Windows
{
    public partial class ProfilesWindow : Window
    {
        private readonly ProfileStore _store;
        private readonly List<ServerProfile> _profiles;
        private ServerProfile? _current;
        private string _certificate = "";
        private string _hostKey = "";
        private bool _filling;

        public ProfilesWindow(ProfileStore store)
        {
            InitializeComponent();
            _store = store;
            _profiles = store.Load();
            ProtocolBox.ItemsSource = new[] { TransferProtocol.Ftp, TransferProtocol.Ftps, TransferProtocol.Sftp };
            if (_profiles.Count == 0) StartNew();
            else RefreshList(_profiles[0]);
        }

        public string? SelectedName { get; private set; }

        private void RefreshList(ServerProfile? select)
        {
            ProfileList.ItemsSource = null;
            ProfileList.ItemsSource = _profiles;
            ProfileList.SelectedItem = select;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileList.SelectedItem is ServerProfile profile) Fill(profile, profile);
        }

        private void Fill(ServerProfile values, ServerProfile? current)
        {
            _filling = true;
            _current = current;
            NameBox.Text = values.Name;
            ProtocolBox.SelectedItem = values.Protocol;
            HostBox.Text = values.Host;
            PortBox.Text = values.Port.ToString(CultureInfo.InvariantCulture);
            UserBox.Text = values.User;
            PasswordInput.Password = "";
            SaveCheck.IsChecked = true;
            FolderBox.Text = values.RemoteFolder;
            _certificate = values.TrustedCertificateThumbprint;
            _hostKey = values.TrustedHostKeyFingerprint;
            PasswordHint.Text = Lang.T(values.EncryptedPassword.Length > 0 ? "profiles.passwordSaved" : "profiles.noPassword");
            UpdateTrustText();
            StatusText.Text = "";
            _filling = false;
        }

        private void StartNew()
        {
            ProfileList.SelectedItem = null;
            Fill(new ServerProfile { RemoteFolder = "/SAVE/Vikings_ActivityLog" }, null);
            NameBox.Focus();
        }

        private void OnNew(object sender, RoutedEventArgs e) => StartNew();

        private void OnProtocolChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || !(ProtocolBox.SelectedItem is TransferProtocol protocol)) return;
            string port = PortBox.Text.Trim();
            if (port.Length == 0 || port == "21" || port == "22") PortBox.Text = ServerProfile.DefaultPort(protocol).ToString(CultureInfo.InvariantCulture);
        }

        private ServerProfile? ReadForm()
        {
            if (!int.TryParse(PortBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int port)) port = 0;
            var profile = new ServerProfile
            {
                Name = NameBox.Text.Trim(),
                Protocol = ProtocolBox.SelectedItem is TransferProtocol protocol ? protocol : TransferProtocol.Ftp,
                Host = HostBox.Text.Trim(),
                Port = port,
                User = UserBox.Text.Trim(),
                RemoteFolder = FolderBox.Text.Trim(),
                EncryptedPassword = _current?.EncryptedPassword ?? "",
                TrustedCertificateThumbprint = _certificate,
                TrustedHostKeyFingerprint = _hostKey
            };

            string? error = profile.Validate();
            if (error == null && _profiles.Any(other => !ReferenceEquals(other, _current) && string.Equals(other.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
                error = Lang.T("profiles.duplicate");
            if (error != null)
            {
                MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (PasswordInput.Password.Length > 0)
                profile.EncryptedPassword = SaveCheck.IsChecked == true ? PasswordProtector.Protect(PasswordInput.Password) : "";
            else if (SaveCheck.IsChecked != true)
                profile.EncryptedPassword = "";
            return profile;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = ReadForm();
            if (profile == null) return;
            if (_current == null) _profiles.Add(profile);
            else _profiles[_profiles.IndexOf(_current)] = profile;
            _store.Save(_profiles);
            SelectedName = profile.Name;
            RefreshList(profile);
            StatusText.Text = Lang.T("profiles.saved");
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (!(ProfileList.SelectedItem is ServerProfile profile)) return;
            string question = Lang.F("profiles.deleteQuestion", profile.Name);
            if (MessageBox.Show(this, question, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _profiles.Remove(profile);
            _store.Save(_profiles);
            if (SelectedName == profile.Name) SelectedName = null;
            if (_profiles.Count == 0) StartNew();
            else RefreshList(_profiles[0]);
        }

        private void OnClearTrust(object sender, RoutedEventArgs e)
        {
            _certificate = "";
            _hostKey = "";
            UpdateTrustText();
            StatusText.Text = Lang.T("profiles.trustCleared");
        }

        private void UpdateTrustText()
        {
            TrustText.Text = _certificate.Length > 0 ? Lang.F("profiles.trustedCertificate", _certificate)
                : _hostKey.Length > 0 ? Lang.F("profiles.trustedKey", _hostKey)
                : Lang.T("profiles.noTrust");
        }

        private async void OnTest(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = ReadForm();
            if (profile == null) return;
            string password = PasswordInput.Password;
            if (password.Length == 0 && !PasswordProtector.TryUnprotect(profile.EncryptedPassword, out password))
            {
                MessageBox.Show(this, Lang.T("profiles.passwordForTest"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestButton.IsEnabled = false;
            StatusText.Text = Lang.T("profiles.testing");
            try
            {
                var prompt = new WpfTrustPrompt(this);
                IReadOnlyList<RemoteFileInfo> files = await Task.Run(async () =>
                {
                    using IRemoteFolder remote = RemoteFolderFactory.Create(profile, password, prompt);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await remote.ConnectAsync(timeout.Token);
                    return await remote.ListDatabasesAsync(timeout.Token);
                });

                bool trustChanged = profile.TrustedCertificateThumbprint != _certificate || profile.TrustedHostKeyFingerprint != _hostKey;
                _certificate = profile.TrustedCertificateThumbprint;
                _hostKey = profile.TrustedHostKeyFingerprint;
                UpdateTrustText();

                string list = files.Count == 0
                    ? Lang.F("profiles.noDb", profile.RemoteFolder)
                    : Lang.F("profiles.worldsList", files.Count, string.Join(", ", files.Select(f => f.Name)));
                MessageBox.Show(this, Lang.T("profiles.connectionOk") + "\n\n" + list + (trustChanged ? Lang.T("profiles.saveTrustHint") : ""),
                    Title, MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = Lang.T("profiles.connectionOk");
            }
            catch (Exception error)
            {
                ErrorLog.Write(error, Lang.F("log.testConnection", profile.Host));
                MessageBox.Show(this, Lang.F("profiles.connectionFailed", error.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = Lang.T("profiles.connectionFailedStatus");
            }
            finally
            {
                TestButton.IsEnabled = true;
            }
        }
    }
}
