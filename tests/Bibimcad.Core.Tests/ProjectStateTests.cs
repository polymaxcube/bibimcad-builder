using System;
using Bibimcad.Models;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class ProjectStateTests
    {
        [Fact]
        public void Upgrade_FillsMissingSectionsOfAVersionOneFile()
        {
            ProjectState state = new ProjectState
            {
                SchemaVersion = "1.0",
                CadSource = new CadSourceInfo { WallLayers = null },
                WallCandidates = null
            };

            state.UpgradeToCurrentSchema();

            Assert.Equal(ProjectState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.NotNull(state.CadSource.WallLayers);
            Assert.NotNull(state.CadAudit.Layers);
            Assert.NotNull(state.WallCandidates);
        }

        [Fact]
        public void Upgrade_RejectsUnknownVersions()
        {
            ProjectState state = new ProjectState { SchemaVersion = "9.9" };

            Assert.Throws<InvalidOperationException>(() => state.UpgradeToCurrentSchema());
        }

        [Fact]
        public void Template_IsTheCurrentSchema()
        {
            ProjectState state = new JsonProjectRepository(TestPaths.TemplateStatePath).Load();

            Assert.Equal(ProjectState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.NotNull(state.CadSource.WallLayers);
            Assert.NotNull(state.WallCandidates);
        }
    }
}
