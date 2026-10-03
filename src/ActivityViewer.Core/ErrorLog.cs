using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ActivityViewer.Core
{
    public static class ErrorLog
    {
        private static readonly object Gate = new object();

        public static string Write(Exception error, string context)
        {
            try
            {
                string directory = Path.Combine(AppPaths.LocalDataRoot, "logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "viewer-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + context +
                    Environment.NewLine + error + Environment.NewLine + Environment.NewLine;
                lock (Gate) File.AppendAllText(path, entry, Encoding.UTF8);
                return path;
            }
            catch (IOException)
            {
                return "";
            }
            catch (UnauthorizedAccessException)
            {
                return "";
            }
        }
    }
}
