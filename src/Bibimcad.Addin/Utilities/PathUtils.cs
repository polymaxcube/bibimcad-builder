using System.IO;

namespace Bibimcad.Utilities
{
    public static class PathUtils
    {
        public static string GetProjectRoot()
        {
            return @"C:\Bibimcad";
        }

        public static string GetDataDirectory()
        {
            return Path.Combine(GetProjectRoot(), "data");
        }

        public static string GetLogsDirectory()
        {
            return Path.Combine(GetProjectRoot(), "logs");
        }

        public static string GetProjectStatePath()
        {
            return Path.Combine(GetDataDirectory(), "project_state.json");
        }

        public static string GetLogPath()
        {
            return Path.Combine(GetLogsDirectory(), "Bibimcad.log");
        }
    }
}
