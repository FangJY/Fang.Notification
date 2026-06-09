using System.Collections.Generic;

namespace Fang.Notification.Teams.Models
{
    public class TeamsAdaptiveCard
    {
        public string type { get; set; } = "AdaptiveCard";
        public string schema { get; set; } = "http://adaptivecards.io/schemas/adaptive-card.json";
        public string version { get; set; } = "1.4";
        public List<object> body { get; set; } = new List<object>();
        public List<object> actions { get; set; } = new List<object>();
    }

    public class TeamsTextBlock
    {
        public string type { get; set; } = "TextBlock";
        public string text { get; set; }
        public string size { get; set; } = "medium";
        public string weight { get; set; } = "normal";
        public bool wrap { get; set; } = true;
    }

    public class TeamsFactSet
    {
        public string type { get; set; } = "FactSet";
        public List<TeamsFact> facts { get; set; } = new List<TeamsFact>();
    }

    public class TeamsFact
    {
        public string title { get; set; }
        public string value { get; set; }
    }

    public class TeamsOpenUrlAction
    {
        public string type { get; set; } = "Action.OpenUrl";
        public string title { get; set; }
        public string url { get; set; }
    }
}
