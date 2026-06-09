using Newtonsoft.Json;

namespace Fang.Notification.Feishu.Models
{
    public class FeishuResponse
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public FeishuResponseData Data { get; set; }
    }

    public class FeishuResponseData
    {
        public string MessageId { get; set; }
    }

    public class FeishuTokenResponse
    {
        public int Code { get; set; }

        public string Msg { get; set; }

        /// <summary>飞书返回字段为 tenant_access_token（蛇形命名）</summary>
        [JsonProperty("tenant_access_token")]
        public string TenantAccessToken { get; set; }

        public int Expire { get; set; }
    }

    /// <summary>
    /// 飞书上传接口（图片/文件）响应
    /// </summary>
    public class FeishuUploadResponse
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public FeishuUploadData Data { get; set; }
    }

    public class FeishuUploadData
    {
        [JsonProperty("image_key")]
        public string ImageKey { get; set; }

        [JsonProperty("file_key")]
        public string FileKey { get; set; }
    }
}
