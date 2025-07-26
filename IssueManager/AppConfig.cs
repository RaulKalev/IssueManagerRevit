using System.Collections.Generic;

namespace IssueManager.Models
{
    public class AppConfig
    {
        public bool IsDarkMode { get; set; } = true;
        public string SelectedProjectKey { get; set; } = null;
        public string AssigneeFilter { get; set; } = "All";
        public string StatusFilter { get; set; } = "All";
        public List<string> LabelFilters { get; set; } = new List<string>();

    }
}
