using System.Collections.Generic;

namespace Fang.Notification.DingTalk.Models
{
    public class DingTalkResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string task_id { get; set; }
        public string processQueryCode { get; set; }
    }

    public class DingTalkSendRequest
    {
        public string agent_id { get; set; }
        public string userid_list { get; set; }
        public DingTalkMessageContent msg { get; set; }
    }

    public class DingTalkMessageContent
    {
        public string msgtype { get; set; }
        public DingTalkText text { get; set; }
        public DingTalkMarkdown markdown { get; set; }
        public DingTalkImage image { get; set; }
        public DingTalkFile file { get; set; }
    }

    public class DingTalkText
    {
        public string content { get; set; }
    }

    public class DingTalkMarkdown
    {
        public string title { get; set; }
        public string text { get; set; }
    }

    public class DingTalkImage
    {
        public string media_id { get; set; }
    }

    public class DingTalkFile
    {
        public string media_id { get; set; }
    }

    public class DingTalkMediaUploadResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string media_id { get; set; }
        public long created_at { get; set; }
        public string type { get; set; }
    }

    /// <summary>
    /// 新 API OAuth2 令牌响应
    /// </summary>
    public class DingTalkOAuthTokenResponse
    {
        public string accessToken { get; set; }
        public int expireIn { get; set; }
    }

    /// <summary>
    /// 旧版工作通知 API 响应（topapi/message/corpconversation/asyncsend_v2）
    /// </summary>
    public class DingTalkWorkNoticeResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public long task_id { get; set; }
        public string request_id { get; set; }
    }

    /// <summary>
    /// 新 API 上传响应（字段为驼峰命名）
    /// </summary>
    public class DingTalkNewUploadResponse
    {
        public string mediaId { get; set; }
    }

}
