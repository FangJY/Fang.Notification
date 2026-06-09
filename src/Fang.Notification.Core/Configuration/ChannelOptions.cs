using System.Collections.Generic;

namespace Fang.Notification.Core.Configuration
{
    /// <summary>
    /// 通道配置基类
    /// </summary>
    public abstract class ChannelOptions
    {
        /// <summary>是否启用该通道</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>请求超时时间（秒）</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>最大重试次数</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>重试间隔基数（秒）</summary>
        public int RetryBaseDelaySeconds { get; set; } = 1;

        /// <summary>是否使用指数退避</summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>熔断器失败阈值</summary>
        public int CircuitBreakerFailuresThreshold { get; set; } = 5;

        /// <summary>熔断器恢复时间（秒）</summary>
        public int CircuitBreakerRecoverySeconds { get; set; } = 60;

        /// <summary>代理服务器地址（可选）</summary>
        public string ProxyUrl { get; set; }

        /// <summary>自定义HTTP头</summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 全局推送配置
    /// </summary>
    public class FangNotificationOptions
    {
        /// <summary>全局默认超时时间（秒）</summary>
        public int DefaultTimeoutSeconds { get; set; } = 30;

        /// <summary>全局最大并发发送数</summary>
        public int MaxConcurrentSends { get; set; } = 10;

        /// <summary>是否启用消息日志</summary>
        public bool EnableMessageLogging { get; set; } = true;

        /// <summary>日志级别</summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        /// <summary>消息历史保留天数</summary>
        public int MessageHistoryRetentionDays { get; set; } = 7;
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }
}
