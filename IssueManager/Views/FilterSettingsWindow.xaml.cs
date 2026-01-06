using IssueManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace IssueManager.Views
{
    public partial class FilterSettingsWindow : Window
    {
        public string SelectedAssignee { get; private set; }
        public string SelectedStatus { get; private set; }
        private string currentAssigneeFilter = "All";
        private string currentStatusFilter = "All";
        public List<string> SelectedLabels { get; private set; } = new List<string>();
        public List<string> AllLabels { get; set; } = new List<string>();
        private const string WindowKey = "FilterSettingsWindow";
        public FilterSettingsWindow(List<string> allAssignees, List<string> allStatuses, List<string> allLabels, string currentAssignee, string currentStatus, List<string> selectedLabels)
        {
            InitializeComponent();
            ApplyTheme();
            // ✅ Restore position
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

            this.DataContext = this;

            // Lists are passed pre-calculated from ViewModel
            AllLabels = allLabels ?? new List<string>();


            LabelCheckComboBox.ItemsSource = AllLabels;

            foreach (var label in selectedLabels ?? new List<string>())
            {
                if (AllLabels.Contains(label))
                    LabelCheckComboBox.SelectedItems.Add(label);
            }


            var assigneeList = new List<string> { "All" };
            assigneeList.AddRange(allAssignees);

            var statusList = new List<string> { "All" };
            statusList.AddRange(allStatuses);

            // Bind lists
            AssigneeFilterComboBox.ItemsSource = assigneeList;
            StatusFilterComboBox.ItemsSource = statusList;

            // Select previously selected filter or default to "All"
            AssigneeFilterComboBox.SelectedItem = string.IsNullOrEmpty(currentAssignee) ? "All" : currentAssignee;
            StatusFilterComboBox.SelectedItem = string.IsNullOrEmpty(currentStatus) ? "All" : currentStatus;
            // ✅ Save position on close
            this.Closing += (s, e) =>
            {
                WindowPositionManager.Save(WindowKey, this.Left, this.Top);
            };
        }

        private void ApplyTheme()
        {
            try
            {
                var mergedDicts = new List<ResourceDictionary>();

                // 1. Restore Material Design
                mergedDicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.ComboBox.xaml") });
                mergedDicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml") });

                // 2. Add Custom Theme
                string themeUri = ThemeManager.IsDarkMode
                    ? "pack://application:,,,/IssueManager;component/Views/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/IssueManager;component/Views/Themes/LightTheme.xaml";

                mergedDicts.Add(new ResourceDictionary { Source = new Uri(themeUri, UriKind.Absolute) });

                // 3. Apply
                Resources.MergedDictionaries.Clear();
                foreach (var d in mergedDicts)
                {
                    Resources.MergedDictionaries.Add(d);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load theme in FilterSettingsWindow: {ex.Message}");
            }
        }
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SelectedAssignee = AssigneeFilterComboBox.SelectedItem?.ToString();
            SelectedStatus = StatusFilterComboBox.SelectedItem?.ToString();
            SelectedLabels = LabelCheckComboBox.SelectedItems.Cast<string>().ToList();

            // SelectedLabels already bound – nothing to do here
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
