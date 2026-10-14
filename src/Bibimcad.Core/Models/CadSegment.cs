namespace Bibimcad.Models
{
    // One straight piece of linework on a wall layer, in CAD-local millimeters.
    // Id is the DXF entity handle, plus ":<vertex index>" for polyline segments, so it is stable across reads.
    public class CadSegment
    {
        public CadSegment(string id, string layer, double x1, double y1, double x2, double y2)
        {
            Id = id;
            Layer = layer;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public string Id { get; private set; }

        public string Layer { get; private set; }

        public double X1 { get; private set; }

        public double Y1 { get; private set; }

        public double X2 { get; private set; }

        public double Y2 { get; private set; }
    }
}
