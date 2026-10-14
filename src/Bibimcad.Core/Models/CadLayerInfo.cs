using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CadLayerInfo
    {
        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("entityCount", Order = 2)]
        public int EntityCount { get; set; }
    }
}
