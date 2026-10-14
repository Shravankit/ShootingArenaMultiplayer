using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MultiplayerShooting.Network.Serialization
{
    public static class NetworkJson
    {
        public static JObject Serialize<T>(T data)
        {
            if (data == null)
                return new JObject();

            return JObject.FromObject(data);
        }

        public static T Deserialize<T>(JToken data)
        {
            if (data == null || data.Type == JTokenType.Null)
                return default(T);

            return data.ToObject<T>();
        }

        public static string ToJson<T>(T data)
        {
            return JsonConvert.SerializeObject(data);
        }

        public static T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}