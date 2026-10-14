using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bibimcad.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ValidationInfo
    {
        public ValidationInfo()
        {
            Failures = new List<ValidationFailure>();
        }

        [JsonProperty("validatedAt", Order = 1)]
        public string ValidatedAt { get; set; }

        [JsonProperty("toleranceMm", Order = 2)]
        public double ToleranceMm { get; set; }

        [JsonProperty("checkedCount", Order = 3)]
        public int CheckedCount { get; set; }

        [JsonProperty("passedCount", Order = 4)]
        public int PassedCount { get; set; }

        [JsonProperty("failedCount", Order = 5)]
        public int FailedCount { get; set; }

        [JsonProperty("failures", Order = 6)]
        public List<ValidationFailure> Failures { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ValidationFailure
    {
        [JsonProperty("candidateId", Order = 1)]
        public string CandidateId { get; set; }

        [JsonProperty("revitElementId", Order = 2)]
        public long? RevitElementId { get; set; }

        [JsonProperty("reason", Order = 3)]
        public string Reason { get; set; }
    }
}
