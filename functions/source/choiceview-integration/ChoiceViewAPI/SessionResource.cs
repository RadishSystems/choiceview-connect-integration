using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChoiceViewAPI
{
    public class SessionResource
    {
        [JsonProperty("sessionId")]
        public int SessionId { get; set; }

        [JsonProperty("callerId")]
        public string CallerId { get; set; }

        [JsonProperty("callId")]
        public string CallId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("networkQuality")]
        public string NetworkQuality { get; set; }

        [JsonProperty("networkType")]
        public string NetworkType { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string,string> Properties { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }

        public SessionResource()
        {
            Properties = new Dictionary<string,string>();
            Links = new List<Link>();
        }
    }
}