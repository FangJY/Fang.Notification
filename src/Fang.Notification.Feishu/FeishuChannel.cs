using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Fang.Notification.Feishu.Models;
using Microsoft.Extensions.Options;

namespace Fang.Notification.Feishu
{
    public class FeishuChannel : IMessageChannel, ITemplateMessageChannel, IBatchMessageChannel
    {
        private readonly HttpClient _httpClient;
        private readonly FeishuOptions _options;
        private readonly IJsonSerializer _serializer;
        private readonly InMemoryTokenCache _tokenCache;
        private readonly MessagePipeline _pipeline;

        public string ChannelName => "feishu";
        public string DisplayName => "飞书";

        public IReadOnlyList<MessageType> SupportedTypes => new List<MessageType>
        {
            MessageType.Text,
            MessageType.Markdown,
            MessageType.Card,
            MessageType.Image,
            MessageType.File,
            MessageType.Template
        };

        public FeishuChannel(
            HttpClient httpClient,
            IOptions<FeishuOptions> options,
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
                    $"飞书不支持消息类型: {message.Type}");

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
            var feishuMessage = BuildWebhookMessage(message);
            var json = _serializer.Serialize(feishuMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var feishuResponse = _serializer.Deserialize<FeishuResponse>(responseBody);
                if (feishuResponse.Code == 0)
                {
                    return SendResult.Success(ChannelName, message.MessageId, feishuResponse.Data?.MessageId);
                }
                return SendResult.Failure(ChannelName, message.MessageId,
                    feishuResponse.Code.ToString(), feishuResponse.Msg, responseBody);
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

            // 图片/文件消息需先上传获取 media key
            var resolvedMediaKey = await ResolveMediaKeyAsync(message, token, cancellationToken);

            var feishuMessage = BuildApiMessage(message, receiver, resolvedMediaKey);
            var json = _serializer.Serialize(feishuMessage);

            var receiveIdType = GetReceiveIdType(receiver);
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_options.BaseUrl}/open-apis/im/v1/messages?receive_id_type={receiveIdType}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var feishuResponse = _serializer.Deserialize<FeishuResponse>(responseBody);
                if (feishuResponse.Code == 0)
                {
                    return SendResult.Success(ChannelName, message.MessageId, feishuResponse.Data?.MessageId);
                }

                if (feishuResponse.Code == 99991663 || feishuResponse.Code == 99991661)
                {
                    _tokenCache.Clear(ChannelName);
                    token = await _tokenCache.GetOrAddAsync(ChannelName,
                        async () => await GetTokenFromApi(cancellationToken),
                        _options.TokenRefreshAdvanceSeconds,
                        cancellationToken);

                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    response = await _httpClient.SendAsync(request, cancellationToken);
                    responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        feishuResponse = _serializer.Deserialize<FeishuResponse>(responseBody);
                        if (feishuResponse.Code == 0)
                        {
                            return SendResult.Success(ChannelName, message.MessageId, feishuResponse.Data?.MessageId);
                        }
                    }
                }

                return SendResult.Failure(ChannelName, message.MessageId,
                    feishuResponse.Code.ToString(), feishuResponse.Msg, responseBody);
            }

            return SendResult.Failure(ChannelName, message.MessageId,
                ((int)response.StatusCode).ToString(), response.ReasonPhrase, responseBody);
        }

        private object BuildWebhookMessage(NotificationMessage message)
        {
            return message switch
            {
                TextMessage text => new
                {
                    msg_type = "text",
                    content = new { text = text.Content }
                },
                ImageMessage image => new
                {
                    msg_type = "image",
                    content = new { image_key = image.MediaId ?? image.ImageUrl }
                },
                CardMessage card => new
                {
                    msg_type = "interactive",
                    card = _serializer.Deserialize<object>(card.TemplateData)
                },
                _ => throw new NotSupportedException($"飞书Webhook不支持消息类型: {message.Type}")
            };
        }

        private object BuildApiMessage(NotificationMessage message, MessageReceiver receiver, string resolvedMediaKey = null)
        {
            // v1 API 使用 receive_id + receive_id_type(query) 标识接收者
            var receiveId = receiver.GetFirst(ReceiverType.GroupChatId)
                ?? receiver.GetFirst(ReceiverType.OpenId)
                ?? receiver.GetFirst(ReceiverType.UserId);

            var baseMsg = new Dictionary<string, object>
            {
                ["receive_id"] = receiveId
            };

            // v1 API content 字段必须是 JSON 字符串
            switch (message)
            {
                case TextMessage text:
                    baseMsg["msg_type"] = "text";
                    baseMsg["content"] = _serializer.Serialize(new { text = text.Content });
                    break;
                case ImageMessage _:
                    baseMsg["msg_type"] = "image";
                    baseMsg["content"] = _serializer.Serialize(new { image_key = resolvedMediaKey });
                    break;
                case FileMessage _:
                    baseMsg["msg_type"] = "file";
                    baseMsg["content"] = _serializer.Serialize(new { file_key = resolvedMediaKey });
                    break;
                case CardMessage card:
                    baseMsg["msg_type"] = "interactive";
                    // TemplateData 已是 JSON 字符串，不重复序列化
                    baseMsg["content"] = card.TemplateData;
                    break;
                default:
                    throw new NotSupportedException($"飞书API不支持消息类型: {message.Type}");
            }

            return baseMsg;
        }

        private static string GetReceiveIdType(MessageReceiver receiver)
        {
            if (receiver.HasType(ReceiverType.GroupChatId)) return "chat_id";
            if (receiver.HasType(ReceiverType.OpenId)) return "open_id";
            if (receiver.HasType(ReceiverType.UserId)) return "user_id";
            throw new ArgumentException("飞书 API 发送需要指定 GroupChatId、OpenId 或 UserId");
        }

        private static string GetFeishuFileType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
            switch (ext)
            {
                case "pdf": return "pdf";
                case "doc":
                case "docx": return "doc";
                case "xls":
                case "xlsx": return "xls";
                case "ppt":
                case "pptx": return "ppt";
                case "mp4": return "mp4";
                case "opus": return "opus";
                default: return "stream";
            }
        }

        private async Task<string> ResolveMediaKeyAsync(NotificationMessage message, string token, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case ImageMessage img when !string.IsNullOrEmpty(img.MediaId):
                    return img.MediaId;
                case ImageMessage img:
                    return await UploadImageAsync(img, token, cancellationToken);
                case FileMessage file when !string.IsNullOrEmpty(file.MediaId):
                    return file.MediaId;
                case FileMessage file:
                    return await UploadFileAsync(file, token, cancellationToken);
                default:
                    return null;
            }
        }

        private async Task<string> UploadImageAsync(ImageMessage image, string token, CancellationToken cancellationToken)
        {
            byte[] imageData;

            if (!string.IsNullOrEmpty(image.ImageUrl))
            {
                imageData = await _httpClient.GetByteArrayAsync(image.ImageUrl);
            }
            else if (!string.IsNullOrEmpty(image.ImageBase64))
            {
                imageData = Convert.FromBase64String(image.ImageBase64);
            }
            else
            {
                throw new InvalidOperationException("飞书发送图片需要设置 ImageUrl 或 ImageBase64（MediaId 为空时）");
            }

            var imageFileName = "image.jpg";
            if (!string.IsNullOrEmpty(image.ImageUrl))
            {
                var extractedName = System.IO.Path.GetFileName(new Uri(image.ImageUrl).AbsolutePath);
                if (!string.IsNullOrWhiteSpace(extractedName))
                    imageFileName = extractedName;
            }

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent("message"), "image_type");
            formData.Add(new ByteArrayContent(imageData), "image", imageFileName);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/open-apis/im/v1/images");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = formData;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var result = _serializer.Deserialize<FeishuUploadResponse>(responseBody);
            if (result?.Code != 0 || string.IsNullOrEmpty(result?.Data?.ImageKey))
                throw new InvalidOperationException($"飞书图片上传失败: {result?.Msg ?? responseBody}");

            return result.Data.ImageKey;
        }

        private async Task<string> UploadFileAsync(FileMessage file, string token, CancellationToken cancellationToken)
        {
            byte[] fileData;
            var fileName = string.IsNullOrEmpty(file.FileName) ? "file" : file.FileName;

            if (!string.IsNullOrEmpty(file.FileUrl))
            {
                // 从本地文件路径读取
                if (string.IsNullOrEmpty(file.FileName))
                    fileName = Path.GetFileName(file.FileUrl);
                fileData = File.ReadAllBytes(file.FileUrl);
            }
            else if (file.FileBytes != null && file.FileBytes.Length > 0)
            {
                fileData = file.FileBytes;
            }
            else
            {
                throw new InvalidOperationException("飞书发送文件需要设置 FileUrl（本地路径）或 FileBytes（MediaId 为空时）");
            }

            var feishuFileType = GetFeishuFileType(fileName);

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(feishuFileType), "file_type");
            formData.Add(new StringContent(fileName), "file_name");
            formData.Add(new ByteArrayContent(fileData), "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/open-apis/im/v1/files");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = formData;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var result = _serializer.Deserialize<FeishuUploadResponse>(responseBody);
            if (result?.Code != 0 || string.IsNullOrEmpty(result?.Data?.FileKey))
                throw new InvalidOperationException($"飞书文件上传失败: {result?.Msg ?? responseBody}");

            return result.Data.FileKey;
        }

        private async Task<TokenCacheEntry> GetTokenFromApi(CancellationToken cancellationToken)
        {
            var body = new { app_id = _options.AppId, app_secret = _options.AppSecret };
            var content = new StringContent(_serializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/open-apis/auth/v3/tenant_access_token/internal", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = _serializer.Deserialize<FeishuTokenResponse>(responseBody);

            return new TokenCacheEntry
            {
                AccessToken = tokenResponse.TenantAccessToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.Expire - 300)
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

        public Task<SendResult> SendTemplateAsync(
            string templateId,
            Dictionary<string, object> templateData,
            MessageReceiver receiver,
            CancellationToken cancellationToken = default)
        {
            var cardMessage = new CardMessage
            {
                TemplateType = templateId,
                TemplateData = _serializer.Serialize(templateData)
            };
            return SendAsync(cardMessage, receiver, cancellationToken);
        }

        private void LogMessage(string msg, LogLevel level)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {msg}");
        }
    }
}
