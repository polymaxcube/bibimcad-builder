using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RevitLinkInfo
    {
        [JsonProperty("elementId", Order = 1)]
        public int? ElementId { get; set; }

        [JsonProperty("name", Order = 2)]
        public string Name { get; set; }

        [JsonProperty("isLinked", Order = 3)]
        public bool IsLinked { get; set; }

        [JsonProperty("transform", Order = 4)]
        public CadTransformInfo Transform { get; set; }
    }
}
