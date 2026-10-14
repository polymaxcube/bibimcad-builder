using System.Collections.Generic;
using Bibimcad.Services;
using Xunit;

namespace Bibimcad.Tests
{
    public class WallGeometryCheckTests
    {
        private static ExpectedWall Expected()
        {
            return new ExpectedWall { X1 = 0, Y1 = 100, X2 = 6000, Y2 = 100, LengthMm = 6000, ThicknessMm = 200, HeightMm = 3000 };
        }

        private static BuiltWall Built()
        {
            return new BuiltWall { X1 = 0, Y1 = 100, X2 = 6000, Y2 = 100, LengthMm = 6000, WidthMm = 200, HeightMm = 3000 };
        }

        [Fact]
        public void Compare_PassesAnExactWall()
        {
            Assert.Empty(WallGeometryCheck.Compare(Expected(), Built(), 1.0));
        }

        [Fact]
        public void Compare_AcceptsReversedDirectionWithinTolerance()
        {
            BuiltWall built = Built();
            built.X1 = 6000.8;
            built.X2 = 0;

            Assert.Empty(WallGeometryCheck.Compare(Expected(), built, 1.0));
        }

        [Fact]
        public void Compare_ReportsEachValueOutsideTolerance()
        {
            BuiltWall built = Built();
            built.X2 = 5998;
            built.LengthMm = 5998;
            built.WidthMm = 150;
            built.HeightMm = 2800;

            List<string> problems = WallGeometryCheck.Compare(Expected(), built, 1.0);

            Assert.Equal(4, problems.Count);
            Assert.Contains(problems, p => p.StartsWith("Endpoint is 2 mm"));
            Assert.Contains(problems, p => p.StartsWith("Width is 150 mm"));
        }

        [Theory]
        [InlineData(200.0, "Bibimcad - 200 mm")]
        [InlineData(149.5, "Bibimcad - 150 mm")]
        [InlineData(99.4, "Bibimcad - 99 mm")]
        public void WallTypeName_UsesWholeMillimeters(double thickness, string expected)
        {
            Assert.Equal(expected, WallGeometryCheck.WallTypeName(thickness));
        }
    }
}
