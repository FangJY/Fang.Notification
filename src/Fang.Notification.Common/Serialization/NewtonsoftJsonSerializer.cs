#if NETSTANDARD2_0
using System;
using Fang.Notification.Core.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Fang.Notification.Common.Serialization
{
    /// <summary>
    /// Newtonsoft.Json实现（netstandard2.0）
    /// </summary>
    public class NewtonsoftJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public NewtonsoftJsonSerializer(JsonSerializerSettings settings = null)
        {
            _settings = settings ?? new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        public T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        public object Deserialize(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, _settings);
        }
    }
}
#endif
