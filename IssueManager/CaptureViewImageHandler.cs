using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using IssueManager.Services;

namespace IssueManager.ExternalEvents
{
    public class CaptureViewImageHandler : IExternalEventHandler
    {
        public UIDocument UiDoc { get; set; }
        public string ResultImagePath { get; private set; }
        public Action<string> OnImageCaptured { get; set; }
        public string SectionBoxMetadata { get; private set; } = null;

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = UiDoc.Document;
                var view = doc.ActiveView;
                var uidoc = ServiceRegistry.Uidoc;

                string tempImagePath = Path.Combine(Path.GetTempPath(), $"RevitView_{Guid.NewGuid()}.png");

                using (Transaction tx = new Transaction(doc, "Export View Image"))
                {
                    tx.Start();

                    var imageOptions = new ImageExportOptions
                    {
                        ExportRange = ExportRange.CurrentView,
                        FilePath = tempImagePath.Replace(".png", ""),
                        FitDirection = FitDirectionType.Horizontal,
                        HLRandWFViewsFileType = ImageFileType.PNG,
                        ImageResolution = ImageResolution.DPI_150
                    };

                    doc.ExportImage(imageOptions);
                    tx.RollBack(); // we didn’t alter model
                }

                string exported = tempImagePath;
                if (!File.Exists(exported))
                {
                    // Revit appends -1.png, -2.png etc. to the filename
                    var folder = Path.GetDirectoryName(tempImagePath);
                    var baseName = Path.GetFileNameWithoutExtension(tempImagePath);
                    var candidates = Directory.GetFiles(folder, baseName + "-*.png");
                    if (candidates.Length > 0)
                        exported = candidates[0];
                }

                ResultImagePath = exported;

                // ✅ Check and store section box coordinates (in world coordinates and meters)
                if (view is View3D view3D && view3D.IsSectionBoxActive)
                {
                    var box = view3D.GetSectionBox();
                    var transform = box.Transform;

                    var min = transform.OfPoint(box.Min);
                    var max = transform.OfPoint(box.Max);

                    // Convert from feet to meters
                    Func<double, string> format = d => (d / 3.28084).ToString("0.###", new System.Globalization.CultureInfo("et-EE"));

                    SectionBoxMetadata = $"[SECTION_BOX] {format(min.X)},{format(min.Y)},{format(min.Z)}|{format(max.X)},{format(max.Y)},{format(max.Z)}";
                }
                else
                {
                    SectionBoxMetadata = null;
                }

                OnImageCaptured?.Invoke(ResultImagePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Capture View Error", ex.Message);
            }
        }

        public string GetName() => "Capture View Image";
    }
}
