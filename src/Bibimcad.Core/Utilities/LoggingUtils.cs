using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Bibimcad.Utilities
{
    public static class LoggingUtils
    {
        private static readonly object Sync = new object();
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception ex)
        {
            string text = message ?? string.Empty;
            if (ex != null)
                text = text + " | " + ex;

            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss} | {1} | {2}",
                DateTime.Now,
                level,
                message ?? string.Empty);

            string path = PathUtils.GetLogPath();
            string directory = Path.GetDirectoryName(path);
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(path, line + Environment.NewLine, Utf8NoBom);
            }
        }
    }
}
