using Autodesk.Revit.UI;
using IssueManager.Models;
using IssueManager.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace IssueManager.Views
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class DockablePage2 : Page
    {
        public static Guid Guid => new Guid("F2F1F1F1-1F1F-1F1F-1F1F-1F1F1F1F1F1F");
        public static Guid Guid3 => new Guid("F3F1F1F1-1F1F-1F1F-1F1F-1F1F1F1F1F1F");
        private const string ConfigFilePath = @"C:\ProgramData\RK Tools\IssueManager\config.json";
        private const string JiraConfigPath = @"C:\ProgramData\RK Tools\IssueManager\jira_config.json";
        private JiraService jiraService;
        private List<JiraUser> cachedUsers;
        private bool _isDarkMode = true;
        private List<JiraIssue> allLoadedIssues;
        private List<JiraProject> cachedProjects;
        private Dictionary<string, List<JiraUser>> cachedAssignees = new Dictionary<string, List<JiraUser>>();
        private AppConfig appConfig = new AppConfig();
        private List<string> currentLabelFilters = new List<string>();
        public JiraIssue CreatedIssue { get; private set; }
        public ICommand ImageClickCommand { get; }
        private System.Windows.Threading.DispatcherTimer autoRefreshTimer;
        private readonly List<string> predefinedLabels = new List<string> { "Ristumine", "EL", "EN", "EA/EAT" };
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickThreshold = 300; // ms
        public static async Task ForEachAsync<T>(IEnumerable<T> source, int degreeOfParallelism, Func<T, Task> body)
        {
            var tasks = new List<Task>();
            using (var semaphore = new System.Threading.SemaphoreSlim(degreeOfParallelism))
            {
                foreach (var item in source)
                {
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try { await body(item); }
                        finally { semaphore.Release(); }
                    }));
                }
                await Task.WhenAll(tasks);
            }
        }

        public DockablePage2()
        {
            InitializeComponent();
            this.IsVisibleChanged += DockablePage2_IsVisibleChanged;
            LoadConfig(); // Load theme state before applying theme
            ThemeManager.IsDarkMode = _isDarkMode;
            ImageClickCommand = new RelayCommand<object>(OnImageClicked);
            DataContext = this;
            InitializeAutoRefreshTimer();

            LoadTheme();
        }
        private void InitializeAutoRefreshTimer()
        {
            autoRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            autoRefreshTimer.Interval = TimeSpan.FromMinutes(10);
            autoRefreshTimer.Tick += (s, e) =>
            {
                if (this.IsVisible && ProjectComboBox.SelectedItem is JiraProject)
                {
                    RefreshButton_Click(null, null);
                }
            };
            autoRefreshTimer.Start();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    appConfig = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load config: {ex.Message}", "Config Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Apply loaded settings
            _isDarkMode = appConfig.IsDarkMode;
            ThemeToggleButton.IsChecked = _isDarkMode;

            currentAssigneeFilter = appConfig.AssigneeFilter;
            currentStatusFilter = appConfig.StatusFilter;
            currentLabelFilters = appConfig.LabelFilters ?? new List<string>();

        }
        private void SaveConfig()
        {
            try
            {
                appConfig.IsDarkMode = _isDarkMode;
                appConfig.AssigneeFilter = currentAssigneeFilter;
                appConfig.StatusFilter = currentStatusFilter;
                appConfig.LabelFilters = currentLabelFilters;


                if (ProjectComboBox.SelectedItem is JiraProject selectedProject)
                {
                    appConfig.SelectedProjectKey = selectedProject.key;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));
                var json = JsonConvert.SerializeObject(appConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save config: {ex.Message}", "Config Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ConnectToJira_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show rotating icon
                ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Loading;
                ConnectionStatusIcon.Visibility = Visibility.Visible;
                ConnectionStatusIcon.Foreground = Brushes.Gray;

                var storyboard = (Storyboard)FindResource("RotateIconStoryboard");
                storyboard.Begin(ConnectionStatusIcon, true);

                // --- Load credentials ---
                if (!File.Exists(JiraConfigPath))
                {
                    ShowCredentialsWindow();
                    return;
                }

                var configText = File.ReadAllText(JiraConfigPath);
                var config = JsonConvert.DeserializeObject<JiraService.JiraConfig>(configText);

                if (string.IsNullOrWhiteSpace(config?.email) || string.IsNullOrWhiteSpace(config?.apiToken))
                {
                    ShowCredentialsWindow();
                    return;
                }

                // Try connection
                jiraService = new JiraService(config.baseUrl, config.email, config.apiToken);
                bool connected = await jiraService.TestConnectionAsync();

                if (!connected)
                {
                    storyboard.Stop(ConnectionStatusIcon);
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                    ConnectionStatusIcon.Foreground = Brushes.Red;

                    ShowCredentialsWindow();
                    return;
                }

                // Success
                storyboard.Stop(ConnectionStatusIcon);
                ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                ConnectionStatusIcon.Foreground = Brushes.Green;
                await Task.Delay(5000);
                ConnectionStatusIcon.Visibility = Visibility.Collapsed;

                // Hook Jira update callback
                JiraIssue.UpdateCallback = async (issue) =>
                {
                    try
                    {
                        bool result = await jiraService.UpdateIssueAsync(issue);
                        if (!result)
                            TaskDialog.Show("Update Failed", $"Failed to update issue: {issue.Key}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Update Error", $"Error updating issue: {ex.Message}");
                        return false;
                    }
                };
                JiraIssue.FilterCallback = ApplyCurrentFilters;


                // Load projects
                cachedProjects = await jiraService.GetProjectsAsync();
                if (cachedProjects.Count > 0)
                {
                    ProjectComboBox.ItemsSource = cachedProjects;
                    ProjectComboBox.Visibility = Visibility.Visible;

                    var index = cachedProjects.FindIndex(p => p.key == appConfig.SelectedProjectKey);
                    ProjectComboBox.SelectedIndex = index >= 0 ? index : 0;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                ConnectionStatusIcon.Foreground = Brushes.Red;
            }
        }
        private void ShowCredentialsWindow()
        {
            var window = new CredentialsWindow(JiraConfigPath);
            window.Owner = Window.GetWindow(this);
            if (window.ShowDialog() == true)
            {
                // Save to config file
                var newConfig = new JiraService.JiraConfig
                {
                    baseUrl = "https://eule.atlassian.net", // Optional: you can allow user to enter this too
                    email = window.Email,
                    apiToken = window.ApiKey
                };

                Directory.CreateDirectory(Path.GetDirectoryName(JiraConfigPath));
                File.WriteAllText(JiraConfigPath, JsonConvert.SerializeObject(newConfig, Formatting.Indented));

                // Retry connection after credentials entered
                ConnectToJira_Click(null, null);
            }
        }
        private async void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Storyboard storyboard = (Storyboard)FindResource("RotateIconStoryboard");

            if (ProjectComboBox.SelectedItem is JiraProject selectedProject)
            {
                try
                {
                    // Show rotating icon
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Loading;
                    ConnectionStatusIcon.Visibility = Visibility.Visible;
                    ConnectionStatusIcon.Foreground = Brushes.Gray;

                    storyboard.Begin(ConnectionStatusIcon, true);

                    // Get and cache users
                    cachedUsers = await jiraService.GetProjectUsersAsync(selectedProject.key);

                    // Get issues
                    var issues = await jiraService.GetProjectIssuesAsync(selectedProject.key);

                    // Assign users up front (fast, no need for parallel)
                    foreach (var issue in issues)
                        issue.AllAssignees = cachedUsers;

                    // Fetch statuses, labels, and comments in parallel (limit to 8 threads)
                    await ForEachAsync(issues, 8, async issue =>
                    {
                        issue.AllStatuses = await jiraService.GetIssueStatusesAsync(issue.Key);

                        var dynamicLabels = issue.Labels != null ? issue.Labels.ToList() : new List<string>();
                        issue.AllLabels = new ObservableCollection<string>(
                            predefinedLabels.Concat(dynamicLabels).Distinct().OrderBy(x => x)
                        );

                        // Fetch comments in parallel too
                        var commentList = await jiraService.GetIssueCommentsAsync(issue.Key);
                        issue.Comments = new ObservableCollection<string>(commentList);
                    });

                    allLoadedIssues = issues;
                    ApplyCurrentFilters();

                    // ✅ Stop and change icon to checkmark
                    storyboard.Stop(ConnectionStatusIcon);
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                    ConnectionStatusIcon.Foreground = Brushes.Green;

                    // ✅ Auto-hide after 5 seconds
                    await Task.Delay(5000);
                    ConnectionStatusIcon.Visibility = Visibility.Collapsed;

                    // Save selected project key
                    appConfig.SelectedProjectKey = selectedProject.key;
                }
                catch (Exception ex)
                {
                    storyboard.Stop(ConnectionStatusIcon);
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                    ConnectionStatusIcon.Foreground = Brushes.Red;

                    // Optional: Hide after failure if you want
                    await Task.Delay(5000);
                    ConnectionStatusIcon.Visibility = Visibility.Collapsed;
                }
            }

            SaveConfig();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectComboBox.SelectedItem is JiraProject selectedProject)
            {
                Storyboard storyboard = (Storyboard)FindResource("RotateIconStoryboard");

                try
                {
                    // Show rotating icon
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Loading;
                    ConnectionStatusIcon.Visibility = Visibility.Visible;
                    ConnectionStatusIcon.Foreground = Brushes.Gray;
                    storyboard.Begin(ConnectionStatusIcon, true);

                    var issues = await jiraService.GetProjectIssuesAsync(selectedProject.key);

                    // Set cached users
                    foreach (var issue in issues)
                        issue.AllAssignees = cachedUsers ?? new List<JiraUser>();

                    allLoadedIssues = issues;

                    // Fetch statuses and labels/comments in parallel (throttle to 8 threads)
                    await ForEachAsync(allLoadedIssues, 8, async issue =>
                    {
                        issue.AllStatuses = await jiraService.GetIssueStatusesAsync(issue.Key);

                        var dynamicLabels = issue.Labels != null ? issue.Labels.ToList() : new List<string>();
                        issue.AllLabels = new ObservableCollection<string>(
                            predefinedLabels.Concat(dynamicLabels).Distinct().OrderBy(x => x)
                        );

                        // If you want to fetch comments in parallel too, uncomment this:
                        var comments = await jiraService.GetIssueCommentsAsync(issue.Key);
                        issue.Comments = new ObservableCollection<string>(comments);
                    });

                    ApplyCurrentFilters();

                    // ✅ Stop and change icon to checkmark
                    storyboard.Stop(ConnectionStatusIcon);
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                    ConnectionStatusIcon.Foreground = Brushes.Green;

                    // ✅ Auto-hide after 5 seconds
                    await Task.Delay(5000);
                    ConnectionStatusIcon.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    storyboard.Stop(ConnectionStatusIcon);
                    ConnectionStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                    ConnectionStatusIcon.Foreground = Brushes.Red;

                    await Task.Delay(5000);
                    ConnectionStatusIcon.Visibility = Visibility.Collapsed;

                    MessageBox.Show($"Failed to refresh issues: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DockablePage2_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!this.IsVisible)
            {
                Console.WriteLine("Dockable pane was closed/hidden.");
            }
        }

        public void Cleanup()
        {
            this.IsVisibleChanged -= DockablePage2_IsVisibleChanged;
            jiraService = null;

            autoRefreshTimer?.Stop();

            TaskDialog td = new TaskDialog("Plugin Cleanup");
            td.MainInstruction = "Plugin resources cleaned up.";
            td.MainContent = "All resources have been released and background tasks cancelled.";
            td.CommonButtons = TaskDialogCommonButtons.Close;
            td.Show();
        }


        private void LoadTheme()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var themeUri = _isDarkMode
                ? $"pack://application:,,,/{assemblyName};component/Views/Themes/DarkTheme.xaml"
                : $"pack://application:,,,/{assemblyName};component/Views/Themes/LightTheme.xaml";

            try
            {
                var resourceDict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Absolute)
                };

                this.Resources.MergedDictionaries.Clear();
                this.Resources.MergedDictionaries.Add(resourceDict);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load theme: {ex.Message}\nTheme URI: {themeUri}", "Theme Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = ThemeToggleButton.IsChecked == true;
            ThemeManager.IsDarkMode = _isDarkMode; // <- Sync with global state

            LoadTheme();

            if (ThemeToggleButton.Template.FindName("ThemeToggleIcon", ThemeToggleButton)
                is MaterialDesignThemes.Wpf.PackIcon themeIcon)
            {
                themeIcon.Kind = _isDarkMode
                    ? MaterialDesignThemes.Wpf.PackIconKind.ToggleSwitchOffOutline
                    : MaterialDesignThemes.Wpf.PackIconKind.ToggleSwitchOutline;
            }
            SaveConfig(); // Save the theme state
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (allLoadedIssues == null || allLoadedIssues.Count == 0)
                return;

            var window = new FilterSettingsWindow(
                allLoadedIssues,
                currentAssigneeFilter,
                currentStatusFilter,
                currentLabelFilters,
                predefinedLabels);

            window.Owner = Window.GetWindow(this);

            if (window.ShowDialog() == true)
            {
                currentAssigneeFilter = window.SelectedAssignee;
                currentStatusFilter = window.SelectedStatus;
                currentLabelFilters = window.SelectedLabels ?? new List<string>(); // ✅ Set label filters
                ApplyCurrentFilters();
                SaveConfig(); // ✅ Save filter changes
            }
        }


        private string currentAssigneeFilter = "All";
        private string currentStatusFilter = "All";

        private void ApplyCurrentFilters()
        {
            var filtered = allLoadedIssues.Where(issue =>
                (currentAssigneeFilter == "All" || issue.Assignee == currentAssigneeFilter) &&
                (currentStatusFilter == "All" || issue.StatusCategory == currentStatusFilter) &&
                (currentLabelFilters == null || currentLabelFilters.Count == 0 ||
                 (issue.Labels != null && issue.Labels.Any(label => currentLabelFilters.Contains(label))))
            ).ToList();

            IssueCardsPanel.ItemsSource = filtered
            .OrderBy(issue => issue.PriorityRank)
            .ThenBy(issue => issue.CreatedDate ?? DateTime.MaxValue)
            .ToList();

        }
        private async void CreateTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var priorities = new List<string> { "Highest", "High", "Medium", "Low", "Lowest" };
            var selectedProject = ProjectComboBox.SelectedItem as JiraProject;
            if (selectedProject == null)
            {
                MessageBox.Show("Please select a project first.", "Missing Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cachedProjects == null)
                cachedProjects = await jiraService.GetProjectsAsync();

            if (!cachedAssignees.TryGetValue(selectedProject.key, out var assignees))
            {
                assignees = await jiraService.GetProjectUsersAsync(selectedProject.key);
                cachedAssignees[selectedProject.key] = assignees;
            }

            var assigneeNames = assignees.Select(a => a.DisplayName).ToList();
            var projectKeys = cachedProjects.Select(p => p.key).ToList();

            CreateTaskWindow.ShowOrActivate(
                jiraService,
                selectedProject.key,
                projectKeys,
                assigneeNames,
                priorities,
                allLoadedIssues, // ✅ pass this in as the new parameter
                async created =>
                {
                    created.Priority = created.Priority ?? "Medium";
                    created.AllAssignees = cachedUsers ?? new List<JiraUser>();
                    created.AllStatuses = await jiraService.GetIssueStatusesAsync(created.Key);

                    allLoadedIssues.Add(created);
                    ApplyCurrentFilters();
                });

        }


        private async void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var issue = checkbox?.DataContext as JiraIssue;

            if (issue == null)
                return;

            bool isChecked = checkbox.IsChecked == true;

            // If checkbox was ticked, mark issue as Done
            if (isChecked && issue.StatusCategory != "Done")
            {
                bool success = await jiraService.TransitionIssueToDoneAsync(issue.Key);

                if (success)
                {
                    issue.StatusCategory = "Done"; // Update UI binding
                    ApplyCurrentFilters(); // 🔁 Re-filter list if user wants to hide Done tasks
                }
                else
                {
                    MessageBox.Show($"Failed to mark issue {issue.Key} as Done.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    checkbox.IsChecked = false; // Roll back if failed
                }
            }
        }
        private void OnImageClicked(object parameter)
        {
            if (parameter is JiraIssue issue)
            {

                if (!string.IsNullOrEmpty(issue.Description) && issue.Description.Contains("[SECTION_BOX]"))
                {
                    ServiceRegistry.ApplySectionBoxHandler?.SetTarget(issue.Description);
                    ServiceRegistry.ApplySectionBoxEvent?.Raise();
                }
                else
                {
                    TaskDialog.Show("Debug", "No section box metadata found.");
                }
            }
            else
            {
                TaskDialog.Show("Error", "Invalid issue parameter in OnImageClicked.");
            }
        }
        private void OpenProject_InBrowser(object sender, RoutedEventArgs e)
        {
            if (ProjectComboBox.SelectedItem is JiraProject selectedProject)
            {
                string baseUrl = jiraService?.BaseUrl?.TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(selectedProject.key))
                {
                    string url = $"{baseUrl}/browse/{selectedProject.key}";
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a Jira project first.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void ImageButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastClickTime).TotalMilliseconds;
            _lastClickTime = now;

            if (elapsed < DoubleClickThreshold)
            {
                // DOUBLE CLICK detected
                if (sender is Button btn && btn.Tag is ImageWithIssue imgWithIssue)
                {
                    // Your "ristumine" logic, e.g.:
                    if (imgWithIssue.Issue != null)
                        OnImageClicked(imgWithIssue.Issue);
                    e.Handled = true;
                }
            }
        }

        private void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ImageWithIssue imgWithIssue)
            {
                // Find the full list and index for gallery navigation
                var issue = imgWithIssue.Issue;
                var images = issue?.ImageWithIssues?.Select(x => x.Image).OfType<BitmapImage>().ToList() ?? new List<BitmapImage>();
                int initialIndex = images.IndexOf(imgWithIssue.Image as BitmapImage);

                // Show only if found
                if (images.Count > 0 && initialIndex >= 0)
                    ImagePreviewWindow.ShowSingle(images, initialIndex);
            }
        }
        private void PasteImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is JiraIssue editingIssue)
            {
                if (Clipboard.ContainsImage())
                {
                    var bmpSource = Clipboard.GetImage();
                    if (bmpSource != null)
                    {
                        var bitmap = new BitmapImage();
                        using (var ms = new MemoryStream())
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(bmpSource));
                            encoder.Save(ms);
                            ms.Position = 0;
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                        }
                        editingIssue.AdditionalImages.Add(bitmap);
                    }
                }
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BitmapImage img && btn.DataContext is JiraIssue editingIssue)
            {
                editingIssue.AdditionalImages.Remove(img);
            }
        }


    }
}
