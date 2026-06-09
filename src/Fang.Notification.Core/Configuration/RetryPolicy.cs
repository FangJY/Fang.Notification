using System;

namespace Fang.Notification.Core.Configuration
{
    /// <summary>
    /// 重试策略配置
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>最大重试次数</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>重试间隔基数（毫秒）</summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>是否使用指数退避</summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>最大退避时间（毫秒）</summary>
        public int MaxBackoffMs { get; set; } = 30000;

        /// <summary>可重试的HTTP状态码</summary>
        public int[] RetryableHttpStatusCodes { get; set; } = { 408, 429, 500, 502, 503, 504 };

        /// <summary>
        /// 计算下次重试的延迟时间
        /// </summary>
        public int GetDelayMs(int attempt)
        {
            if (attempt <= 0) return 0;

            if (UseExponentialBackoff)
            {
                var delay = (int)(BaseDelayMs * Math.Pow(2, attempt - 1));
                return Math.Min(delay, MaxBackoffMs);
            }

            return BaseDelayMs;
        }
    }
}
