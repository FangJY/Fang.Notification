using System;

namespace Fang.Notification.Core.Abstractions
{
    /// <summary>
    /// JSON序列化接口（抽象序列化实现）
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>序列化对象为JSON字符串</summary>
        string Serialize<T>(T obj);

        /// <summary>反序列化JSON字符串为对象</summary>
        T Deserialize<T>(string json);

        /// <summary>反序列化JSON字符串为对象</summary>
        object Deserialize(string json, Type type);
    }
}
