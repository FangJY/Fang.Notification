using System;
using System.Threading.Tasks;
using Fang.Notification.Core.Models;
using Fang.Notification.Facade;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddFangNotification(options =>
        {
            options.MaxConcurrentSends = 1;
            options.EnableMessageLogging = true;
        })
        .AddFeishu(options =>
        {
            options.AppId = Environment.GetEnvironmentVariable("FEISHU_APP_ID") ?? "your-app-id";
            options.AppSecret = Environment.GetEnvironmentVariable("FEISHU_APP_SECRET") ?? "your-app-secret";
            options.WebhookUrl = Environment.GetEnvironmentVariable("FEISHU_WEBHOOK_URL") ?? "https://open.feishu.cn/open-apis/bot/v2/hook/your-webhook-id";
        })
        .AddDingTalk(options =>
        {
            options.WebhookUrl = Environment.GetEnvironmentVariable("DINGTALK_WEBHOOK_URL") ?? "https://oapi.dingtalk.com/robot/send?access_token=your-access-token";
            options.WebhookSecret = Environment.GetEnvironmentVariable("DINGTALK_WEBHOOK_SECRET") ?? "SECyour-webhook-secret";
            options.CorpId = Environment.GetEnvironmentVariable("DINGTALK_CORP_ID") ?? "your-corp-id";
            options.ClientID = Environment.GetEnvironmentVariable("DINGTALK_CLIENT_ID") ?? "your-client-id";
            options.ClientSecret = Environment.GetEnvironmentVariable("DINGTALK_CLIENT_SECRET") ?? "your-client-secret";
            options.AgentId = long.Parse(Environment.GetEnvironmentVariable("DINGTALK_AGENT_ID") ?? "0");
        });

        var serviceProvider = services.BuildServiceProvider();
        var notificationService = serviceProvider.GetRequiredService<NotificationService>();

        var textMessage = new TextMessage
        {
            Title = "系统通知",
            Content = "服务器运行正常",
            Priority = MessagePriority.Normal
        };

        var markdownMessage = new MarkdownMessage
        {
            Title = "系统状态报告",
            Content = "# 服务器状态\n\n- **CPU**: 正常\n- **内存**: 使用率 45%\n- **磁盘**: 剩余 120GB\n\n> 自动巡检报告"
        };

        var fileMessage = new FileMessage
        {
            FileUrl = "C:\\Users\\cjy56\\Desktop\\考勤\\1.xlsx"
        };

        var user = MessageReceiver.FromUserId("213904134737638319");
        var group = MessageReceiver.FromGroupChatId("替换为实际 chatid"); // chat/send API 使用 chatid

        // Webhook 发送
        //var receiver = MessageReceiver.FromWebhook("https://oapi.dingtalk.com/robot/send?access_token=3d5629ac31c32215e4a07f50918b01f190d47155d5930763c9a5e41fc33fbd86");
        //var result = await notificationService.SendAsync("dingtalk", textMessage, receiver);

        // 工作通知 API 发送
        //var result=await notificationService.SendAsync("dingtalk", fileMessage, user);

        // 群聊发送（chat/send）支持 text / markdown / image / file
        var result = await notificationService.SendAsync("dingtalk", markdownMessage, group);
        if (result.IsSuccess)
            Console.WriteLine($"消息发送成功: {result.RemoteMessageId}");
        else
            Console.WriteLine($"消息发送失败: {result.ErrorMessage}");
        Console.WriteLine("示例运行完成。");
        Console.ReadKey();
    }
}
