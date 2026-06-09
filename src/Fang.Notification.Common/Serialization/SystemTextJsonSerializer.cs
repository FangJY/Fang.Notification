#if NETSTANDARD2_1
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fang.Notification.Core.Abstractions;

namespace Fang.Notification.Common.Serialization
{
    /// <summary>
    /// System.Text.Json 实现（netstandard2.1）
    /// </summary>
    public class SystemTextJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonSerializer(JsonSerializerOptions options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
        }

        public string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        public object Deserialize(string json, Type type)
        {
            return JsonSerializer.Deserialize(json, type, _options);
        }
    }
}
#endif
