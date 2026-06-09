using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fang.Notification.Common.TokenManagement;
using Fang.Notification.Core.Abstractions;
using Fang.Notification.Core.Models;
using Fang.Notification.WeCom.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace Fang.Notification.WeCom.Tests
{
    [TestClass]
    public class WeComChannelTests
    {
        private Mock<HttpMessageHandler> _mockHttp;
        private HttpClient _httpClient;
        private Mock<IOptions<WeComOptions>> _mockOptions;
        private Mock<IJsonSerializer> _mockSerializer;
        private InMemoryTokenCache _tokenCache;
        private WeComChannel _channel;

        [TestInitialize]
        public void Setup()
        {
            _mockHttp = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttp.Object);
            _mockOptions = new Mock<IOptions<WeComOptions>>();
            _mockSerializer = new Mock<IJsonSerializer>();
            _tokenCache = new InMemoryTokenCache();

            _mockOptions.Setup(o => o.Value).Returns(new WeComOptions
            {
                CorpId = "test_corp_id",
                CorpSecret = "test_corp_secret",
                AgentId = 1000001,
                BaseUrl = "https://qyapi.weixin.qq.com",
                WebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=test-key"
            });

            _channel = new WeComChannel(
                _httpClient, _mockOptions.Object, _mockSerializer.Object, _tokenCache);
        }

        // ──────────────────────────────
        // Text message - Webhook
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_TextMessage_Webhook_ShouldSucceed()
        {
            var message = new TextMessage { Content = "Hello WeCom" };
            var receiver = MessageReceiver.FromWebhook("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=test-key");

            SetupHttpResponse(HttpStatusCode.OK, "{\"errcode\":0,\"errmsg\":\"ok\"}");
            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
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

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"test_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"invaliduser\":\"\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "test_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
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

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"md_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "md_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // Image message - via MediaId (API)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_ImageMessage_WithMediaId_ShouldSucceed()
        {
            var message = new ImageMessage
            {
                MediaId = "test_media_id_for_image",
                Title = "图片测试"
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_img");

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"img_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "img_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // File message - via MediaId (API)
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_FileMessage_WithMediaId_ShouldSucceed()
        {
            var message = new FileMessage
            {
                MediaId = "test_media_id_for_file",
                FileName = "test.xlsx"
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_file");

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"file_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "file_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // News message - API
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_NewsMessage_Api_ShouldSucceed()
        {
            var message = new NewsMessage
            {
                Title = "新闻",
                Articles = new List<NewsArticle>
                {
                    new NewsArticle
                    {
                        Title = "文章标题",
                        Description = "文章描述",
                        Url = "https://example.com/article1",
                        PicUrl = "https://example.com/pic1.jpg"
                    }
                }
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_news");

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"news_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "news_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess);
        }

        // ──────────────────────────────
        // Unsupported message type
        // ──────────────────────────────

        [TestMethod]
        public async Task SendAsync_UnsupportedType_ShouldReturnFailure()
        {
            // CardMessage is NOT in WeComChannel.SupportedTypes
            var message = new CardMessage { TemplateData = "{}" };
            var receiver = MessageReceiver.FromWebhook("https://hook.test");

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("UNSUPPORTED_TYPE", result.ErrorCode);
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

            // token endpoint returns success
            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"expired_token\",\"expires_in\":7200}");
            // send endpoint returns 40014 (invalid access_token)
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":40014,\"errmsg\":\"invalid access_token\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "expired_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 40014, errmsg = "invalid access_token" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("40014", result.ErrorCode);
        }

        // ──────────────────────────────
        // Image message - from Base64 (upload + API send)
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
                .AddIdentifier(ReceiverType.UserId, "user_img_upload");

            // HTTP: token
            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"img_upload_token\",\"expires_in\":7200}");
            // HTTP: media upload
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@img_uploaded_789\",\"type\":\"image\",\"created_at\":1605863153573}");
            // HTTP: message send
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "img_upload_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new WeComMediaUploadResponse
                {
                    errcode = 0,
                    media_id = "@img_uploaded_789",
                    type = "image",
                    created_at = 1605863153573
                });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess, $"Image upload + send should succeed. Error: {result.ErrorMessage}");

            // Verify upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // File message - from bytes (upload + API send)
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
                .AddIdentifier(ReceiverType.UserId, "user_file_upload");

            // HTTP: token
            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"file_upload_token\",\"expires_in\":7200}");
            // HTTP: media upload
            SetupHttpResponseForPath("media/upload", HttpStatusCode.OK,
                "{\"errcode\":0,\"media_id\":\"@file_uploaded_abc\",\"type\":\"file\",\"created_at\":1605863153573}");
            // HTTP: message send
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "file_upload_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComMediaUploadResponse>(It.IsAny<string>()))
                .Returns(new WeComMediaUploadResponse
                {
                    errcode = 0,
                    media_id = "@file_uploaded_abc",
                    type = "file",
                    created_at = 1605863153573
                });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var result = await _channel.SendAsync(message, receiver);

            Assert.IsTrue(result.IsSuccess, $"File upload + send should succeed. Error: {result.ErrorMessage}");

            // Verify upload HTTP call was actually made
            _mockHttp.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null &&
                    r.RequestUri.ToString().Contains("media/upload")),
                ItExpr.IsAny<CancellationToken>());
        }

        // ──────────────────────────────
        // File message - Webhook (unsupported)
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
        }

        // ──────────────────────────────
        // Batch send
        // ──────────────────────────────

        [TestMethod]
        public async Task SendBatchAsync_MultipleMessages_ShouldSucceed()
        {
            var messages = new List<NotificationMessage>
            {
                new TextMessage { Content = "消息1" },
                new TextMessage { Content = "消息2" },
                new TextMessage { Content = "消息3" }
            };
            var receiver = new MessageReceiver()
                .AddIdentifier(ReceiverType.UserId, "user_batch");

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"batch_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "batch_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var results = await _channel.SendBatchAsync(messages, receiver);
            var resultList = results.ToList();

            Assert.AreEqual(3, resultList.Count);
            Assert.IsTrue(resultList.All(r => r.IsSuccess));
        }

        // ──────────────────────────────
        // SendToMany
        // ──────────────────────────────

        [TestMethod]
        public async Task SendToManyAsync_MultipleReceivers_ShouldSucceed()
        {
            var message = new TextMessage { Content = "群发消息" };
            var receivers = new List<MessageReceiver>
            {
                new MessageReceiver().AddIdentifier(ReceiverType.UserId, "user_a"),
                new MessageReceiver().AddIdentifier(ReceiverType.UserId, "user_b")
            };

            SetupHttpResponseForPath("gettoken", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"many_token\",\"expires_in\":7200}");
            SetupHttpResponseForPath("message/send", HttpStatusCode.OK,
                "{\"errcode\":0,\"errmsg\":\"ok\"}");

            _mockSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            _mockSerializer.Setup(s => s.Deserialize<WeComTokenResponse>(It.IsAny<string>()))
                .Returns(new WeComTokenResponse { errcode = 0, access_token = "many_token", expires_in = 7200 });
            _mockSerializer.Setup(s => s.Deserialize<WeComResponse>(It.IsAny<string>()))
                .Returns(new WeComResponse { errcode = 0, errmsg = "ok" });

            var results = await _channel.SendToManyAsync(message, receivers);
            var resultList = results.ToList();

            Assert.AreEqual(2, resultList.Count);
            Assert.IsTrue(resultList.All(r => r.IsSuccess));
        }

        // ──────────────────────────────
        // Helpers
        // ──────────────────────────────

        private void SetupHttpResponse(HttpStatusCode statusCode, string responseBody)
        {
            _mockHttp.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseBody)
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
