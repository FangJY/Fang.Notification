using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.Middleware;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Configuration;
using Fang.Notification.Core.Middleware;
using Fang.Notification.Core.Models;
using Fang.Notification.WeCom.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.WeCom
{
    public class WeComChannel : IMessageChannel, IBatchMessageChannel
    {
        private readonly HttpClient _httpClient;
        private readonly WeComOptions _options;
        private readonly IJsonSerializer _serializer;
        private readonly InMemoryTokenCache _tokenCache;
        private readonly MessagePipeline _pipeline;

        public string ChannelName => "wecom";
        public string DisplayName => "企业微信";

        public IReadOnlyList<MessageType> SupportedTypes => new List<MessageType>
        {
            MessageType.Text,
            MessageType.Markdown,
            MessageType.News,
            MessageType.Image,
            MessageType.File
        };

        public WeComChannel(
            HttpClient httpClient,
            IOptions<WeComOptions> options,
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
                    $"企业微信不支持消息类型: {message.Type}");

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
                    return await SendViaApi(message, receiver, cancellationToken);
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
            var wecomMessage = new WeComMessageBuilder().BuildMessage(message);
            var json = _serializer.Serialize(wecomMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var wecomResponse = _serializer.Deserialize<WeComResponse>(responseBody);
                if (wecomResponse.errcode == 0)
                    return SendResult.Success(ChannelName, message.MessageId);
                return SendResult.Failure(ChannelName, message.MessageId,
                    wecomResponse.errcode.ToString(), wecomResponse.errmsg, responseBody);
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
                300, cancellationToken);

            // 图片/文件消息需先上传获取 media_id
            var resolvedMediaId = await ResolveMediaAsync(message, token, cancellationToken);

            var wecomMessage = BuildApiMessage(message, receiver, resolvedMediaId);
            var json = _serializer.Serialize(wecomMessage);

            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/cgi-bin/message/send?access_token={token}",
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var wecomResponse = _serializer.Deserialize<WeComResponse>(responseBody);
                if (wecomResponse.errcode == 0)
                    return SendResult.Success(ChannelName, message.MessageId);

                if (wecomResponse.errcode == 40014 || wecomResponse.errcode == 42001)
                {
                    _tokenCache.Clear(ChannelName);
                    token = await _tokenCache.GetOrAddAsync(ChannelName,
                        async () => await GetTokenFromApi(cancellationToken),
                        300, cancellationToken);

                    response = await _httpClient.PostAsync(
                        $"{_options.BaseUrl}/cgi-bin/message/send?access_token={token}",
                        new StringContent(json, Encoding.UTF8, "application/json"),
                        cancellationToken);
                    responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        wecomResponse = _serializer.Deserialize<WeComResponse>(responseBody);
                        if (wecomResponse.errcode == 0)
                            return SendResult.Success(ChannelName, message.MessageId);
                    }
                }

                return SendResult.Failure(ChannelName, message.MessageId,
                    wecomResponse.errcode.ToString(), wecomResponse.errmsg, responseBody);
            }

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private object BuildApiMessage(NotificationMessage message, MessageReceiver receiver, string resolvedMediaId = null)
        {
            var msg = new WeComMessageRequest
            {
                touser = string.Join("|", receiver.Identifiers
                    .Where(kv => kv.Key == ReceiverType.UserId)
                    .SelectMany(kv => kv.Value)),
                toparty = string.Join("|", receiver.Identifiers
                    .Where(kv => kv.Key == ReceiverType.DepartmentId)
                    .SelectMany(kv => kv.Value)),
                agentid = _options.AgentId ?? 0
            };

            switch (message)
            {
                case TextMessage text:
                    msg.msgtype = "text";
                    msg.text = new WeComTextMessage { content = text.Content };
                    break;
                case MarkdownMessage markdown:
                    msg.msgtype = "markdown";
                    msg.markdown = new WeComMarkdownMessage { content = markdown.Content };
                    break;
                case ImageMessage image:
                    msg.msgtype = "image";
                    msg.image = new WeComImageContent
                    {
                        media_id = resolvedMediaId ?? image.MediaId ?? image.ImageUrl
                    };
                    break;
                case FileMessage file:
                    msg.msgtype = "file";
                    msg.file = new WeComFileContent
                    {
                        media_id = resolvedMediaId ?? file.MediaId ?? file.FileUrl
                    };
                    break;
                case NewsMessage news:
                    msg.msgtype = "news";
                    msg.news = new WeComNewsMessage
                    {
                        articles = news.Articles.Select(a => new WeComNewsArticle
                        {
                            title = a.Title,
                            description = a.Description,
                            url = a.Url,
                            picurl = a.PicUrl
                        }).ToArray()
                    };
                    break;
                default:
                    throw new NotSupportedException($"企业微信API不支持消息类型: {message.Type}");
            }

            return msg;
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
                    fileName = Path.GetFileName(new Uri(img.ImageUrl).AbsolutePath);
                    if (string.IsNullOrWhiteSpace(fileName)) fileName = "image.jpg";
                    break;
                case ImageMessage img when !string.IsNullOrEmpty(img.ImageBase64):
                    fileData = Convert.FromBase64String(img.ImageBase64);
                    fileName = "image.jpg";
                    break;
                case FileMessage file when !string.IsNullOrEmpty(file.FileUrl):
                    fileName = string.IsNullOrEmpty(file.FileName) ? Path.GetFileName(file.FileUrl) : file.FileName;
                    fileData = File.ReadAllBytes(file.FileUrl);
                    break;
                case FileMessage file when file.FileBytes != null:
                    fileName = string.IsNullOrEmpty(file.FileName) ? "file" : file.FileName;
                    fileData = file.FileBytes;
                    break;
                default:
                    throw new InvalidOperationException("企业微信上传需要提供文件数据");
            }

            using var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(fileData), "media", fileName);

            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/cgi-bin/media/upload?access_token={token}&type={mediaType}",
                formData,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var uploadResponse = _serializer.Deserialize<WeComMediaUploadResponse>(responseBody);
                if (uploadResponse.errcode == 0 && !string.IsNullOrEmpty(uploadResponse.media_id))
                {
                    return uploadResponse.media_id;
                }

                throw new InvalidOperationException(
                    $"企业微信媒体上传失败: errcode={uploadResponse.errcode}, errmsg={uploadResponse.errmsg}");
            }

            throw new HttpRequestException(
                $"企业微信媒体上传HTTP错误: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        private async Task<TokenCacheEntry> GetTokenFromApi(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                $"{_options.BaseUrl}/cgi-bin/gettoken?corpid={_options.CorpId}&corpsecret={_options.CorpSecret}",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = _serializer.Deserialize<WeComTokenResponse>(responseBody);

            return new TokenCacheEntry
            {
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 300)
            };
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

        private void LogMessage(string msg, LogLevel level)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
        }
    }
}
