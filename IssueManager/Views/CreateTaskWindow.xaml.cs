using Autodesk.Revit.UI;
using IssueManager.ExternalEvents;
using IssueManager.Models;
using IssueManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace IssueManager.Views
{
    public partial class CreateTaskWindow : Window
    {
        private readonly JiraService _jiraService;
        private readonly List<string> _projects;
        private readonly List<string> _assignees;
        private readonly List<string> _priorities;
        private const string WindowKey = "CreateTaskWindow";
        private static CreateTaskWindow _openInstance;
        public static bool IsOpen => _openInstance != null;
        private readonly CaptureViewImageHandler captureViewImageHandler;
        private string _sectionBoxMetadata = null;
        public ObservableCollection<string> AvailableLabels { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SelectedLabels { get; } = new ObservableCollection<string>();


        public static void ShowOrActivate(
            JiraService jiraService,
            string selectedProject,
            List<string> availableProjects,
            List<string> projectAssignees,
            List<string> priorities,
            List<JiraIssue> allIssues,
            Action<JiraIssue> onTaskCreated)
        {
            if (_openInstance != null)
            {
                _openInstance.Activate();
                return;
            }

            var window = new CreateTaskWindow(jiraService, selectedProject, availableProjects, projectAssignees, priorities, allIssues);
            window.TaskCreated += onTaskCreated;
            window.Show();
        }
        private CreateTaskWindow(
            JiraService jiraService,
            string selectedProject,
            List<string> availableProjects,
            List<string> projectAssignees,
            List<string> priorities,
            List<JiraIssue> allIssues)

        {
            _openInstance = this;
            InitializeComponent();
            ApplyTheme();

            var pos = WindowPositionManager.Load(WindowKey);
            if (pos.Left != 0 || pos.Top != 0)
            {
                this.Left = pos.Left;
                this.Top = pos.Top;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            captureViewImageHandler = ServiceRegistry.CaptureViewImageHandler;
            if (captureViewImageHandler != null)
            {
                captureViewImageHandler.OnImageCaptured = async (imagePath) =>
                {
                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(imagePath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();

                            CapturedImagePreview.Source = bitmap;
                            CapturedImagePreview.Visibility = Visibility.Visible;

                            // ✅ ADD THIS LINE:
                            _sectionBoxMetadata = captureViewImageHandler.SectionBoxMetadata;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to load captured image: {ex.Message}");
                        }
                    }
                };

            }
            // Load all labels from existing issues
            var uniqueLabels = allIssues.SelectMany(i => i.Labels).Distinct().OrderBy(x => x).ToList();

            foreach (var label in uniqueLabels)
                AvailableLabels.Add(label);

            // Set DataContext for binding
            this.DataContext = this;


            _jiraService = jiraService;
            _projects = availableProjects;
            _assignees = projectAssignees;
            _priorities = priorities;

            ProjectComboBox.ItemsSource = _projects;
            ProjectComboBox.SelectedItem = selectedProject;

            AssigneeComboBox.ItemsSource = new[] { "Unassigned" }.Concat(_assignees);
            AssigneeComboBox.SelectedIndex = 0;

            PriorityComboBox.ItemsSource = _priorities;
            PriorityComboBox.SelectedItem = "Medium";

            this.Closing += (s, e) =>
            {
                WindowPositionManager.Save(WindowKey, this.Left, this.Top);
                _openInstance = null;
            };
        }
        public event Action<JiraIssue> TaskCreated;

        public JiraIssue CreatedIssue { get; private set; }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            string projectKey = ProjectComboBox.SelectedItem?.ToString();
            string taskName = TaskNameTextBox.Text.Trim();
            string taskDescription = TaskDescriptionTextBox.Text.Trim();

            // ✅ Embed section box metadata (in hidden HTML comment)
            if (!string.IsNullOrEmpty(_sectionBoxMetadata))
            {
                taskDescription += $"\n\n<!-- {_sectionBoxMetadata} -->";
            }
            string assignee = AssigneeComboBox.SelectedItem?.ToString();
            assignee = (assignee == "Unassigned") ? null : assignee;
            string priority = SelectedPriority;

            if (string.IsNullOrWhiteSpace(taskName))
            {
                MessageBox.Show("Task name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var labels = SelectedLabels.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            // Add "Ristumine" if section box & image are provided
            if (!string.IsNullOrWhiteSpace(_sectionBoxMetadata) && File.Exists(captureViewImageHandler?.ResultImagePath))
            {
                if (!labels.Contains("Ristumine"))
                    labels.Add("Ristumine");
            }

            var issueKey = await _jiraService.CreateIssueAsync(
                projectKey,
                taskName,
                taskDescription,
                assignee,
                priority,
                labels,
                _sectionBoxMetadata,
                captureViewImageHandler?.ResultImagePath);

            if (!string.IsNullOrEmpty(issueKey))
            {
                // ✅ Upload and clean up image
                if (File.Exists(captureViewImageHandler?.ResultImagePath))
                {
                    var uploaded = await _jiraService.AttachFileToIssueAsync(issueKey, captureViewImageHandler.ResultImagePath);

                    if (uploaded)
                    {
                        try
                        {
                            File.Delete(captureViewImageHandler.ResultImagePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to delete temp image: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }

                CreatedIssue = await _jiraService.GetIssueByKeyAsync(issueKey);
                TaskCreated?.Invoke(CreatedIssue);
                Close();
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        public string SelectedPriority => PriorityComboBox.SelectedItem as string ?? "Medium";


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
        private void Ristumine_Click(object sender, RoutedEventArgs e)
        {
            ServiceRegistry.CaptureImageEvent?.Raise();
        }


    }
}
