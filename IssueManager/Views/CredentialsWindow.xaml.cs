using IssueManager.Services;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace IssueManager.Views
{
    public partial class CredentialsWindow : Window
    {
        private readonly string configPath;

        public string Email => EmailTextBox.Text.Trim();
        public string ApiKey => TokenBox.Password.Trim();

        public CredentialsWindow(string configPath)
        {
            InitializeComponent();
            ApplyTheme();
            this.configPath = configPath;
        }

        private void OpenApiSite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://id.atlassian.com/manage-profile/security/api-tokens",
                UseShellExecute = true
            });
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var config = new JiraService.JiraConfig
            {
                email = EmailTextBox.Text.Trim(),
                apiToken = TokenBox.Password.Trim(),
                baseUrl = "https://eule.atlassian.net"
            };

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
