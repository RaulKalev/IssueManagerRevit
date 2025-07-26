using IssueManager.Services;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
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
    }
}
