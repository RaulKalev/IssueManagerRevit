namespace IssueManager.Models
{
    public class JiraUser
    {
        public string DisplayName { get; set; }
        public string AccountId { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
