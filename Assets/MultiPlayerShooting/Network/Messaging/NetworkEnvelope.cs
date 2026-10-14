
using Newtonsoft.Json.Linq;

namespace MultiplayerShooting.Network.Messaging
{
    public class NetworkEnvelope
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public JObject Data { get; set; }
        public string To { get; set; }
        public bool Echo { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject
            {
                ["t"] = Type,
                ["name"] = Name,
                ["data"] = Data ?? new JObject(),
                ["echo"] = Echo
            };

            if (!string.IsNullOrEmpty(To))
                json["to"] = To;

            return json;
        }
    }
}
