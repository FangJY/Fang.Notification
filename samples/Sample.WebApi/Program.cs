using Fang.Notification.Core.Models;
using Fang.Notification.Facade;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFangNotification();
builder.Services.AddFeishu(options =>
{
    builder.Configuration.GetSection("Notification:Feishu").Bind(options);
});
builder.Services.AddDingTalk(options =>
{
    builder.Configuration.GetSection("Notification:DingTalk").Bind(options);
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
