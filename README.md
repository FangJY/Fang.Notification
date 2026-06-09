# Fang.Notification

多平台消息推送 .NET 类库，为钉钉、飞书、企业微信、邮件和 Microsoft Teams 提供统一的发送接口。

## 特性

- 统一接口：无论目标平台如何，调用方式保持一致
- 多平台支持：钉钉、飞书、企业微信、邮件、Microsoft Teams
- 消息类型：文本、Markdown、图片、文件等
- 发送方式：Webhook 机器人、应用消息（需平台凭据）
- 内置中间件：自动重试、超时控制、日志记录
- Token 管理：内存缓存 + 自动刷新
- 高度可扩展：通过接口扩展新平台

## 支持平台

| 平台 | Webhook | 应用消息 | 群聊 | 文件 |
|------|---------|----------|------|------|
| 钉钉 | ✓ | ✓ | ✓ | ✓ |
| 飞书 | ✓ | ✓ | ✓ | ✓ |
| 企业微信 | ✓ | ✓ | ✓ | ✓ |
| 邮件 | - | ✓ | - | ✓ |
| Teams | ✓ | ✓ | ✓ | - |

## 安装

```bash
dotnet add package Fang.Notification
```

## 快速开始

### 1. 注册服务

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// 注册核心服务
services.AddFangNotification();

// 根据需要添加平台通道
services.AddFeishu(options =>
{
    options.AppId = "your-app-id";
    options.AppSecret = "your-app-secret";
    options.WebhookUrl = "https://open.feishu.cn/open-apis/bot/v2/hook/your-hook-id";
});

services.AddDingTalk(options =>
{
    options.WebhookUrl = "https://oapi.dingtalk.com/robot/send?access_token=your-token";
    options.WebhookSecret = "SECyour-secret";
});

services.AddWeCom(options =>
{
    options.WebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=your-key";
    options.CorpId = "your-corp-id";
    options.CorpSecret = "your-corp-secret";
    options.AgentId = 1000001;
});

var provider = services.BuildServiceProvider();
```

### 2. 发送消息

```csharp
using Fang.Notification.Core.Models;
using Fang.Notification.Facade;

var notificationService = provider.GetRequiredService<NotificationService>();

// 发送文本消息
var textMessage = new TextMessage
{
    Title = "系统通知",
    Content = "服务器运行正常"
};

// 通过 Webhook 发送
var receiver = MessageReceiver.FromWebhook("https://oapi.dingtalk.com/robot/send?access_token=xxx");
var result = await notificationService.SendAsync("dingtalk", textMessage, receiver);

// 发送 Markdown 消息
var markdownMessage = new MarkdownMessage
{
    Title = "状态报告",
    Content = "# 服务器状态\n\n- **CPU**: 正常\n- **内存**: 45%"
};

// 发送文件
var fileMessage = new FileMessage
{
    FileUrl = @"C:\path\to\file.xlsx"
};
```

### 3. 消息接收者

```csharp
// Webhook 方式
var receiver = MessageReceiver.FromWebhook("https://...");

// 用户 ID
var user = MessageReceiver.FromUserId("user-id");

// 群聊 ID
var group = MessageReceiver.FromGroupChatId("chat-id");
```

## 配置说明

### 钉钉 (DingTalk)

```csharp
services.AddDingTalk(options =>
{
    options.ClientID = "";           // 应用 AppKey（机器人编码时使用）
    options.ClientSecret = "";       // 应用 AppSecret
    options.CorpId = "";             // 企业 ID
    options.AgentId = 0;             // 应用 AgentId
    options.WebhookUrl = "";         // 机器人 Webhook 地址
    options.WebhookSecret = "";      // 机器人加签密钥
    options.RobotCode = "";          // 机器人编码（同 ClientID）
    options.BaseUrl = "https://api.dingtalk.com";
    options.TokenRefreshAdvanceSeconds = 300;
});
```

### 飞书 (Feishu)

```csharp
services.AddFeishu(options =>
{
    options.AppId = "";              // 应用 App ID
    options.AppSecret = "";          // 应用 App Secret
    options.WebhookUrl = "";         // 机器人 Webhook 地址
    options.VerificationToken = "";  // 验证 Token（事件订阅）
    options.EncryptKey = "";         // 加密密钥
    options.BaseUrl = "https://open.feishu.cn";
    options.TokenType = "tenant";    // "tenant" 或 "user"
    options.TokenRefreshAdvanceSeconds = 300;
});
```

### 企业微信 (WeCom)

```csharp
services.AddWeCom(options =>
{
    options.CorpId = "";             // 企业 ID
    options.CorpSecret = "";         // 应用 Secret
    options.AgentId = 0;             // 应用 AgentId
    options.WebhookUrl = "";         // 机器人 Webhook 地址
    options.BaseUrl = "https://qyapi.weixin.qq.com";
    options.UploadUrl = "";          // 文件上传地址
});
```

### Microsoft Teams

```csharp
services.AddTeams(options =>
{
    options.TenantId = "";           // Azure AD 租户 ID
    options.ClientId = "";           // 应用 Client ID
    options.ClientSecret = "";       // 应用 Client Secret
    options.TeamId = "";             // Team ID
    options.ChannelId = "";          // Channel ID
    options.WebhookUrl = "";         // Incoming Webhook URL
    options.GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    options.AuthorityUrl = "https://login.microsoftonline.com";
    options.Scopes = new[] { "https://graph.microsoft.com/.default" };
});
```

### 邮件 (Email)

```csharp
services.AddEmail(options =>
{
    options.SmtpHost = "smtp.example.com";
    options.Port = 587;
    options.FromAddress = "sender@example.com";
    options.FromName = "系统通知";
    options.UserName = "smtp-user";
    options.Password = "smtp-password";
    options.EnableSsl = true;
    options.IsHtml = true;
    options.Encoding = "UTF-8";
});
```

## 核心配置

```csharp
services.AddFangNotification(options =>
{
    options.MaxConcurrentSends = 10;      // 最大并发发送数
    options.EnableMessageLogging = true;  // 启用消息日志
});
```

## 环境变量配置

推荐通过环境变量管理敏感凭据，示例见 `.env.example`：

```bash
# 飞书
FEISHU_APP_ID=cli_xxx
FEISHU_APP_SECRET=xxx
FEISHU_WEBHOOK_URL=https://open.feishu.cn/open-apis/bot/v2/hook/xxx

# 钉钉
DINGTALK_WEBHOOK_URL=https://oapi.dingtalk.com/robot/send?access_token=xxx
DINGTALK_WEBHOOK_SECRET=SECxxx
DINGTALK_CORP_ID=dingxxx
DINGTALK_CLIENT_ID=dingxxx
DINGTALK_CLIENT_SECRET=xxx

# 企业微信
WECOM_WEBHOOK_URL=https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxx
WECOM_CORP_ID=wwxxx
WECOM_CORP_SECRET=xxx
WECOM_AGENT_ID=1000001
```

## 高级功能

### 重试策略

内置自动重试机制，可通过配置调整：

```csharp
services.AddDingTalk(options =>
{
    // ... 其他配置
});
```

### Token 管理

库内置内存级 Token 缓存，支持自动刷新。Token 在过期前会自动续期。

### 中间件管道

消息发送经过中间件管道处理：
- 超时控制
- 自动重试
- 日志记录

## 扩展开发

实现 `IMessageChannel` 接口即可添加新平台：

```csharp
public class MyPlatformChannel : IMessageChannel
{
    public string Name => "myplatform";
    
    public async Task<SendResult> SendAsync(
        NotificationMessage message,
        MessageReceiver receiver,
        CancellationToken cancellationToken = default)
    {
        // 实现发送逻辑
    }
}
```

注册新通道：

```csharp
services.Configure<MyPlatformOptions>(config);
services.AddHttpClient<MyPlatformChannel>();
services.AddSingleton<IMessageChannel, MyPlatformChannel>();
```

## 项目结构

```
src/
  Fang.Notification.Core/         # 核心抽象层
  Fang.Notification.Common/       # 公共实现
  Fang.Notification.Facade/       # 门面层
  Fang.Notification.DingTalk/     # 钉钉实现
  Fang.Notification.Feishu/       # 飞书实现
  Fang.Notification.WeCom/        # 企业微信实现
  Fang.Notification.Teams/        # Teams 实现
  Fang.Notification.Email/        # 邮件实现
samples/
  Sample.Console/                 # 控制台示例
  Sample.WebApi/                  # Web API 示例
```

## 目标框架

- .NET Standard 2.0
- .NET Standard 2.1

## 贡献

欢迎提交 Issue 和 Pull Request。

## License

MIT
