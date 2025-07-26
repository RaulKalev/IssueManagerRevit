namespace IssueManager.Models
{
    public class JiraProject
    {
        public string id { get; set; }
        public string key { get; set; }
        public string name { get; set; }

        public override string ToString() => $"{name} ({key})";
    }
}
