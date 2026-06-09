using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Core.Configuration;

namespace Fang.Notification.Common.Infrastructure
{
    /// <summary>
    /// 带重试机制的DelegatingHandler
    /// </summary>
    public class RetryHandler : DelegatingHandler
    {
        private readonly RetryPolicy _policy;

        public RetryHandler(RetryPolicy policy = null)
            : base(new HttpClientHandler())
        {
            _policy = policy ?? new RetryPolicy();
        }

        public RetryHandler(RetryPolicy policy, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _policy = policy ?? new RetryPolicy();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;

            for (int attempt = 0; attempt <= _policy.MaxRetryAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 0)
                {
                    var delay = _policy.GetDelayMs(attempt);
                    await Task.Delay(delay, cancellationToken);
                }

                response = await base.SendAsync(request.Clone(), cancellationToken);

                if (response.IsSuccessStatusCode)
                    return response;

                if (!IsRetryable(response))
                    return response;
            }

            return response;
        }

        private bool IsRetryable(HttpResponseMessage response)
        {
            var statusCode = (int)response.StatusCode;
            foreach (var code in _policy.RetryableHttpStatusCodes)
            {
                if (code == statusCode)
                    return true;
            }
            return false;
        }
    }

    internal static class HttpRequestMessageExtensions
    {
        public static HttpRequestMessage Clone(this HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content != null)
            {
                var content = request.Content.ReadAsByteArrayAsync()
                    .GetAwaiter().GetResult();
                clone.Content = new ByteArrayContent(content);
                if (request.Content.Headers != null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Version = request.Version;

            return clone;
        }
    }
}
