using Newtonsoft.Json;

namespace ChoiceViewAPI
{
    public class Link
    {
        public static string StateNotificationRel = "/rels/statenotification";
        public static string MessageNotificationRel = "/rels/messagenotification";
        public static string SessionRel = "/rels/session";
        public static string PayloadRel = "/rels/properties";
        public static string ControlMessageRel = "/rels/controlmessage";
        public static string SelfRel = "self";

        [JsonProperty("rel")]
        public string Rel { get; set; }

        [JsonProperty("href")]
        public string? Href { get; set; }
    }
}