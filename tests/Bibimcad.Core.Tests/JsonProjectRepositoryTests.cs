using System;
using System.IO;
using Bibimcad.Models;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class JsonProjectRepositoryTests : IDisposable
    {
        private readonly string _directory;

        public JsonProjectRepositoryTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "BibimcadTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Fact]
        public void Load_ReadsTheRepositoryTemplate()
        {
            JsonProjectRepository repository = new JsonProjectRepository(TestPaths.TemplateStatePath);

            ProjectState state = repository.Load();

            Assert.Equal("K1.dwg", state.CadSource.DwgName);
            Assert.Equal(1.0, state.CadSource.MmPerCadUnit);
            Assert.True(state.CadSource.UnitCalibrationApproved);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsTheState()
        {
            string path = Path.Combine(_directory, "project_state.json");
            JsonProjectRepository repository = new JsonProjectRepository(path);
            ProjectState state = new JsonProjectRepository(TestPaths.TemplateStatePath).Load();
            state.Project.ProjectName = "Round trip";

            repository.Save(state);
            ProjectState loaded = repository.Load();

            Assert.Equal("Round trip", loaded.Project.ProjectName);
            Assert.Equal(state.CadSource.DxfPath, loaded.CadSource.DxfPath);
        }

        [Fact]
        public void Load_MissingFile_Throws()
        {
            JsonProjectRepository repository = new JsonProjectRepository(Path.Combine(_directory, "missing.json"));

            Assert.Throws<FileNotFoundException>(() => repository.Load());
        }
    }
}
