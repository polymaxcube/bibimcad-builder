using System;
using System.IO;

namespace Bibimcad.Tests
{
    internal static class TestPaths
    {
        public static string RepositoryRoot
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Bibimcad.sln")))
                    directory = directory.Parent;

                if (directory == null)
                    throw new InvalidOperationException("Bibimcad.sln was not found above the test output folder.");

                return directory.FullName;
            }
        }

        public static string TemplateStatePath
        {
            get { return Path.Combine(RepositoryRoot, "data", "project_state.json"); }
        }

        public static string SampleDxfPath
        {
            get { return Path.Combine(RepositoryRoot, "tests", "data", "sample_walls.dxf"); }
        }
    }
}
