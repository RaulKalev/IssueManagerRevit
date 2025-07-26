using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IssueManager.Commands;
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

                // DockablePage2
                {
                    var page = new IssueManager.Views.DockablePage2();
                    DockablePaneCreatorService.Register(IssueManager.Views.DockablePage2.Guid, "Ülesanded", page, new DockablePaneHideWhenFamilyDocument());
                }

            };

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab already exists; continue without throwing an error
            }

            // Create Ribbon Panel on the custom tab
            ribbonPanel = application.CreateOrSelectPanel(tabName, "Tools");


            ribbonPanel.CreatePushButton<CommandShow>()
                .SetLargeImage("Assets/Issues.tiff")
                .SetText("Issues")
                .SetToolTip("Create and resolve issues.")
                .SetContextualHelp("https://raulkalev.github.io/rktools/");


                var commandHide = ribbonPanel.CreatePushButton<CommandHide>("Hide")
                .SetLargeImage("Assets/Issues.tiff");
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