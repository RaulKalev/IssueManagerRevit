using System;
using System.Collections.Generic;
using IssueManager.Models;
using IssueManager.Views;
using System.Windows;
using System.Linq;

namespace IssueManager.Services
{
    public class DialogService : IDialogService
    {
        public void ShowFilterSettings(List<string> assignees, List<string> statuses, List<string> labels, string currentAssignee, string currentStatus, List<string> selectedLabels, Action<string, string, List<string>> onApply)
        {
             var window = new FilterSettingsWindow(assignees, statuses, labels, currentAssignee, currentStatus, selectedLabels);
             bool? result = window.ShowDialog();
             if (result == true)
             {
                 onApply?.Invoke(window.SelectedAssignee, window.SelectedStatus, window.SelectedLabels);
             }
        }

        public void ShowCreateTask(JiraService jiraService, string currentProjectKey, List<string> projectKeys, List<string> assignees, List<string> priorities, List<JiraIssue> existingIssues, Action<JiraIssue> onCreated)
        {
            CreateTaskWindow.ShowOrActivate(
                jiraService,
                currentProjectKey,
                projectKeys,
                assignees,
                priorities,
                existingIssues,
                onCreated
            );
        }

        public void ShowCredentials(string configPath, Action onSaved)
        {
            var window = new CredentialsWindow(configPath);
            if (window.ShowDialog() == true)
            {
                onSaved?.Invoke();
            }
        }

        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title);
        }
    }
}
