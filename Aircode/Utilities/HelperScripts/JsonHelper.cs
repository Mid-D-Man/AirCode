using Newtonsoft.Json;

namespace Aircode.Utilities;

public class JsonHelper
{
    // Utilities/HelperScripts/JsonHelper.cs
  
        private static readonly JsonSerializerSettings Settings = new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-dd"
        };

        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }

        public static T? Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json, Settings);
            }
            catch
            {
                return default;
            }
        }
    
}