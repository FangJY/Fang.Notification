using System;

namespace Fang.Notification.Common.TokenManagement
{
    /// <summary>
    /// Token缓存条目
    /// </summary>
    public class TokenCacheEntry
    {
        /// <summary>访问令牌</summary>
        public string AccessToken { get; set; }

        /// <summary>过期时间</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>获取时间</summary>
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

        /// <summary>是否已过期</summary>
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        /// <summary>是否即将过期（默认提前5分钟）</summary>
        public bool IsNearExpiry(int advanceSeconds = 300)
        {
            return DateTime.UtcNow >= ExpiresAt.AddSeconds(-advanceSeconds);
        }
    }
}
