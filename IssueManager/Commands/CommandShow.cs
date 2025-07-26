using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IssueManager.Services;
using IssueManager.Views;

namespace IssueManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CommandShow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet)
        {
            UIApplication uiapp = commandData.Application;

            App.DockablePaneCreatorService.Get(IssueManager.Views.DockablePage2.Guid)?.Show();

            return Result.Succeeded;
        }
    }

}
