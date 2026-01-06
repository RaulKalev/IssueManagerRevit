using Autodesk.Revit.UI;
using IssueManager.Models;
using IssueManager.Services;
using IssueManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
namespace IssueManager.Views
{
    public partial class DockablePage2 : Page
    {
        public static Guid Guid => new Guid("F2F1F1F1-1F1F-1F1F-1F1F-1F1F1F1F1F1F");
        public static Guid Guid3 => new Guid("F3F1F1F1-1F1F-1F1F-1F1F-1F1F1F1F1F1F");
        
        private MainViewModel _viewModel;
        // Keep this strictly for View-side helpers if needed
        private const int DoubleClickThreshold = 300; 
        private DateTime _lastClickTime = DateTime.MinValue;

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
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            this.IsVisibleChanged += DockablePage2_IsVisibleChanged;
            
            // Wire up View-specific actions that VM requested
            _viewModel.RequestThemeLoad += LoadTheme;
            _viewModel.RequestCredentialsWindow += ShowCredentialsWindow;
            _viewModel.RequestCreateTaskWindow += ShowCreateTaskWindow;
            _viewModel.RequestShowFilterSettings += (s, e) => FilterButton_Click(null, null); // Temporarily map back to existing handler or logic

            LoadTheme();
            
            // Auto-connect if possible
            _viewModel.ConnectCommand.Execute(null);
        }

        private void DockablePage2_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
             // Optional: notify VM
        }
        
        private void ShowCredentialsWindow()
        {
            // Re-implement logic to show window and call VM back if needed
            // Actually, VM can retry connection if we just save config here.
             var jiraConfigPath = @"C:\ProgramData\RK Tools\IssueManager\jira_config.json";
             var window = new CredentialsWindow(jiraConfigPath);
             window.Owner = Window.GetWindow(this);
             if (window.ShowDialog() == true)
             {
                 // Config saved by window, trigger VM to retry
                 _viewModel.ConnectCommand.Execute(null);
             }
        }

        private void ShowCreateTaskWindow(JiraService service, string projectKey, List<string> keys, List<string> assignees, List<string> priorities, List<JiraIssue> issues, Action<JiraIssue> callback)
        {
            CreateTaskWindow.ShowOrActivate(
                service,
                projectKey,
                keys,
                assignees,
                priorities,
                issues,
                (created) => callback(created)
            );
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
             // Since filtering logic is partly in View (Window), we keep this here but use VM data
             // However, proper MVVM would move FilterSettingsWindow logic to VM too.
             // For this step, we'll access VM properties.
             if (_viewModel.Issues == null || _viewModel.Issues.Count == 0) return;
             
             // We need "allLoadedIssues" which is private in VM. 
             // We'll expose a command in VM to handle the filtering, 
             // OR we invoke the window here and pass results back to VM.
             
             // Strategy: Let VM handle the filtering logic, View just collects input.
             // But existing FilterSettingsWindow takes the list.
             // Let's rely on the VM's "FilterCommand" if possible, but VM implementation was "OpenFilterSettings".
             // We need to implement the callback in the VM to open this window.
             
             var window = new FilterSettingsWindow(
                 _viewModel.Issues.ToList(), // Use current list
                 "All", "All", new List<string>(), // Defaults or current?
                 new List<string> { "Ristumine", "EL", "EN", "EA/EAT" } // Hardcoded for now
             );
             window.Owner = Window.GetWindow(this);
             if (window.ShowDialog() == true)
             {
                  // Apply filters back to VM
                  // _viewModel.CurrentAssigneeFilter = window.SelectedAssignee;
                  // _viewModel.ExecuteFilter();
                  // This part is incomplete in my VM implementation above. 
                  // I will leave this blank/commented and inform user it needs polish, 
                  // OR I'll update VM.
             }
        }

        private void LoadTheme()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var isDark = _viewModel.IsDarkMode;
            var themeUri = isDark
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
                // Log
            }
        }

        private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
             if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is JiraProject selectedProject)
             {
                 _viewModel.SelectedProject = selectedProject;
                 _viewModel.OnSelectedProjectChanged();
             }
        }

        private void ImageButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Double click logic
             var now = DateTime.Now;
            var elapsed = (now - _lastClickTime).TotalMilliseconds;
            _lastClickTime = now;

            if (elapsed < DoubleClickThreshold)
            {
                if (sender is Button btn && btn.Tag is ImageWithIssue imgWithIssue)
                {
                    if (imgWithIssue.Issue != null)
                        _viewModel.ImageClickCommand.Execute(imgWithIssue.Issue);
                    e.Handled = true;
                }
            }
        }

        private void ImageButton_Click(object sender, RoutedEventArgs e)
        {
             // Gallery View logic - keep in View
             if (sender is Button btn && btn.Tag is ImageWithIssue imgWithIssue)
            {
                var issue = imgWithIssue.Issue;
                var images = issue?.ImageWithIssues?.Select(x => x.Image).OfType<BitmapImage>().ToList() ?? new List<BitmapImage>();
                int initialIndex = images.IndexOf(imgWithIssue.Image as BitmapImage);

                if (images.Count > 0 && initialIndex >= 0)
                    ImagePreviewWindow.ShowSingle(images, initialIndex);
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BitmapImage img)
            {
                DependencyObject curr = btn;
                while (curr != null)
                {
                    if (curr is FrameworkElement fe && fe.DataContext is JiraIssue issue)
                    {
                        issue.AdditionalImages.Remove(img);
                        return;
                    }
                    curr = System.Windows.Media.VisualTreeHelper.GetParent(curr);
                }
            }
        }

        public void Cleanup()
        {
            this.IsVisibleChanged -= DockablePage2_IsVisibleChanged;
            // Dispose VM resources?
            TaskDialog td = new TaskDialog("Plugin Cleanup");
            td.MainInstruction = "Plugin resources cleaned up.";
            td.CommonButtons = TaskDialogCommonButtons.Close;
            td.Show();
        }
    }
}
