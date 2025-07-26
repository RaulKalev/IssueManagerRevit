// Keep using directives at the top
using IssueManager.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using NewtonsoftJson = Newtonsoft.Json;
using SystemTextJson = System.Text.Json;

namespace IssueManager.Services
{
    public class JiraService
    {
        private readonly string baseUrl;
        private readonly string email;
        private readonly string apiToken;
        private readonly string _baseUrl;
        private readonly HttpClient _client;

        public JiraService(string baseUrl, string email, string apiToken)
        {
            this.baseUrl = baseUrl;
            this.email = email;
            this.apiToken = apiToken;

            _baseUrl = baseUrl.TrimEnd('/');
            _client = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            _client.BaseAddress = new Uri(_baseUrl);
        }


        public async Task<bool> TestConnectionAsync()
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                var response = await client.GetAsync("/rest/api/3/myself");
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<List<JiraProject>> GetProjectsAsync()
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                var response = await client.GetAsync("/rest/api/3/project/search");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var projects = new List<JiraProject>();

                using (var doc = JsonDocument.Parse(json))
                {
                    foreach (var item in doc.RootElement.GetProperty("values").EnumerateArray())
                    {
                        projects.Add(new JiraProject
                        {
                            id = item.GetProperty("id").GetString(),
                            key = item.GetProperty("key").GetString(),
                            name = item.GetProperty("name").GetString()
                        });
                    }
                }

                return projects;
            }
        }

        public static JiraService LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            var config = SystemTextJson.JsonSerializer.Deserialize<JiraConfig>(json);
            return new JiraService(config.baseUrl, config.email, config.apiToken);
        }
        private async Task<BitmapImage> DownloadImageAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                var data = await client.GetByteArrayAsync(url);

                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(data))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                return bitmap;
            }
        }
        public async Task<List<JiraIssue>> GetProjectIssuesAsync(string projectKey)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                string jql = $"project={projectKey}";
                var response = await client.GetAsync($"/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&fields=summary,description,assignee,reporter,status,attachment,labels");

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var issues = new List<JiraIssue>();

                using (var doc = JsonDocument.Parse(json))
                {
                    foreach (var issue in doc.RootElement.GetProperty("issues").EnumerateArray())
                    {
                        var fields = issue.GetProperty("fields");

                        // Load image attachments
                        List<string> imageUrls = new List<string>();
                        List<BitmapImage> imageBitmaps = new List<BitmapImage>();
                        if (fields.TryGetProperty("attachment", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var att in attachments.EnumerateArray())
                            {
                                if (att.TryGetProperty("mimeType", out var mimeTypeElem) &&
                                    mimeTypeElem.GetString().StartsWith("image/") &&
                                    att.TryGetProperty("content", out var contentElem))
                                {
                                    imageUrls.Add(contentElem.GetString());
                                }
                            }
                        }

                        foreach (var url in imageUrls)
                        {
                            try
                            {
                                var bitmap = await DownloadImageAsync(url);
                                imageBitmaps.Add(bitmap);
                            }
                            catch { }
                        }

                        // Extract assignee name and accountId
                        string assigneeName = null;
                        string assigneeAccountId = null;

                        if (fields.TryGetProperty("assignee", out var assigneeElem) && assigneeElem.ValueKind == JsonValueKind.Object)
                        {
                            if (assigneeElem.TryGetProperty("displayName", out var nameProp))
                                assigneeName = nameProp.GetString();
                            if (assigneeElem.TryGetProperty("accountId", out var idProp))
                                assigneeAccountId = idProp.GetString();
                        }

                        // Extract reporter
                        string reporter = fields.TryGetProperty("reporter", out var reporterElem) && reporterElem.ValueKind == JsonValueKind.Object
                            ? reporterElem.TryGetProperty("displayName", out var reporterName) ? reporterName.GetString() : null
                            : null;

                        // Extract description (plain text from ADF)
                        string descriptionText = "";
                        if (fields.TryGetProperty("description", out var descElem) && descElem.ValueKind == JsonValueKind.Object)
                        {
                            if (descElem.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var block in blocks.EnumerateArray())
                                {
                                    if (block.TryGetProperty("content", out var inner) && inner.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var node in inner.EnumerateArray())
                                        {
                                            if (node.TryGetProperty("text", out var textVal))
                                                descriptionText += textVal.GetString() + "\n";
                                        }
                                    }
                                }
                            }
                        }

                        string statusCategory = fields.TryGetProperty("status", out var statusElem) &&
                                                statusElem.TryGetProperty("statusCategory", out var categoryElem) &&
                                                categoryElem.TryGetProperty("name", out var nameElem)
                            ? nameElem.GetString()
                            : null;

                        List<string> labels = new List<string>();
                        if (fields.TryGetProperty("labels", out var labelsElem) && labelsElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var label in labelsElem.EnumerateArray())
                            {
                                if (label.ValueKind == JsonValueKind.String)
                                    labels.Add(label.GetString());
                            }
                        }


                        issues.Add(new JiraIssue
                        {
                            Key = issue.GetProperty("key").GetString(),
                            Summary = fields.GetProperty("summary").GetString(),
                            Description = descriptionText.Trim(),
                            Assignee = assigneeName,
                            AssigneeAccountId = assigneeAccountId,
                            Reporter = reporter,
                            StatusCategory = statusCategory,
                            ImageUrls = imageUrls,
                            ImageBitmaps = imageBitmaps,
                            Labels = labels // ✅ Add this line
                        });

                    }
                }

                return issues;
            }
        }

        public async Task<bool> UpdateIssueAsync(JiraIssue issue)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                // ---------- 1. Prepare issue fields (description, assignee) ----------
                var fields = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(issue.Description))
                {
                    fields["description"] = new
                    {
                        type = "doc",
                        version = 1,
                        content = new object[]
                        {
                    new {
                        type = "paragraph",
                        content = new object[]
                        {
                            new { type = "text", text = issue.Description }
                        }
                    }
                        }
                    };
                }

                if (!string.IsNullOrEmpty(issue.AssigneeAccountId))
                {
                    fields["assignee"] = new { accountId = issue.AssigneeAccountId };
                }

                var patch = new { fields };
                var json = SystemTextJson.JsonSerializer.Serialize(patch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var updateResponse = await client.PutAsync($"/rest/api/3/issue/{issue.Key}", content);
                if (!updateResponse.IsSuccessStatusCode)
                    return false;

                // ---------- 2. Handle status update (via transition) ----------
                if (!string.IsNullOrEmpty(issue.StatusCategory))
                {
                    // Get available transitions
                    var transitionsResp = await client.GetAsync($"/rest/api/3/issue/{issue.Key}/transitions");
                    if (!transitionsResp.IsSuccessStatusCode)
                        return false;

                    var transitionsJson = await transitionsResp.Content.ReadAsStringAsync();
                    string transitionId = null;

                    using (var doc = JsonDocument.Parse(transitionsJson))
                    {
                        foreach (var t in doc.RootElement.GetProperty("transitions").EnumerateArray())
                        {
                            if (t.GetProperty("name").GetString() == issue.StatusCategory)
                            {
                                transitionId = t.GetProperty("id").GetString();
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(transitionId))
                    {
                        var transitionPayload = new
                        {
                            transition = new { id = transitionId }
                        };

                        var transitionContent = new StringContent(SystemTextJson.JsonSerializer.Serialize(transitionPayload), Encoding.UTF8, "application/json");
                        var transitionResp = await client.PostAsync($"/rest/api/3/issue/{issue.Key}/transitions", transitionContent);

                        if (!transitionResp.IsSuccessStatusCode)
                            return false;
                    }
                }

                return true;
            }
        }

        public async Task<List<JiraUser>> GetProjectUsersAsync(string projectKey)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                var response = await client.GetAsync($"/rest/api/3/user/assignable/search?project={projectKey}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var users = new List<JiraUser>();

                using (var doc = JsonDocument.Parse(json))
                {
                    foreach (var user in doc.RootElement.EnumerateArray())
                    {
                        string displayName = user.GetProperty("displayName").GetString();
                        string accountId = user.GetProperty("accountId").GetString();

                        users.Add(new JiraUser
                        {
                            DisplayName = displayName,
                            AccountId = accountId
                        });
                    }
                }

                return users;
            }
        }
        public async Task<List<string>> GetIssueStatusesAsync(string issueKey)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                var response = await client.GetAsync($"/rest/api/3/issue/{issueKey}/transitions");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var statuses = new List<string>();

                using (var doc = JsonDocument.Parse(json))
                {
                    foreach (var t in doc.RootElement.GetProperty("transitions").EnumerateArray())
                    {
                        if (t.TryGetProperty("name", out var nameElem))
                            statuses.Add(nameElem.GetString());
                    }
                }

                return statuses;
            }
        }
        public async Task<bool> TransitionIssueToDoneAsync(string issueKey)
        {
            try
            {
                var response = await _client.GetAsync($"/rest/api/3/issue/{issueKey}/transitions");
                if (!response.IsSuccessStatusCode) return false;

                var json = await response.Content.ReadAsStringAsync();

                string doneTransitionId = null;

                using (var doc = JsonDocument.Parse(json))
                {
                    var transitions = doc.RootElement.GetProperty("transitions");

                    foreach (var transition in transitions.EnumerateArray())
                    {
                        var name = transition.GetProperty("name").GetString();
                        if (name == "Done")
                        {
                            doneTransitionId = transition.GetProperty("id").GetString();
                            break;
                        }
                    }
                }

                if (doneTransitionId == null)
                    return false;

                var transitionPayload = new
                {
                    transition = new { id = doneTransitionId }
                };

                var content = new StringContent(
                    NewtonsoftJson.JsonConvert.SerializeObject(transitionPayload),
                    Encoding.UTF8,
                    "application/json");

                var transitionResponse = await _client.PostAsync($"/rest/api/3/issue/{issueKey}/transitions", content);
                return transitionResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


        public async Task<bool> CreateIssueAsync(string projectKey, string summary, string description, string assignee)
        {
            var payload = new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = summary,
                    description = description,
                    issuetype = new { name = "Task" },
                    assignee = assignee == null ? null : new { name = assignee }
                }
            };

            string json = NewtonsoftJson.JsonConvert.SerializeObject(payload, NewtonsoftJson.Formatting.None,
                new NewtonsoftJson.JsonSerializerSettings { NullValueHandling = NewtonsoftJson.NullValueHandling.Ignore });


            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"{_baseUrl}/rest/api/2/issue", content);

            return response.IsSuccessStatusCode;
        }

        public class JiraConfig
        {
            public string baseUrl { get; set; }
            public string email { get; set; }
            public string apiToken { get; set; }
        }

    }
}
