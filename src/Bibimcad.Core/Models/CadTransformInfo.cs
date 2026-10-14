using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CadTransformInfo
    {
        [JsonProperty("originXFeet", Order = 1)]
        public double OriginXFeet { get; set; }

        [JsonProperty("originYFeet", Order = 2)]
        public double OriginYFeet { get; set; }

        [JsonProperty("originZFeet", Order = 3)]
        public double OriginZFeet { get; set; }

        [JsonProperty("basisX_X", Order = 4)]
        public double BasisX_X { get; set; }

        [JsonProperty("basisX_Y", Order = 5)]
        public double BasisX_Y { get; set; }

        [JsonProperty("basisX_Z", Order = 6)]
        public double BasisX_Z { get; set; }

        [JsonProperty("basisY_X", Order = 7)]
        public double BasisY_X { get; set; }

        [JsonProperty("basisY_Y", Order = 8)]
        public double BasisY_Y { get; set; }

        [JsonProperty("basisY_Z", Order = 9)]
        public double BasisY_Z { get; set; }

        [JsonProperty("basisZ_X", Order = 10)]
        public double BasisZ_X { get; set; }

        [JsonProperty("basisZ_Y", Order = 11)]
        public double BasisZ_Y { get; set; }

        [JsonProperty("basisZ_Z", Order = 12)]
        public double BasisZ_Z { get; set; }

        [JsonProperty("rotationDegrees", Order = 13)]
        public double RotationDegrees { get; set; }

        [JsonProperty("scaleX", Order = 14)]
        public double ScaleX { get; set; }

        [JsonProperty("scaleY", Order = 15)]
        public double ScaleY { get; set; }

        [JsonProperty("scaleZ", Order = 16)]
        public double ScaleZ { get; set; }

        [JsonProperty("isMirrored", Order = 17)]
        public bool IsMirrored { get; set; }
    }
}
