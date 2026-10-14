using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProjectInfo
    {
        [JsonProperty("systemName", Order = 1)]
        public string SystemName { get; set; }

        [JsonProperty("projectName", Order = 2)]
        public string ProjectName { get; set; }

        [JsonProperty("targetRevitVersion", Order = 3)]
        public int TargetRevitVersion { get; set; }

        [JsonProperty("targetLod", Order = 4)]
        public int TargetLod { get; set; }
    }
}
