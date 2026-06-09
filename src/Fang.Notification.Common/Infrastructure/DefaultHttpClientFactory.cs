using System;
using System.Net;
using System.Net.Http;
using Fang.Notification.Core.Configuration;

namespace Fang.Notification.Common.Infrastructure
{
    /// <summary>
    /// 默认HttpClient工厂
    /// </summary>
    public class DefaultHttpClientFactory
    {
        /// <summary>
        /// 创建HttpClient
        /// </summary>
        public static HttpClient CreateClient(ChannelOptions options = null)
        {
            var handler = new HttpClientHandler();

            if (options?.ProxyUrl != null)
            {
                handler.Proxy = new WebProxy(new Uri(options.ProxyUrl));
                handler.UseProxy = true;
            }

            var client = new HttpClient(handler);

            if (options != null)
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                if (options.CustomHeaders != null)
                {
                    foreach (var header in options.CustomHeaders)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            return client;
        }
    }
}
