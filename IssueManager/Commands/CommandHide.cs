using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IssueManager.Services;
using IssueManager.Views;
using System;

namespace IssueManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CommandHide : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet)
        {
            // Hide the dockable pane
            var pane = App.DockablePaneCreatorService.Get(DockablePage2.Guid);
            pane?.Hide();

            // Run cleanup logic
            if (App.DockablePaneCreatorService.GetFrameworkElement(DockablePage2.Guid) is DockablePage2 page)
            {
                page.Cleanup(); // Add this method in your page class
            }

            return Result.Succeeded;
        }
    }
}
