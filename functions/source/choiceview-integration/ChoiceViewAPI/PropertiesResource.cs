using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChoiceViewAPI
{
    public class PropertiesResource
    {
        [JsonProperty("sessionId")]
        public int SessionId { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string,string> Properties { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }

        public PropertiesResource()
        {
            Properties = new Dictionary<string,string>();
            Links = new List<Link>();
        }
    }
}