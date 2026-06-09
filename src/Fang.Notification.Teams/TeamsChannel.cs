using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.Middleware;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Core.Middleware;
using Fang.Notification.Core.Models;
using Fang.Notification.Teams.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.Teams
{
    public class TeamsChannel : IMessageChannel
    {
        private readonly HttpClient _httpClient;
        private readonly TeamsOptions _options;
        private readonly IJsonSerializer _serializer;
        private readonly InMemoryTokenCache _tokenCache;
        private readonly MessagePipeline _pipeline;

        public string ChannelName => "teams";
        public string DisplayName => "Microsoft Teams";

        public IReadOnlyList<MessageType> SupportedTypes => new List<MessageType>
        {
            MessageType.Text,
            MessageType.Card,
            MessageType.Image
        };

        public TeamsChannel(
            HttpClient httpClient,
            IOptions<TeamsOptions> options,
            IJsonSerializer serializer,
            InMemoryTokenCache tokenCache,
            IEnumerable<IMessageMiddleware> middlewares = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));

            var builder = new MessagePipelineBuilder()
                .Use(new LoggingMiddleware(LogMessage))
                .Use(new TimeoutMiddleware(_options.TimeoutSeconds))
                .Use(new RetryMiddleware())
                .SetFinalHandler((msg, rec) => SendCoreAsync(msg, rec, CancellationToken.None));

            if (middlewares != null)
            {
                foreach (var middleware in middlewares.OrderBy(m => m.Order))
                    builder.Use(middleware);
            }

            _pipeline = builder.Build();
        }

        public bool SupportsMessageType(MessageType type) => SupportedTypes.Contains(type);

        public async Task<SendResult> SendAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            if (!SupportsMessageType(message.Type))
                return SendResult.Failure(ChannelName, message.MessageId, "UNSUPPORTED_TYPE",
                    $"Teams不支持消息类型: {message.Type}");

            return await _pipeline.ExecuteAsync(message, receiver, cancellationToken);
        }

        private async Task<SendResult> SendCoreAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken)
        {
            try
            {
                if (receiver.HasType(ReceiverType.WebhookUrl))
                {
                    return await SendViaWebhook(message, receiver.GetFirst(ReceiverType.WebhookUrl), cancellationToken);
                }
                else
                {
                    return await SendViaGraphApi(message, receiver, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                return SendResult.Failure(ChannelName, message.MessageId, "EXCEPTION", ex.Message);
            }
        }

        private async Task<SendResult> SendViaWebhook(
            NotificationMessage message,
            string webhookUrl,
            CancellationToken cancellationToken)
        {
            object payload;

            if (message is CardMessage)
            {
                payload = new TeamsMessageBuilder().BuildMessage(message);
            }
            else
            {
                payload = new { text = message.Content };
            }

            var json = _serializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return SendResult.Success(ChannelName, message.MessageId);

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private async Task<SendResult> SendViaGraphApi(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken)
        {
            var token = await _tokenCache.GetOrAddAsync(ChannelName,
                async () => await GetTokenFromApi(cancellationToken),
                300, cancellationToken);

            var teamId = receiver.GetFirst(ReceiverType.ChannelId) ?? _options.TeamId;
            var channelId = _options.ChannelId;

            string graphUrl;
            object payload;

            if (message is CardMessage card)
            {
                graphUrl = $"{_options.GraphBaseUrl}/teams/{teamId}/channels/{channelId}/messages";
                var adaptiveCard = new TeamsMessageBuilder().BuildMessage(message);
                payload = new
                {
                    body = new
                    {
                        contentType = "adaptiveCard",
                        content = _serializer.Serialize(adaptiveCard)
                    }
                };
            }
            else
            {
                graphUrl = $"{_options.GraphBaseUrl}/teams/{teamId}/channels/{channelId}/messages";
                payload = new
                {
                    body = new
                    {
                        contentType = "html",
                        content = message.Content
                    }
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, graphUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(_serializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return SendResult.Success(ChannelName, message.MessageId);

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private async Task<TokenCacheEntry> GetTokenFromApi(CancellationToken cancellationToken)
        {
            var body = new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                scope = string.Join(" ", _options.Scopes),
                grant_type = "client_credentials"
            };

            var content = new StringContent(BuildFormUrlEncodedBody(body), Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _httpClient.PostAsync(
                $"{_options.AuthorityUrl}/{_options.TenantId}/oauth2/v2.0/token", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = _serializer.Deserialize<TeamsTokenResponse>(responseBody);

            return new TokenCacheEntry
            {
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 300)
            };
        }

        private static string BuildFormUrlEncodedBody(object obj)
        {
            var props = obj.GetType().GetProperties();
            var pairs = props.Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.GetValue(obj)?.ToString() ?? "")}");
            return string.Join("&", pairs);
        }

        private void LogMessage(string msg, LogLevel level)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
        }
    }

    internal class TeamsTokenResponse
    {
        public string token_type { get; set; }
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }
}
