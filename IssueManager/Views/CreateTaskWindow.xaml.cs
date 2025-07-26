using IssueManager.Models;
using IssueManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace IssueManager.Views
{
    public partial class CreateTaskWindow : Window
    {
        private readonly JiraService _jiraService;
        private readonly List<string> _projects;
        private readonly List<string> _assignees;

        public CreateTaskWindow(JiraService jiraService, string selectedProject, List<string> availableProjects, List<string> projectAssignees)
        {
            InitializeComponent();
            ApplyTheme();
            _jiraService = jiraService;
            _projects = availableProjects;
            _assignees = projectAssignees;

            ProjectComboBox.ItemsSource = _projects;
            ProjectComboBox.SelectedItem = selectedProject;

            AssigneeComboBox.ItemsSource = new[] { "Unassigned" }.Concat(_assignees);
            AssigneeComboBox.SelectedIndex = 0;
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            string projectKey = ProjectComboBox.SelectedItem?.ToString();
            string taskName = TaskNameTextBox.Text.Trim();
            string taskDescription = TaskDescriptionTextBox.Text.Trim();
            string assignee = AssigneeComboBox.SelectedItem?.ToString();
            assignee = (assignee == "Unassigned") ? null : assignee;

            if (string.IsNullOrWhiteSpace(taskName))
            {
                MessageBox.Show("Task name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = await _jiraService.CreateIssueAsync(projectKey, taskName, taskDescription, assignee);
            if (success)
            {
                MessageBox.Show("Task created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void ApplyTheme()
        {
            string themeUri = ThemeManager.IsDarkMode
                ? "pack://application:,,,/IssueManager;component/Views/Themes/DarkTheme.xaml"
                : "pack://application:,,,/IssueManager;component/Views/Themes/LightTheme.xaml";

            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Absolute)
                };

                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load theme in CreateTaskWindow: {ex.Message}");
            }
        }
    }
}
