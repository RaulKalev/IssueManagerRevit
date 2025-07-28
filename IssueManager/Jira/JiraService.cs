// Keep using directives at the top
using IssueManager.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        public string BaseUrl => baseUrl;

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
                var response = await client.GetAsync($"/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&fields=summary,description,assignee,reporter,status,attachment,labels,priority");

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

                        string priority = fields.TryGetProperty("priority", out var priorityElem) &&
                             priorityElem.TryGetProperty("name", out var prioName)
                             ? prioName.GetString()
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
                        DateTime? createdDate = fields.TryGetProperty("created", out var createdElem)
                            ? createdElem.GetDateTime()
                            : (DateTime?)null;

                        // ✅ Parse and clone ADF description safely before the JsonDocument is disposed
                        JsonElement descRaw;
                        string originalDescriptionADF = null;
                        JsonElement? originalADFJson = null;

                        if (fields.TryGetProperty("description", out descRaw) && descRaw.ValueKind == JsonValueKind.Object)
                        {
                            originalDescriptionADF = descRaw.ToString();
                            originalADFJson = JsonDocument.Parse(descRaw.GetRawText()).RootElement.Clone();
                        }
                        else
                        {
                            // Use a basic empty paragraph ADF fallback
                            originalDescriptionADF = "{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\"}]}";
                            originalADFJson = JsonDocument.Parse(originalDescriptionADF).RootElement.Clone();
                        }


                        var jiraIssue = new JiraIssue
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
                            Labels = new ObservableCollection<string>(labels),
                            CreatedDate = createdDate,
                            Priority = priority,
                            OriginalDescriptionADF = originalDescriptionADF,
                            OriginalADFJson = originalADFJson
                        };

                        // ✅ Populate ImageWithIssues for image click command
                        jiraIssue.ImageWithIssues = new ObservableCollection<ImageWithIssue>();
                        foreach (var img in imageBitmaps)
                        {
                            jiraIssue.ImageWithIssues.Add(new ImageWithIssue
                            {
                                Image = img,
                                Issue = jiraIssue
                            });
                        }

                        issues.Add(jiraIssue);


                    }
                }
                // ✅ Populate AllLabels for each issue based on all distinct labels across all issues
                var distinctLabels = new HashSet<string>();
                foreach (var issue in issues)
                {
                    foreach (var label in issue.Labels)
                        distinctLabels.Add(label);
                }

                foreach (var issue in issues)
                {
                    issue.AllLabels = new ObservableCollection<string>(distinctLabels);
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

                // ---------- 1. Prepare issue fields (summary, description, assignee, priority, labels) ----------
                var fields = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(issue.Summary))
                {
                    fields["summary"] = issue.Summary;
                }

                // Update description safely even if ADF is missing
                if (!string.IsNullOrWhiteSpace(issue.Description))
                {
                    var fallbackADF = JsonDocument.Parse(
                        "{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"\"}]}]}"
                    ).RootElement;

                    var adf = issue.OriginalADFJson ?? fallbackADF;
                    var updatedADF = UpdateTextInADF(adf, issue.Description);
                    fields["description"] = updatedADF;
                }

                if (!string.IsNullOrEmpty(issue.AssigneeAccountId))
                {
                    fields["assignee"] = new { accountId = issue.AssigneeAccountId };
                }

                if (!string.IsNullOrEmpty(issue.Priority))
                {
                    fields["priority"] = new { name = issue.Priority };
                }

                if (issue.Labels != null)
                {
                    fields["labels"] = issue.Labels;
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
                    var transitionsResp = await client.GetAsync($"/rest/api/3/issue/{issue.Key}/transitions");
                    if (!transitionsResp.IsSuccessStatusCode)
                        return false;

                    var transitionsJson = await transitionsResp.Content.ReadAsStringAsync();
                    string transitionId = null;

                    // ✅ Define mapping between local status and Jira transition name
                    var statusToTransitionName = new Dictionary<string, string>
    {
        { "Done", "Done" },
        { "Ootel", "To Do" },
        { "Tegemisel", "In Progress" }, // <-- update this based on your Jira setup
    };

                    if (statusToTransitionName.TryGetValue(issue.StatusCategory, out var targetTransitionName))
                    {
                        using (var doc = JsonDocument.Parse(transitionsJson))
                        {
                            foreach (var t in doc.RootElement.GetProperty("transitions").EnumerateArray())
                            {
                                if (t.GetProperty("name").GetString() == targetTransitionName)
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
                }


                return true;
            }
        }
        private object UpdateTextInADF(JsonElement originalADF, string newText)
        {
            var updatedContent = new List<object>();
            bool contentReplaced = false;

            foreach (var block in originalADF.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "paragraph" &&
                    block.TryGetProperty("content", out var paragraphContent) &&
                    paragraphContent.ValueKind == JsonValueKind.Array)
                {
                    var replacedParagraph = new
                    {
                        type = "paragraph",
                        content = new object[]
                        {
                    new { type = "text", text = newText }
                        }
                    };
                    updatedContent.Add(replacedParagraph);
                    contentReplaced = true;
                }
                else
                {
                    updatedContent.Add(SystemTextJson.JsonSerializer.Deserialize<object>(block.GetRawText()));
                }
            }

            // If original ADF had no usable content (e.g., was just an empty paragraph), create it manually
            if (!contentReplaced)
            {
                updatedContent.Add(new
                {
                    type = "paragraph",
                    content = new object[]
                    {
                new { type = "text", text = newText }
                    }
                });
            }

            return new
            {
                type = "doc",
                version = 1,
                content = updatedContent
            };
        }

        public async Task<bool> UploadAttachmentAsync(string issueKey, string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");

                using (var form = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                    form.Add(fileContent, "file", Path.GetFileName(filePath));

                    var response = await client.PostAsync($"{baseUrl}/rest/api/3/issue/{issueKey}/attachments", form);
                    return response.IsSuccessStatusCode;
                }
            }
        }
        public async Task<bool> AttachFileToIssueAsync(string issueKey, string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using (var content = new MultipartFormDataContent())
                using (var fileStream = File.OpenRead(filePath))
                {
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(fileContent, "file", Path.GetFileName(filePath));

                    _client.DefaultRequestHeaders.Remove("X-Atlassian-Token"); // Ensure no duplicate
                    _client.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");

                    var response = await _client.PostAsync($"/rest/api/3/issue/{issueKey}/attachments", content);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to attach file to Jira issue: {ex.Message}");
                return false;
            }
        }

        public async Task<List<JiraUser>> GetProjectUsersAsync(string projectKey)
        {
            using (var client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.BaseAddress = new Uri(baseUrl);

                // Fetch current user info
                var myselfResp = await client.GetAsync("/rest/api/3/myself");
                myselfResp.EnsureSuccessStatusCode();
                var myselfJson = await myselfResp.Content.ReadAsStringAsync();
                var myId = JsonDocument.Parse(myselfJson).RootElement.GetProperty("accountId").GetString();

                // Fetch assignable users
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
                            AccountId = accountId,
                            IsCurrentUser = (accountId == myId)
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
        public async Task<string> CreateIssueAsync(
            string projectKey,
            string summary,
            string description,
            string assignee,
            string priority,
            List<string> labels = null,
            string sectionBoxMetadata = null,
            string imagePath = null)

        {
            var fields = new Dictionary<string, object>
    {
        { "project", new { key = projectKey } },
        { "summary", summary },
        { "description", description },
        { "issuetype", new { name = "Task" } }
    };

            if (!string.IsNullOrEmpty(assignee))
                fields["assignee"] = new { name = assignee };

            if (!string.IsNullOrEmpty(priority))
                fields["priority"] = new { name = priority };

            if (labels != null && labels.Any())
            {
                fields["labels"] = labels.Distinct().ToList();
            }


            var payload = new { fields };

            string json = NewtonsoftJson.JsonConvert.SerializeObject(
                payload,
                NewtonsoftJson.Formatting.None,
                new NewtonsoftJson.JsonSerializerSettings { NullValueHandling = NewtonsoftJson.NullValueHandling.Ignore });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"{_baseUrl}/rest/api/2/issue", content);

            if (!response.IsSuccessStatusCode)
                return null;

            var responseBody = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(responseBody))
            {
                if (doc.RootElement.TryGetProperty("key", out var keyElem))
                    return keyElem.GetString();
            }

            return null;
        }
        public class JiraComment
        {
            public Body body { get; set; }
        }

        public class Body
        {
            public List<Content> content { get; set; }
        }

        public class Content
        {
            public List<ContentText> content { get; set; }
        }

        public class ContentText
        {
            public string text { get; set; }
        }

        public class JiraCommentResponse
        {
            public List<JiraComment> comments { get; set; }
        }

        public async Task<List<string>> GetIssueCommentsAsync(string issueKey)
        {
            var url = $"{_baseUrl}/rest/api/3/issue/{issueKey}/comment";
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = NewtonsoftJson.JsonConvert.DeserializeObject<JiraCommentResponse>(content);

            var comments = new List<string>();

            foreach (var comment in json.comments)
            {
                try
                {
                    var text = comment.body?.content?[0]?.content?[0]?.text;
                    comments.Add(string.IsNullOrWhiteSpace(text) ? "(Empty comment)" : text);
                }
                catch
                {
                    comments.Add("(Unsupported comment format)");
                }
            }

            return comments;
        }


        public async Task<JiraIssue> GetIssueByKeyAsync(string issueKey)
        {
            var response = await _client.GetAsync($"/rest/api/3/issue/{issueKey}?fields=summary,description,assignee,reporter,status,attachment,labels,priority");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var fields = root.GetProperty("fields");

            string summary = fields.GetProperty("summary").GetString();
            string description = "";
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
                                    description += textVal.GetString() + "\n";
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

            string priority = fields.TryGetProperty("priority", out var priorityElem) &&
                             priorityElem.TryGetProperty("name", out var prioName)
                             ? prioName.GetString()
                             : null;

            string assigneeName = null;
            string assigneeAccountId = null;
            if (fields.TryGetProperty("assignee", out var assigneeElem) && assigneeElem.ValueKind == JsonValueKind.Object)
            {
                if (assigneeElem.TryGetProperty("displayName", out var nameProp))
                    assigneeName = nameProp.GetString();
                if (assigneeElem.TryGetProperty("accountId", out var idProp))
                    assigneeAccountId = idProp.GetString();
            }

            // ✅ Load image attachments (same logic as GetProjectIssuesAsync)
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

            return new JiraIssue
            {
                Key = issueKey,
                Summary = summary,
                Description = description.Trim(),
                Assignee = assigneeName,
                AssigneeAccountId = assigneeAccountId,
                StatusCategory = statusCategory,
                Priority = priority,
                ImageUrls = imageUrls,
                ImageBitmaps = imageBitmaps
            };
        }



        public class JiraConfig
        {
            public string baseUrl { get; set; }
            public string email { get; set; }
            public string apiToken { get; set; }
        }

    }
}
