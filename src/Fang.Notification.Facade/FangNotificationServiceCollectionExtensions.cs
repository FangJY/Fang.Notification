using System;
using Fang.Notification.Common.Serialization;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Facade;
using Fang.Notification.DingTalk;
using Fang.Notification.Email;
using Fang.Notification.Feishu;
using Fang.Notification.Teams;
using Fang.Notification.WeCom;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Fang.Notification 服务注册扩展
    /// </summary>
    public static class FangNotificationServiceCollectionExtensions
    {
        /// <summary>
        /// 添加Fang.Notification核心服务
        /// </summary>
        public static IServiceCollection AddFangNotification(
            this IServiceCollection services,
            Action<FangNotificationOptions> configureOptions = null)
        {
            if (configureOptions != null)
                services.Configure(configureOptions);

#if NETSTANDARD2_0
            services.AddSingleton<IJsonSerializer, NewtonsoftJsonSerializer>();
#elif NETSTANDARD2_1
            services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
#endif
            services.AddSingleton<InMemoryTokenCache>();
            services.AddSingleton<NotificationService>();

            return services;
        }

        /// <summary>
        /// 添加钉钉通道
        /// </summary>
        public static IServiceCollection AddDingTalk(
            this IServiceCollection services,
            Action<DingTalkOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddHttpClient<DingTalkChannel>();
            services.AddSingleton<IMessageChannel, DingTalkChannel>();
            return services;
        }

        /// <summary>
        /// 添加飞书通道
        /// </summary>
        public static IServiceCollection AddFeishu(
            this IServiceCollection services,
            Action<FeishuOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddHttpClient<FeishuChannel>();
            services.AddSingleton<IMessageChannel, FeishuChannel>();
            return services;
        }

        /// <summary>
        /// 添加企业微信通道
        /// </summary>
        public static IServiceCollection AddWeCom(
            this IServiceCollection services,
            Action<WeComOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddHttpClient<WeComChannel>();
            services.AddSingleton<IMessageChannel, WeComChannel>();
            return services;
        }

        /// <summary>
        /// 添加邮件通道
        /// </summary>
        public static IServiceCollection AddEmail(
            this IServiceCollection services,
            Action<EmailOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddSingleton<IMessageChannel, EmailChannel>();
            return services;
        }

        /// <summary>
        /// 添加Teams通道
        /// </summary>
        public static IServiceCollection AddTeams(
            this IServiceCollection services,
            Action<TeamsOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddHttpClient<TeamsChannel>();
            services.AddSingleton<IMessageChannel, TeamsChannel>();
            return services;
        }
    }
}
