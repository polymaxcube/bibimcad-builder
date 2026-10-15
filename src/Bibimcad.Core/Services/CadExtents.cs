using System;

namespace Bibimcad.Services
{
    // Axis-aligned XY bounding box.
    public class CadExtents
    {
        public CadExtents()
        {
            MinX = double.PositiveInfinity;
            MinY = double.PositiveInfinity;
            MaxX = double.NegativeInfinity;
            MaxY = double.NegativeInfinity;
        }

        public double MinX { get; private set; }

        public double MinY { get; private set; }

        public double MaxX { get; private set; }

        public double MaxY { get; private set; }

        public int PointCount { get; private set; }

        public bool IsEmpty
        {
            get { return PointCount == 0; }
        }

        public double Width
        {
            get { return IsEmpty ? 0.0 : MaxX - MinX; }
        }

        public double Height
        {
            get { return IsEmpty ? 0.0 : MaxY - MinY; }
        }

        public void Include(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                return;

            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
            PointCount++;
        }

        public CadExtents Scaled(double factor)
        {
            CadExtents scaled = new CadExtents();
            if (IsEmpty)
                return scaled;

            scaled.Include(MinX * factor, MinY * factor);
            scaled.Include(MaxX * factor, MaxY * factor);
            scaled.PointCount = PointCount;
            return scaled;
        }
    }
}
