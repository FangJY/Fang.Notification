using System.Threading;
using System.Threading.Tasks;

namespace Fang.Notification.Core.Abstractions
{
    /// <summary>
    /// Token提供者接口
    /// </summary>
    public interface ITokenProvider
    {
        /// <summary>通道名称</summary>
        string ChannelName { get; }

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 刷新令牌
        /// </summary>
        Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 清除缓存的令牌
        /// </summary>
        void ClearToken();
    }
}
