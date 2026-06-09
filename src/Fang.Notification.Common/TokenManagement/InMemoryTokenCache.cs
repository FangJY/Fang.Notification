using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Fang.Notification.Common.TokenManagement
{
    /// <summary>
    /// 基于内存的Token缓存实现
    /// </summary>
    public class InMemoryTokenCache
    {
        private readonly ConcurrentDictionary<string, TokenCacheEntry> _cache
            = new ConcurrentDictionary<string, TokenCacheEntry>();

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks
            = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// 获取或添加Token（带并发控制）
        /// </summary>
        public async Task<string> GetOrAddAsync(
            string channelName,
            Func<Task<TokenCacheEntry>> tokenFactory,
            int advanceSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(channelName, out var entry) && !entry.IsNearExpiry(advanceSeconds))
            {
                return entry.AccessToken;
            }

            var lockObj = _locks.GetOrAdd(channelName, _ => new SemaphoreSlim(1, 1));

            await lockObj.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(channelName, out entry) && !entry.IsNearExpiry(advanceSeconds))
                {
                    return entry.AccessToken;
                }

                var newEntry = await tokenFactory();
                _cache[channelName] = newEntry;
                return newEntry.AccessToken;
            }
            finally
            {
                lockObj.Release();
            }
        }

        /// <summary>
        /// 清除指定通道的Token缓存
        /// </summary>
        public void Clear(string channelName)
        {
            _cache.TryRemove(channelName, out _);
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAll()
        {
            _cache.Clear();
        }
    }
}
