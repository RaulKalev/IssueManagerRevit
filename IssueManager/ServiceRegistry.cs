using Autodesk.Revit.UI;
using IssueManager.ExternalEvents;

namespace IssueManager.Services
{
    public static class ServiceRegistry
    {
        public static UIDocument Uidoc { get; set; }
        public static ExternalEvent CaptureImageEvent { get; set; }
        public static CaptureViewImageHandler CaptureViewImageHandler { get; set; }
        public static ApplySectionBoxHandler ApplySectionBoxHandler { get; set; }
        public static ExternalEvent ApplySectionBoxEvent { get; set; }

    }
}
