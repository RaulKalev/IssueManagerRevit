using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IssueManager.Models
{
    public class JiraIssue : INotifyPropertyChanged
    {
        private bool isEditing;
        private string description;
        private string assignee;
        private string statusCategory;

        public string Key { get; set; }
        public string Summary { get; set; }
        public string Reporter { get; set; }

        public List<string> ImageUrls { get; set; }
        public List<BitmapImage> ImageBitmaps { get; set; }

        public List<JiraUser> AllAssignees { get; set; }
        public List<string> AllStatuses { get; set; }
        public string Assignee { get; set; } // Display name
        public string AssigneeAccountId { get; set; } // Actual account ID for update
        public List<string> Labels { get; set; }
        public string LabelsText => Labels != null && Labels.Count > 0
            ? string.Join(", ", Labels)
            : "—";

        public bool IsEditing
        {
            get => isEditing;
            set { isEditing = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => description;
            set { description = value; OnPropertyChanged(); }
        }


        public string StatusCategory
        {
            get => statusCategory;
            set { statusCategory = value; OnPropertyChanged(); }
        }

        public ICommand ToggleEditCommand { get; set; }

        public JiraIssue()
        {
            ImageUrls = new List<string>();
            ImageBitmaps = new List<BitmapImage>();
            AllAssignees = new List<JiraUser>();
            AllStatuses = new List<string>();
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
