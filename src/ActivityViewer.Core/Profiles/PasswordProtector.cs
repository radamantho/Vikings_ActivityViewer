using System;
using System.Security.Cryptography;
using System.Text;

namespace ActivityViewer.Core.Profiles
{
    public static class PasswordProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Vikings_ActivityViewer.v1");

        public static string Protect(string password)
        {
            if (password.Length == 0) return "";
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static bool TryUnprotect(string encrypted, out string password)
        {
            password = "";
            if (encrypted.Length == 0) return false;
            try
            {
                byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
                password = Encoding.UTF8.GetString(plain);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
