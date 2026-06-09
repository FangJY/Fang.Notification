using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.Middleware;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Core.Middleware;
using Fang.Notification.Core.Models;
using Fang.Notification.DingTalk.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.DingTalk
{
    public class DingTalkChannel : IMessageChannel, ITemplateMessageChannel, IBatchMessageChannel
    {
        private readonly HttpClient _httpClient;
        private readonly DingTalkOptions _options;
        private readonly IJsonSerializer _serializer;
        private readonly InMemoryTokenCache _tokenCache;
        private readonly MessagePipeline _pipeline;

        public string ChannelName => "dingtalk";
        public string DisplayName => "钉钉";

        public IReadOnlyList<MessageType> SupportedTypes => new List<MessageType>
        {
            MessageType.Text,
            MessageType.Markdown,
            MessageType.Image,
            MessageType.File
        };

        public DingTalkChannel(
            HttpClient httpClient,
            IOptions<DingTalkOptions> options,
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
                {
                    builder.Use(middleware);
                }
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
                    $"钉钉不支持消息类型: {message.Type}");

            return await _pipeline.ExecuteAsync(message, receiver, cancellationToken);
        }

        private async Task<SendResult> SendCoreAsync(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (receiver.HasType(ReceiverType.WebhookUrl))
                {
                    return await SendViaWebhook(message, receiver.GetFirst(ReceiverType.WebhookUrl),
                        cancellationToken);
                }
                else if (receiver.HasType(ReceiverType.GroupChatId))
                {
                    return await SendViaChat(message, receiver.GetFirst(ReceiverType.GroupChatId),
                        cancellationToken);
                }
                else
                {
                    return await SendViaApi(message, receiver, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                return SendResult.Failure(ChannelName, message.MessageId, "EXCEPTION", ex.Message);
            }
            finally
            {
                sw.Stop();
            }
        }

        private async Task<SendResult> SendViaWebhook(
            NotificationMessage message,
            string webhookUrl,
            CancellationToken cancellationToken)
        {
            var url = webhookUrl;

            if (!string.IsNullOrEmpty(_options.WebhookSecret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sign = GenerateSignature(timestamp, _options.WebhookSecret);
                url = $"{webhookUrl}&timestamp={timestamp}&sign={sign}";
            }

            var dingtalkMessage = new DingTalkMessageBuilder().BuildMessage(message);
            var json = _serializer.Serialize(dingtalkMessage);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var dingtalkResponse = _serializer.Deserialize<DingTalkResponse>(responseBody);
                if (dingtalkResponse.errcode == 0)
                {
                    return SendResult.Success(ChannelName, message.MessageId,
                        dingtalkResponse.task_id);
                }

                return SendResult.Failure(ChannelName, message.MessageId,
                    dingtalkResponse.errcode.ToString(), dingtalkResponse.errmsg, responseBody);
            }

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private async Task<SendResult> SendViaApi(
            NotificationMessage message,
            MessageReceiver receiver,
            CancellationToken cancellationToken)
        {
            var token = await _tokenCache.GetOrAddAsync(ChannelName,
                async () => await GetTokenFromApi(cancellationToken),
                _options.TokenRefreshAdvanceSeconds,
                cancellationToken);

            // 图片/文件消息需先上传获取 media_id
            var resolvedMediaId = await ResolveMediaAsync(message, token, cancellationToken);

            var dingtalkMessage = BuildApiMessage(message, receiver, resolvedMediaId);
            var json = _serializer.Serialize(dingtalkMessage);

            var url = $"https://oapi.dingtalk.com/topapi/message/corpconversation/asyncsend_v2?access_token={token}";

            var response = await _httpClient.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            DingTalkWorkNoticeResponse dingtalkResponse = null;
            if (response.IsSuccessStatusCode)
            {
                dingtalkResponse = _serializer.Deserialize<DingTalkWorkNoticeResponse>(responseBody);

                // 旧 API token 过期/无效时返回 HTTP 200 + errcode
                if (dingtalkResponse.errcode == 40001 ||
                    dingtalkResponse.errcode == 40014 ||
                    dingtalkResponse.errcode == 42001)
                {
                    _tokenCache.Clear(ChannelName);
                    token = await _tokenCache.GetOrAddAsync(ChannelName,
                        async () => await GetTokenFromApi(cancellationToken),
                        _options.TokenRefreshAdvanceSeconds,
                        cancellationToken);

                    url = $"https://oapi.dingtalk.com/topapi/message/corpconversation/asyncsend_v2?access_token={token}";

                    response = await _httpClient.PostAsync(
                        url,
                        new StringContent(json, Encoding.UTF8, "application/json"),
                        cancellationToken);
                    responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dingtalkResponse = _serializer.Deserialize<DingTalkWorkNoticeResponse>(responseBody);
                    }
                }

                if (dingtalkResponse.errcode == 0)
                {
                    return SendResult.Success(ChannelName, message.MessageId,
                        dingtalkResponse.task_id.ToString());
                }

                return SendResult.Failure(ChannelName, message.MessageId,
                    dingtalkResponse.errcode.ToString(), dingtalkResponse.errmsg, responseBody);
            }

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private object BuildApiMessage(NotificationMessage message, MessageReceiver receiver, string resolvedMediaId = null)
        {
            var userIds = receiver.Identifiers
                .Where(kv => kv.Key == ReceiverType.UserId || kv.Key == ReceiverType.OpenId)
                .SelectMany(kv => kv.Value)
                .ToList();

            if (userIds.Count == 0)
                throw new ArgumentException("钉钉工作通知 API 需要指定 UserId 或 OpenId");

            if (_options.AgentId == null)
                throw new InvalidOperationException("钉钉工作通知 API 需要配置 AgentId");

            var useridList = string.Join(",", userIds);
            object msgContent;

            switch (message)
            {
                case TextMessage text:
                    msgContent = new
                    {
                        msgtype = "text",
                        text = new { content = text.Content }
                    };
                    break;
                case MarkdownMessage markdown:
                    msgContent = new
                    {
                        msgtype = "markdown",
                        markdown = new { title = markdown.Title, text = markdown.Content }
                    };
                    break;
                case ImageMessage _ when !string.IsNullOrEmpty(resolvedMediaId):
                    msgContent = new
                    {
                        msgtype = "image",
                        image = new { media_id = resolvedMediaId }
                    };
                    break;
                case FileMessage _ when !string.IsNullOrEmpty(resolvedMediaId):
                    msgContent = new
                    {
                        msgtype = "file",
                        file = new { media_id = resolvedMediaId }
                    };
                    break;
                default:
                    throw new NotSupportedException($"钉钉旧版工作通知 API 不支持消息类型: {message.Type}");
            }

            return new
            {
                agent_id = _options.AgentId.Value,
                userid_list = useridList,
                msg = msgContent
            };
        }

        private async Task<TokenCacheEntry> GetTokenFromApi(CancellationToken cancellationToken)
        {
            var body = new { appKey = _options.ClientID, appSecret = _options.ClientSecret };
            var json = _serializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/v1.0/oauth2/accessToken", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = _serializer.Deserialize<DingTalkOAuthTokenResponse>(responseBody);

            return new TokenCacheEntry
            {
                AccessToken = tokenResponse.accessToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expireIn - 300)
            };
        }

        private async Task<string> ResolveMediaAsync(NotificationMessage message, string token, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case ImageMessage img when !string.IsNullOrEmpty(img.MediaId):
                    return img.MediaId;
                case ImageMessage img:
                    return await UploadMediaAsync(img, token, "image", cancellationToken);
                case FileMessage file when !string.IsNullOrEmpty(file.MediaId):
                    return file.MediaId;
                case FileMessage file:
                    return await UploadMediaAsync(file, token, "file", cancellationToken);
                default:
                    return null;
            }
        }

        private async Task<string> UploadMediaAsync(NotificationMessage message, string token, string mediaType, CancellationToken cancellationToken)
        {
            byte[] fileData;
            string fileName;

            switch (message)
            {
                case ImageMessage img when !string.IsNullOrEmpty(img.ImageUrl):
                    fileData = await _httpClient.GetByteArrayAsync(img.ImageUrl);
                    fileName = System.IO.Path.GetFileName(new Uri(img.ImageUrl).AbsolutePath);
                    if (string.IsNullOrWhiteSpace(fileName)) fileName = "image.jpg";
                    break;
                case ImageMessage img when !string.IsNullOrEmpty(img.ImageBase64):
                    fileData = Convert.FromBase64String(img.ImageBase64);
                    fileName = "image.jpg";
                    break;
                case FileMessage file when !string.IsNullOrEmpty(file.FileUrl):
                    fileName = string.IsNullOrEmpty(file.FileName) ? System.IO.Path.GetFileName(file.FileUrl) : file.FileName;
                    fileData = System.IO.File.ReadAllBytes(file.FileUrl);
                    break;
                case FileMessage file when file.FileBytes != null:
                    fileName = string.IsNullOrEmpty(file.FileName) ? "file" : file.FileName;
                    fileData = file.FileBytes;
                    break;
                default:
                    throw new InvalidOperationException("钉钉上传需要提供文件数据");
            }

            // 使用 SendViaApi 通过 _tokenCache.GetOrAddAsync 获取的 token
            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(fileData), "media", fileName);

            var response = await _httpClient.PostAsync(
                $"https://oapi.dingtalk.com/media/upload?access_token={token}&type={mediaType}",
                formData, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var result = _serializer.Deserialize<DingTalkMediaUploadResponse>(responseBody);
            if (result?.errcode != 0 || string.IsNullOrEmpty(result?.media_id))
                throw new InvalidOperationException($"钉钉上传{mediaType}失败: {result?.errmsg ?? responseBody}");

            return result.media_id;
        }

        private async Task<SendResult> SendViaChat(
            NotificationMessage message,
            string chatid,
            CancellationToken cancellationToken)
        {
            var token = await _tokenCache.GetOrAddAsync(ChannelName,
                async () => await GetTokenFromApi(cancellationToken),
                _options.TokenRefreshAdvanceSeconds,
                cancellationToken);

            // 图片/文件先上传获取 media_id
            var resolvedMediaId = await ResolveMediaAsync(message, token, cancellationToken);

            object msgContent;

            switch (message)
            {
                case TextMessage text:
                    msgContent = new
                    {
                        msgtype = "text",
                        text = new { content = text.Content }
                    };
                    break;
                case MarkdownMessage markdown:
                    msgContent = new
                    {
                        msgtype = "markdown",
                        markdown = new { title = markdown.Title, text = markdown.Content }
                    };
                    break;
                case ImageMessage _:
                    msgContent = new
                    {
                        msgtype = "image",
                        image = new { media_id = resolvedMediaId }
                    };
                    break;
                case FileMessage _:
                    msgContent = new
                    {
                        msgtype = "file",
                        file = new { media_id = resolvedMediaId }
                    };
                    break;
                default:
                    throw new NotSupportedException($"钉钉群聊不支持消息类型: {message.Type}");
            }

            var body = new
            {
                chatid,
                msg = msgContent
            };

            var json = _serializer.Serialize(body);

            var response = await _httpClient.PostAsync(
                $"https://oapi.dingtalk.com/chat/send?access_token={token}",
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var chatResponse = _serializer.Deserialize<DingTalkResponse>(responseBody);
                if (chatResponse.errcode == 0)
                    return SendResult.Success(ChannelName, message.MessageId, string.Empty);

                return SendResult.Failure(ChannelName, message.MessageId,
                    chatResponse.errcode.ToString(), chatResponse.errmsg, responseBody);
            }

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private static string GenerateSignature(long timestamp, string secret)
        {
            var stringToSign = $"{timestamp}\n{secret}";
            using var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            return Convert.ToBase64String(hash);
        }

        public async Task<IEnumerable<SendResult>> SendBatchAsync(
            IEnumerable<NotificationMessage> messages,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            var tasks = messages.Select(m => SendAsync(m, receiver, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        public async Task<IEnumerable<SendResult>> SendToManyAsync(
            NotificationMessage message,
            IEnumerable<MessageReceiver> receivers,
            CancellationToken cancellationToken = default)
        {
            var tasks = receivers.Select(r => SendAsync(message, r, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        public Task<SendResult> SendTemplateAsync(
            string templateId,
            Dictionary<string, object> templateData,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            var markdownMessage = new MarkdownMessage
            {
                Title = templateId,
                Content = _serializer.Serialize(templateData)
            };

            return SendAsync(markdownMessage, receiver, cancellationToken);
        }

        private void LogMessage(string msg, LogLevel level)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
        }
    }
}
