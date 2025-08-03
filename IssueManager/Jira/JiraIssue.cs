using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IssueManager.Models
{
    public class ImageWithIssue
    {
        public BitmapImage Image { get; set; }
        public JiraIssue Issue { get; set; }
    }

    public class JiraIssue : INotifyPropertyChanged
    {
        private bool isEditing;
        private string description;
        private string assignee;
        private string statusCategory;
        public static Action FilterCallback;
        public DateTime? CreatedDate { get; set; }
        public string OriginalDescriptionADF { get; set; }
        public JsonElement? OriginalADFJson { get; set; }
        public ObservableCollection<ImageWithIssue> ImageWithIssues { get; set; } = new ObservableCollection<ImageWithIssue>();
        public ObservableCollection<string> Comments { get; set; } = new ObservableCollection<string>();
        public string CommentsText => Comments != null && Comments.Count > 0
            ? string.Join("\n• ", Comments.Prepend("•"))
            : "—";
        public ObservableCollection<BitmapImage> AdditionalImages { get; set; } = new ObservableCollection<BitmapImage>();

        public string Key { get; set; }
        public string Reporter { get; set; }
        private string summary;
        public string Summary
        {
            get => summary;
            set { summary = value; OnPropertyChanged(); }
        }
        // Available priorities for ComboBox
        public ObservableCollection<string> PriorityOptions { get; set; } =
            new ObservableCollection<string> { "Highest", "High", "Medium", "Low", "Lowest" };
        public string CleanDescription => Regex.Replace(Description ?? "", @"<!--.*?-->", "").Trim();
        public string EditableDescription
        {
            get => CleanDescription;
            set
            {
                // Preserve existing section box metadata
                string metadata = "";
                var match = Regex.Match(Description ?? "", @"<!--(.*?)-->");
                if (match.Success)
                    metadata = match.Value;

                Description = $"{value.Trim()}\n\n{metadata}".Trim();

                OnPropertyChanged(nameof(EditableDescription));
                OnPropertyChanged(nameof(Description));
            }
        }


        // All label options shown in CheckComboBox
        public ObservableCollection<string> AllLabels { get; set; } = new ObservableCollection<string>();

        private string priority;
        public string Priority
        {
            get => priority;
            set { priority = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriorityIcon)); OnPropertyChanged(nameof(PriorityColor)); }
        }

        public List<string> ImageUrls { get; set; }
        public List<BitmapImage> ImageBitmaps { get; set; }

        public List<JiraUser> AllAssignees { get; set; }
        public List<string> AllStatuses { get; set; }
        public string Assignee { get; set; } // Display name
        public string AssigneeAccountId { get; set; } // Actual account ID for update
        private ObservableCollection<string> labels = new ObservableCollection<string>();
        public ObservableCollection<string> Labels
        {
            get => labels;
            set
            {
                labels = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LabelsText));
            }
        }
        public string LabelsText => Labels != null && Labels.Count > 0
            ? string.Join(", ", Labels)
            : "—";

        public bool IsChecked
        {
            get => StatusCategory == "Done";
            set
            {
                if (value && StatusCategory != "Done")
                {
                    // Fire and forget async call
                    _ = Task.Run(async () =>
                    {
                        if (UpdateCallback != null)
                        {
                            bool success = await UpdateCallback(this);
                            if (success)
                            {
                                StatusCategory = "Done"; // triggers UI update
                            }
                        }
                    });
                }

                // No unchecking allowed
                OnPropertyChanged();
            }
        }
        public string PriorityIcon
        {
            get
            {
                switch (Priority)
                {
                    case "Highest": return "!!"; // double up arrow
                    case "High": return "!";   // single up arrow
                    case "Medium": return "≡"; // triple bar (neutral)
                    case "Low": return "↓";    // single down arrow
                    case "Lowest": return "⇊"; // double down arrow
                    default: return "";
                }
            }
        }

        public Brush PriorityColor
        {
            get
            {
                switch (Priority)
                {
                    case "Highest": return Brushes.Red;
                    case "High": return Brushes.OrangeRed;
                    case "Medium": return Brushes.Goldenrod;
                    case "Low": return Brushes.DodgerBlue;
                    case "Lowest": return Brushes.MediumBlue;
                    default: return Brushes.Gray;
                }
            }
        }
        public int PriorityRank
        {
            get
            {
                switch (Priority)
                {
                    case "Highest": return 1;
                    case "High": return 2;
                    case "Medium": return 3;
                    case "Low": return 4;
                    case "Lowest": return 5;
                    default: return 99;
                }
            }
        }


        public bool IsWaiting
        {
            get => StatusCategory == "Ootel";
            set
            {
                if (value && StatusCategory != "Ootel")
                    _ = SetStatusCategoryAsync("Ootel");
                OnPropertyChanged();
            }
        }

        public bool IsInProgress
        {
            get => StatusCategory == "Tegemisel";
            set
            {
                if (value && StatusCategory != "Tegemisel")
                    _ = SetStatusCategoryAsync("Tegemisel");
                OnPropertyChanged();
            }
        }

        public bool IsDone
        {
            get => StatusCategory == "Done";
            set
            {
                if (value && StatusCategory != "Done")
                    _ = SetStatusCategoryAsync("Done");
                OnPropertyChanged();
            }
        }
        private async Task SetStatusCategoryAsync(string newStatus)
        {
            // ✅ Update local status immediately
            StatusCategory = newStatus;
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(IsInProgress));
            OnPropertyChanged(nameof(IsDone));

            // 🔄 Refresh filter view
            FilterCallback?.Invoke();

            // 🔄 Jira update
            if (UpdateCallback != null)
            {
                // ✅ Auto-assign current user if not already assigned
                if (string.IsNullOrEmpty(AssigneeAccountId) && AllAssignees != null)
                {
                    var current = AllAssignees.Find(u => u.IsCurrentUser);
                    if (current != null)
                    {
                        AssigneeAccountId = current.AccountId;
                        Assignee = current.DisplayName;
                        OnPropertyChanged(nameof(Assignee));
                    }
                }

                bool success = await UpdateCallback(this);

                if (!success)
                {
                    MessageBox.Show($"Failed to update status to {newStatus}.", "Jira Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public bool IsEditing
        {
            get => isEditing;
            set { isEditing = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => description;
            set
            {
                description = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CleanDescription)); // <- add this line
            }
        }

        public string StatusCategory
        {
            get => statusCategory;
            set
            {
                statusCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsChecked)); // ✅ trigger checkbox refresh
            }
        }


        public ICommand ToggleEditCommand { get; set; }

        public JiraIssue()
        {
            ImageUrls = new List<string>();
            ImageBitmaps = new List<BitmapImage>();
            AllAssignees = new List<JiraUser>();
            AllStatuses = new List<string>();
            Labels = new ObservableCollection<string>();
            Labels.CollectionChanged += (s, e) => OnPropertyChanged(nameof(LabelsText));

            Comments.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CommentsText)); // ✅ Add this

            ToggleEditCommand = new RelayCommand(ToggleEdit);
        }

        public static Func<JiraIssue, Task<bool>> UpdateCallback;

        private async void ToggleEdit()
        {
            if (IsEditing)
            {
                IsEditing = false;

                if (UpdateCallback != null)
                {
                    bool success = await UpdateCallback(this);

                    if (success)
                    {
                        // 🟢 Sync Assignee display name from selected AccountId
                        if (!string.IsNullOrEmpty(AssigneeAccountId) && AllAssignees != null)
                        {
                            var match = AllAssignees.Find(u => u.AccountId == AssigneeAccountId);
                            if (match != null)
                                Assignee = match.DisplayName;
                        }
                    }
                }
            }
            else
            {
                IsEditing = true;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
