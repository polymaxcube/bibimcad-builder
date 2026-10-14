using System;
using System.IO;
using System.Linq;
using Bibimcad.Models;
using Bibimcad.Services;
using netDxf;
using Xunit;

namespace Bibimcad.Tests
{
    public class DxfAuditServiceTests
    {
        private static CadSourceInfo MillimeterSource()
        {
            return new CadSourceInfo
            {
                DxfPath = TestPaths.SampleDxfPath,
                ApprovedCoordinateUnit = "Millimeters",
                MmPerCadUnit = 1.0,
                UnitCalibrationApproved = true
            };
        }

        private static CadAuditInfo AuditSample(CadSourceInfo source)
        {
            DxfDocument document = DxfLoader.Load(TestPaths.SampleDxfPath);
            return new DxfAuditService().Audit(document, TestPaths.SampleDxfPath, source, new DateTime(2026, 10, 14, 9, 30, 0));
        }

        [Fact]
        public void Audit_CountsEntitiesByDxfType()
        {
            CadAuditInfo audit = AuditSample(MillimeterSource());

            Assert.Equal(6, audit.EntityCounts["LINE"]);
            Assert.Equal(2, audit.EntityCounts["LWPOLYLINE"]);
            Assert.Equal(1, audit.EntityCounts["INSERT"]);
        }

        [Fact]
        public void Audit_CountsEntitiesPerLayer()
        {
            CadAuditInfo audit = AuditSample(MillimeterSource());

            Assert.Equal(7, audit.Layers.Single(l => l.Name == "A-WALL").EntityCount);
            Assert.Equal(1, audit.Layers.Single(l => l.Name == "A-ANNO").EntityCount);
            Assert.Equal(1, audit.Layers.Single(l => l.Name == "A-DOOR").EntityCount);
            Assert.Equal(audit.LayerCount, audit.Layers.Count);
        }

        [Fact]
        public void Audit_RecordsHeaderUnitsVersionAndUserBlocks()
        {
            CadSourceInfo source = MillimeterSource();

            CadAuditInfo audit = AuditSample(source);

            Assert.Equal("Millimeters", source.HeaderInsUnits);
            Assert.Equal("AutoCad2000", audit.DxfVersion);
            Assert.Equal(1, audit.BlockCount);
            Assert.Equal("2026-10-14T09:30:00", audit.ReadAt);
        }

        [Fact]
        public void Audit_WarnsWhenHeaderDisagreesWithApprovedUnit()
        {
            CadSourceInfo source = MillimeterSource();
            source.ApprovedCoordinateUnit = "Inches";

            CadAuditInfo audit = AuditSample(source);

            Assert.Contains(audit.Warnings, w => w.Contains("INSUNITS is Millimeters") && w.Contains("approved coordinate unit is Inches"));
            Assert.Equal("Inches", source.ApprovedCoordinateUnit);
        }

        [Fact]
        public void Audit_WarnsAboutBlockReferences()
        {
            CadAuditInfo audit = AuditSample(MillimeterSource());

            Assert.Contains(audit.Warnings, w => w.Contains("1 block references"));
        }

        [Fact]
        public void Load_RejectsDwgFiles()
        {
            string path = Path.Combine(Path.GetTempPath(), "Bibimcad_" + Guid.NewGuid().ToString("N") + ".dwg");
            File.WriteAllText(path, "not a dxf");
            try
            {
                InvalidDataException ex = Assert.Throws<InvalidDataException>(() => DxfLoader.Load(path));
                Assert.Contains("DXF, not DWG", ex.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_MissingFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() => DxfLoader.Load(@"C:\does\not\exist.dxf"));
        }
    }
}
