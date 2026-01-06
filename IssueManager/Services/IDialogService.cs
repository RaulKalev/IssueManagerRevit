using System;
using System.Collections.Generic;
using IssueManager.Models;
using IssueManager.Services;

namespace IssueManager.Services
{
    public interface IDialogService
    {
        void ShowFilterSettings(List<string> assignees, List<string> statuses, List<string> labels, string currentAssignee, string currentStatus, List<string> selectedLabels, Action<string, string, List<string>> onApply);
        void ShowCreateTask(JiraService jiraService, string currentProjectKey, List<string> projectKeys, List<string> assignees, List<string> priorities, List<JiraIssue> existingIssues, Action<JiraIssue> onCreated);
        void ShowCredentials(string configPath, Action onSaved);
        void ShowMessage(string message, string title);
    }
}
