using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    // Coordinates are CAD-local millimeters: the DXF value times cadSource.mmPerCadUnit.
    [JsonObject(MemberSerialization.OptIn)]
    public class WallCandidate
    {
        public WallCandidate()
        {
            SourceSegmentIds = new List<string>();
            Status = WallCandidateStatus.Unverified;
        }

        [JsonProperty("id", Order = 1)]
        public string Id { get; set; }

        [JsonProperty("layer", Order = 2)]
        public string Layer { get; set; }

        [JsonProperty("sourceSegmentIds", Order = 3)]
        public List<string> SourceSegmentIds { get; set; }

        [JsonProperty("startXMm", Order = 4)]
        public double StartXMm { get; set; }

        [JsonProperty("startYMm", Order = 5)]
        public double StartYMm { get; set; }

        [JsonProperty("endXMm", Order = 6)]
        public double EndXMm { get; set; }

        [JsonProperty("endYMm", Order = 7)]
        public double EndYMm { get; set; }

        [JsonProperty("lengthMm", Order = 8)]
        public double LengthMm { get; set; }

        [JsonProperty("thicknessMm", Order = 9)]
        public double ThicknessMm { get; set; }

        [JsonProperty("orientation", Order = 10)]
        public string Orientation { get; set; }

        [JsonProperty("status", Order = 11)]
        public string Status { get; set; }

        [JsonProperty("heightMm", Order = 12)]
        public double? HeightMm { get; set; }

        [JsonProperty("notes", Order = 13)]
        public string Notes { get; set; }

        [JsonProperty("revitElementId", Order = 14)]
        public long? RevitElementId { get; set; }

        public WallCandidate Clone()
        {
            WallCandidate copy = (WallCandidate)MemberwiseClone();
            copy.SourceSegmentIds = SourceSegmentIds == null ? new List<string>() : new List<string>(SourceSegmentIds);
            return copy;
        }
    }
}
