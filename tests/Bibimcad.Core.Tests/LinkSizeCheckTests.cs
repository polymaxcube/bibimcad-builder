using System;
using System.Collections.Generic;
using Bibimcad.Models;
using Bibimcad.Services;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;
using Xunit;

namespace Bibimcad.Tests
{
    public class LinkSizeCheckTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 15, 12, 0, 0);

        private static CadExtents Box(double minX, double minY, double maxX, double maxY)
        {
            CadExtents extents = new CadExtents();
            extents.Include(minX, minY);
            extents.Include(maxX, maxY);
            return extents;
        }

        [Fact]
        public void Evaluate_PassesWhenSizeAndPositionMatch()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(1000, 2000, 61000, 32000),
                Box(1000.5, 2000, 61000.5, 32000),
                new[] { "HV-WALL" },
                Now);

            Assert.Equal(LinkSizeCheckResult.Passed, info.Result);
            Assert.Equal(1.0, info.Ratio.Value, 3);
            Assert.Equal(new[] { "HV-WALL" }, info.Layers);
        }

        [Fact]
        public void Evaluate_DetectsLinkImportedAsInches()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(0, 0, 60000, 30000),
                Box(0, 0, 60000 * 25.4, 30000 * 25.4),
                null,
                Now);

            Assert.Equal(LinkSizeCheckResult.ScaleMismatch, info.Result);
            Assert.Equal("Inch", info.LikelyImportUnit);
            Assert.Equal(25.4, info.Ratio.Value, 2);
            Assert.Contains("Millimeter", info.Message);
            Assert.True(LinkSizeCheckResult.IsProblem(info));
        }

        [Theory]
        [InlineData(304.8, "Foot")]
        [InlineData(10.0, "Centimeter")]
        [InlineData(1000.0, "Meter")]
        public void Evaluate_NamesOtherImportUnits(double factor, string unit)
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(0, 0, 6000, 3000),
                Box(0, 0, 6000 * factor, 3000 * factor),
                null,
                Now);

            Assert.Equal(unit, info.LikelyImportUnit);
        }

        [Fact]
        public void Evaluate_ReportsSmallerLinkWithoutGuessingAUnit()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(0, 0, 60000, 30000),
                Box(0, 0, 60000 / 25.4, 30000 / 25.4),
                null,
                Now);

            Assert.Equal(LinkSizeCheckResult.ScaleMismatch, info.Result);
            Assert.Null(info.LikelyImportUnit);
            Assert.Contains("smaller", info.Message);
        }

        [Fact]
        public void Evaluate_DetectsShiftWhenSizeMatches()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(0, 0, 60000, 30000),
                Box(-12000, 500, 48000, 30500),
                null,
                Now);

            Assert.Equal(LinkSizeCheckResult.Offset, info.Result);
            Assert.Equal(-12000, info.OffsetXMm.Value, 1);
            Assert.Equal(500, info.OffsetYMm.Value, 1);
        }

        [Fact]
        public void Evaluate_IsInconclusiveWhenShapesDisagree()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(
                Box(0, 0, 60000, 30000),
                Box(0, 0, 60000, 90000),
                null,
                Now);

            Assert.Equal(LinkSizeCheckResult.Inconclusive, info.Result);
            Assert.False(LinkSizeCheckResult.IsProblem(info));
        }

        [Fact]
        public void Evaluate_IsInconclusiveWithoutGeometry()
        {
            LinkSizeCheckInfo info = LinkSizeCheck.Evaluate(new CadExtents(), Box(0, 0, 1, 1), null, Now);

            Assert.Equal(LinkSizeCheckResult.Inconclusive, info.Result);
        }

        [Fact]
        public void DxfExtentsReader_IncludesLinesInsideBlocksAndFiltersLayers()
        {
            DxfDocument document = new DxfDocument();
            Layer wall = new Layer("HV-WALL");
            Layer anno = new Layer("ANNO");

            Line loose = new Line(new Vector2(0, 0), new Vector2(1000, 0)) { Layer = wall };
            Line note = new Line(new Vector2(-50000, -50000), new Vector2(-49000, -50000)) { Layer = anno };

            Line inBlock = new Line(new Vector2(0, 0), new Vector2(100, 200)) { Layer = wall };
            Block block = new Block("PLAN", new List<EntityObject> { inBlock });
            Insert insert = new Insert(block, new Vector2(5000, 3000));

            document.Entities.Add(loose);
            document.Entities.Add(note);
            document.Entities.Add(insert);

            CadExtents extents = DxfExtentsReader.Read(document, new[] { "HV-WALL" }, 1.0);

            Assert.Equal(0, extents.MinX, 3);
            Assert.Equal(0, extents.MinY, 3);
            Assert.Equal(5100, extents.MaxX, 3);
            Assert.Equal(3200, extents.MaxY, 3);
        }

        [Fact]
        public void DxfExtentsReader_AppliesMillimetersPerUnit()
        {
            DxfDocument document = new DxfDocument();
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(6, 3)));

            CadExtents extents = DxfExtentsReader.Read(document, null, 1000.0);

            Assert.Equal(6000, extents.Width, 3);
            Assert.Equal(3000, extents.Height, 3);
        }
    }
}
