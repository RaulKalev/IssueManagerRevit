using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IssueManager.ExternalEvents;
using IssueManager.Services;
using IssueManager.Views;
using System.Windows;

namespace IssueManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CommandShow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            ServiceRegistry.Uidoc = uiapp.ActiveUIDocument;

            if (ServiceRegistry.CaptureImageEvent == null)
            {
                var handler = new CaptureViewImageHandler
                {
                    UiDoc = uiapp.ActiveUIDocument
                };
                ServiceRegistry.CaptureViewImageHandler = handler;
                ServiceRegistry.CaptureImageEvent = ExternalEvent.Create(handler);

            }
            if (ServiceRegistry.ApplySectionBoxEvent == null)
            {
                var handler = new ApplySectionBoxHandler();
                ServiceRegistry.ApplySectionBoxHandler = handler;
                ServiceRegistry.ApplySectionBoxEvent = ExternalEvent.Create(handler);
            }

            App.DockablePaneCreatorService.Get(DockablePage2.Guid)?.Show();

            return Result.Succeeded;
        }

    }
}
