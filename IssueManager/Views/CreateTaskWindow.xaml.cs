using Autodesk.Revit.UI;
using IssueManager.ExternalEvents;
using IssueManager.Models;
using IssueManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private ObservableCollection<string> _imagePaths = new ObservableCollection<string>();
        public ObservableCollection<string> AdditionalImages => _imagePaths;

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
            this.Loaded += async (s, e) => await LoadAssignableUsersAsync(selectedProject);
            this.AllowDrop = true;
            this.Drop += CreateTaskWindow_Drop;

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

                            _sectionBoxMetadata = captureViewImageHandler.SectionBoxMetadata;

                            if (!_imagePaths.Contains(imagePath))
                                _imagePaths.Insert(0, imagePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to load captured image: {ex.Message}");
                        }
                    }
                };

            }
            // Load all labels from existing issues
            // Load all labels from existing issues
            var uniqueLabels = allIssues.SelectMany(i => i.Labels).Distinct().OrderBy(x => x).ToList();

            // Add predefined labels
            var predefinedLabels = new List<string> { "Ristumine", "EL", "EN", "EA/EAT" };

            // Merge predefined with existing, keeping only unique and sorted
            var combinedLabels = predefinedLabels
                .Concat(uniqueLabels)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (var label in combinedLabels)
                AvailableLabels.Add(label);

            // Set DataContext for binding
            this.DataContext = this;


            _jiraService = jiraService;
            _projects = availableProjects;
            _assignees = projectAssignees;
            _priorities = priorities;

            ProjectComboBox.ItemsSource = _projects;
            ProjectComboBox.SelectedItem = selectedProject;


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
            string assignee = null;
            if (AssigneeComboBox.SelectedItem is JiraUser user)
                assignee = user.AccountId;

            string priority = SelectedPriority;

            if (string.IsNullOrWhiteSpace(taskName))
            {
                MessageBox.Show("Task name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var labels = SelectedLabels?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? new List<string>();

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
                // ✅ Upload all images in _imagePaths (including the Revit one if present)
                foreach (var imagePath in _imagePaths)
                {
                    if (File.Exists(imagePath))
                    {
                        var uploaded = await _jiraService.AttachFileToIssueAsync(issueKey, imagePath);

                        // Optionally clean up temp files (pasted/auto-generated), but not user files!
                        try
                        {
                            // Only delete if file is in temp folder (be careful with user-added files)
                            if (imagePath.StartsWith(Path.GetTempPath(), StringComparison.InvariantCultureIgnoreCase) && File.Exists(imagePath))
                            {
                                try { File.Delete(imagePath); } catch { }
                            }

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to delete temp image: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }


                CreatedIssue = await _jiraService.GetIssueByKeyAsync(issueKey);
                if (CreatedIssue != null)
                {
                    CreatedIssue.ImageWithIssues = new ObservableCollection<ImageWithIssue>();

                    if (CreatedIssue.ImageBitmaps != null)
                    {
                        foreach (var img in CreatedIssue.ImageBitmaps)
                        {
                            CreatedIssue.ImageWithIssues.Add(new ImageWithIssue
                            {
                                Image = img,
                                Issue = CreatedIssue
                            });
                        }
                    }
                }


                TaskCreated?.Invoke(CreatedIssue);
                Close();
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAssignableUsersAsync(string selectedProject)
        {
            var assignableUsers = await _jiraService.GetProjectUsersAsync(selectedProject);
            assignableUsers.Insert(0, new JiraUser { DisplayName = "Unassigned", AccountId = null });
            AssigneeComboBox.ItemsSource = assignableUsers;
            AssigneeComboBox.DisplayMemberPath = "DisplayName";
            AssigneeComboBox.SelectedValuePath = "AccountId";
            AssigneeComboBox.SelectedIndex = 0;
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
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    var encoder = new PngBitmapEncoder();
                    var tempFile = Path.Combine(Path.GetTempPath(), $"Ristumine_{Guid.NewGuid()}.png");
                    using (var stream = new FileStream(tempFile, FileMode.Create))
                    {
                        encoder.Frames.Add(BitmapFrame.Create(img));
                        encoder.Save(stream);
                    }
                    _imagePaths.Add(tempFile);
                    // Optionally: Update UI binding for AdditionalImages
                }
            }
            base.OnPreviewKeyDown(e);
        }
        private void CreateTaskWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        if (!_imagePaths.Contains(file))
                            _imagePaths.Add(file);
                    }
                }
            }
        }
        private void PasteImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                var encoder = new PngBitmapEncoder();
                var tempFile = Path.Combine(Path.GetTempPath(), $"Ristumine_{Guid.NewGuid()}.png");
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(stream);
                }
                _imagePaths.Add(tempFile);
            }
            else
            {
                MessageBox.Show("No image found in clipboard.", "Paste Error");
            }
        }
        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var imagePath = button?.Tag as string;
            if (!string.IsNullOrEmpty(imagePath) && AdditionalImages.Contains(imagePath))
            {
                AdditionalImages.Remove(imagePath);
            }
        }


    }
}
