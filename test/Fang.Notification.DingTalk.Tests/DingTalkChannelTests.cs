using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Fang.Notification.DingTalk.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace Fang.Notification.DingTalk.Tests
{
    [TestClass]
    public class DingTalkChannelTests
    {
        private Mock<HttpMessageHandler> _mockHttp;
        private HttpClient _httpClient;
        private Mock<IOptions<DingTalkOptions>> _mockOptions;
        private Mock<IJsonSerializer> _mockSerializer;
        private InMemoryTokenCache _tokenCache;
        private DingTalkChannel _channel;

        [TestInitialize]
        public void Setup()
        {
            _mockHttp = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttp.Object);
            _mockOptions = new Mock<IOptions<DingTalkOptions>>();
            _mockSerializer = new Mock<IJsonSerializer>();
            _tokenCache = new InMemoryTokenCache();

            _mockOptions.Setup(o => o.Value).Returns(new DingTalkOptions
            {
                WebhookUrl = "https://oapi.dingtalk.com/robot/send",
                BaseUrl = "https://api.dingtalk.com",
                ClientID = "test_client_id",
                ClientSecret = "test_client_secret",
                AgentId = 4650060916,
                RobotCode = "test_robot_code",
                TokenRefreshAdvanceSeconds = 300
            });

            _channel = new DingTalkChannel(
                _httpClient, _mockOptions.Object, _mockSerializer.Object, _tokenCache);
        }

        // ──────────────────────────────
        // Text message - Webhook
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_Webhook_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello" };
            var receiver = MessageReceiver.FromWebhook("https://hook.test");

            SetupHttpResponse(HttpStatusCode.OK, "{\"errcode\":0,\"task_id\":\"task_123\"}");
            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<DingTalkResponse>(It.IsAny<string>()))
                .Returns(new DingTalkResponse { errcode = 0, task_id = "task_123" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("task_123", result.RemoteMessageId);
        }

        // ──────────────────────────────
        // Unsupported message type
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_UnsupportedType_ShouldReturnFailure()
        {
            var message = new NewsMessage { Content = "News" };
            var receiver = MessageReceiver.FromWebhook("https://hook.test");

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("UNSUPPORTED_TYPE", result.ErrorCode);
        }

        // ──────────────────────────────
        // Text message - API (via UserId)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_Api_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello via API" };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user123");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"test_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("asyncsend_v2", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"task_id\":256271667526,\"request_id\":\"req_123\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "test_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkWorkNoticeResponse>(It.IsAny<string>()))
                .Returns(new DingTalkWorkNoticeResponse { errcode = 0, errmsg = "ok", task_id = 256271667526 });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("256271667526", result.RemoteMessageId);
        }

        // ──────────────────────────────
        // Markdown message - API
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_MarkdownMessage_Api_ShouldSucceed()
        {
            var message = new MarkdownMessage
            {
                Title = "Test Title",
                Content = "**bold** and *italic*"
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user456");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"md_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("asyncsend_v2", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"task_id\":256271667527,\"request_id\":\"req_456\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "md_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkWorkNoticeResponse>(It.IsAny<string>()))
                .Returns(new DingTalkWorkNoticeResponse { errcode = 0, errmsg = "ok", task_id = 256271667527 });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("256271667527", result.RemoteMessageId);
        }

        // ──────────────────────────────
        // Image message - from Base64 (API upload + send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_ImageMessage_FromBase64_ShouldSucceed()
        {
            var imageBase64 = Convert.ToBase64String(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // small PNG header

            var message = new ImageMessage
            {
                ImageBase64 = imageBase64,
                Title = "本地图片上传测试"
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user789");

            // HTTP: token
            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"img_upload_token\",\"expireIn\":7200}");
            // HTTP: image upload
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@img_uploaded_789\",\"type\":\"image\",\"created_at\":1605863153573}");
            // HTTP: message send
            SetupHttpResponseForPath("asyncsend_v2", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"task_id\":256271667528,\"request_id\":\"req_img_789\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "img_upload_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new DingTalkMediaUploadResponse
                {
                    errcode = 0,
                    media_id = "@img_uploaded_789",
                    type = "image",
                    created_at = 1605863153573
                });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkWorkNoticeResponse>(It.IsAny<string>()))
                .Returns(new DingTalkWorkNoticeResponse { errcode = 0, errmsg = "ok", task_id = 256271667528 });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Image upload + send should succeed. Error: {result.ErrorMessage}");
            Assert.AreEqual("256271667528", result.RemoteMessageId);

            // Verify upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // File message - from FileBytes (API upload + send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_FileMessage_FromBytes_ShouldSucceed()
        {
            var message = new FileMessage
            {
                FileBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 },
                FileName = "test.bin"
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_file");

            // HTTP: token
            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"file_upload_token\",\"expireIn\":7200}");
            // HTTP: file upload
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@file_uploaded_abc\",\"type\":\"file\",\"created_at\":1605863153573}");
            // HTTP: message send
            SetupHttpResponseForPath("asyncsend_v2", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"task_id\":256271667529,\"request_id\":\"req_file_abc\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "file_upload_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new DingTalkMediaUploadResponse
                {
                    errcode = 0,
                    media_id = "@file_uploaded_abc",
                    type = "file",
                    created_at = 1605863153573
                });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkWorkNoticeResponse>(It.IsAny<string>()))
                .Returns(new DingTalkWorkNoticeResponse { errcode = 0, errmsg = "ok", task_id = 256271667529 });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess, $"File upload + send should succeed. Error: {result.ErrorMessage}");
            Assert.AreEqual("256271667529", result.RemoteMessageId);

            // Verify file upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // Token expired - auto retry
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_Api_TokenExpired_ShouldRetry()
        {
            var message = new TextMessage { Content = "Retry test" };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_retry");

            // HTTP: token - called twice (first success, then after expiry)
            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"expired_token\",\"expireIn\":7200}");
            // HTTP: send - first returns 40001 (token expired)
            SetupHttpResponseForPath("asyncsend_v2", HttpStatusCode.OK,
                "{\"errcode\":40001,\"errmsg\":\"invalid credential\",\"request_id\":\"req_retry_1\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "expired_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkWorkNoticeResponse>(It.IsAny<string>()))
                .Returns(new DingTalkWorkNoticeResponse { errcode = 40001, errmsg = "invalid credential" });

            var result = await _channel.SendAsync(message, receiver);

            // Should fail after exhausting retry (both attempts use expired_token)
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("40001", result.ErrorCode);
        }

        // ──────────────────────────────
        // File message - Webhook (not supported)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_FileMessage_Webhook_ShouldReturnFailure()
        {
            var message = new FileMessage
            {
                FileBytes = new byte[] { 0x01, 0x02, 0x03 },
                FileName = "test.bin"
            };
            var receiver = MessageReceiver.FromWebhook("https://hook.test");

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("EXCEPTION", result.ErrorCode);
            StringAssert.Contains(result.ErrorMessage, "钉钉Webhook不支持文件消息");
        }

        // ──────────────────────────────
        // Group chat - Text message (chat/send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_GroupChat_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello group" };
            var receiver = MessageReceiver.FromGroupChatId("chate39f5xxxxxx335");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"chat_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("chat/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "chat_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkResponse>(It.IsAny<string>()))
                .Returns(new DingTalkResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // Group chat - Markdown message (chat/send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_MarkdownMessage_GroupChat_ShouldSucceed()
        {
            var message = new MarkdownMessage { Title = "Notice", Content = "**Alert** from chat" };
            var receiver = MessageReceiver.FromGroupChatId("chat_md_group");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"md_chat_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("chat/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "md_chat_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkResponse>(It.IsAny<string>()))
                .Returns(new DingTalkResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // Group chat - Image message (upload + chat/send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_ImageMessage_GroupChat_ShouldSucceed()
        {
            var message = new ImageMessage
            {
                ImageBase64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
                Title = "Chat Image"
            };
            var receiver = MessageReceiver.FromGroupChatId("chat_img_group");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"img_chat_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@img_upload_to_chat\",\"type\":\"image\"}");
            SetupHttpResponseForPath("chat/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "img_chat_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new DingTalkMediaUploadResponse { errcode = 0, media_id = "@img_upload_to_chat" });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkResponse>(It.IsAny<string>()))
                .Returns(new DingTalkResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);

            // Verify upload was called
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // Group chat - File message (upload + chat/send)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_FileMessage_GroupChat_ShouldSucceed()
        {
            var message = new FileMessage
            {
                FileBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                FileName = "report.xlsx"
            };
            var receiver = MessageReceiver.FromGroupChatId("chat_file_group");

            SetupHttpResponseForPath("oauth2/accessToken", HttpStatusCode.OK,
                "{\"accessToken\":\"file_chat_token\",\"expireIn\":7200}");
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@file_upload_to_chat\",\"type\":\"file\"}");
            SetupHttpResponseForPath("chat/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkOAuthTokenResponse>(It.IsAny<string>()))
                .Returns(new DingTalkOAuthTokenResponse { accessToken = "file_chat_token", expireIn = 7200 });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new DingTalkMediaUploadResponse { errcode = 0, media_id = "@file_upload_to_chat" });
            _mockSerializer
                .Setup(s => s.Deserialize<DingTalkResponse>(It.IsAny<string>()))
                .Returns(new DingTalkResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);

            // Verify upload was called
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // Helpers
        // ──────────────────────────────

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _mockHttp.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
        }

        private void SetupHttpResponseForPath(
            string pathSubstring, HttpStatusCode statusCode, string responseBody)
        {
            _mockHttp.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri != null &&
                        r.RequestUri.ToString().Contains(pathSubstring)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody)
                });
        }
    }
}
