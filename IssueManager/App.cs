using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IssueManager.Commands;
using IssueManager.ExternalEvents;
using IssueManager.Services;
using IssueManager.Views;
using ricaun.Revit.UI;

namespace IssueManager
{
    [AppLoader]
    public class App : IExternalApplication
    {
        public static DockablePaneCreatorService DockablePaneCreatorService;
        private static RibbonPanel ribbonPanel;
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "RK Tools";
            DockablePaneCreatorService = new DockablePaneCreatorService(application);
            DockablePaneCreatorService.Initialize();

            application.ControlledApplication.ApplicationInitialized += (sender, args) =>
            {


                var page = new DockablePage2();
                DockablePaneCreatorService.Register(DockablePage2.Guid, "Ülesanded", page, new DockablePaneHideWhenFamilyDocument());
            };


            // Create Ribbon UI
            try { application.CreateRibbonTab(tabName); } catch { }
            ribbonPanel = application.CreateOrSelectPanel(tabName, "Tools");

            ribbonPanel.CreatePushButton<CommandShow>()
                .SetLargeImage("Assets/Issues.tiff")
                .SetText("Ülesanded")
                .SetToolTip("Jira ülesanded Revitis.")
                .SetContextualHelp("https://raulkalev.github.io/rktools/");

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            ribbonPanel?.Remove();

            DockablePaneCreatorService.Dispose();

            return Result.Succeeded;
        }
    }

}