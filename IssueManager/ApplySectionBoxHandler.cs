using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace IssueManager.ExternalEvents
{
    public class ApplySectionBoxHandler : IExternalEventHandler
    {
        private string _metadata;

        public void SetTarget(string description)
        {
            _metadata = description;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_metadata))
                {
                    TaskDialog.Show("Debug", "No metadata provided.");
                    return;
                }


                var cleaned = _metadata
                    .Replace("<!--", "")
                    .Replace("-->", "")
                    .Trim();


                var match = Regex.Match(cleaned,
                    @"\[SECTION_BOX\]\s*(-?\d+,\d+)\s*,\s*(-?\d+,\d+)\s*,\s*(-?\d+,\d+)\s*\|\s*(-?\d+,\d+)\s*,\s*(-?\d+,\d+)\s*,\s*(-?\d+,\d+)");

                if (!match.Success)
                {
                    return;
                }

                // Use Estonian-style culture to parse comma decimals
                var culture = new CultureInfo("et-EE");
                double MeterToFeet(double m) => m * 3.28084;

                var min = new XYZ(
                    MeterToFeet(double.Parse(match.Groups[1].Value, culture)),
                    MeterToFeet(double.Parse(match.Groups[2].Value, culture)),
                    MeterToFeet(double.Parse(match.Groups[3].Value, culture)));

                var max = new XYZ(
                    MeterToFeet(double.Parse(match.Groups[4].Value, culture)),
                    MeterToFeet(double.Parse(match.Groups[5].Value, culture)),
                    MeterToFeet(double.Parse(match.Groups[6].Value, culture)));



                var trueMin = new XYZ(
                    Math.Min(min.X, max.X),
                    Math.Min(min.Y, max.Y),
                    Math.Min(min.Z, max.Z));

                var trueMax = new XYZ(
                    Math.Max(min.X, max.X),
                    Math.Max(min.Y, max.Y),
                    Math.Max(min.Z, max.Z));

                var box = new BoundingBoxXYZ
                {
                    Min = trueMin,
                    Max = trueMax,
                    Transform = Transform.Identity
                };

                var doc = app.ActiveUIDocument.Document;
                var collector = new FilteredElementCollector(doc);
                var view3D = collector.OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && v.Name == "{3D}");

                if (view3D == null)
                {
                    TaskDialog.Show("Debug", "Default 3D view '{3D}' not found.");
                    return;
                }

                using (var tx = new Transaction(doc, "Apply Section Box"))
                {
                    tx.Start();
                    view3D.SetSectionBox(box);
                    view3D.IsSectionBoxActive = true;
                    tx.Commit();
                }

                // Switch to the 3D view
                app.ActiveUIDocument.RequestViewChange(view3D);

                // Create the dummy shape in a transaction
                ElementId dummyId;
                using (var tx = new Transaction(doc, "Zoom Helper"))
                {
                    tx.Start();

                    var dummy = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    dummy.SetShape(new GeometryObject[] { Line.CreateBound(trueMin, trueMax) });
                    dummyId = dummy.Id;

                    tx.Commit();
                }

                // Give Revit some time to refresh the view (optional, can be omitted)
                System.Threading.Thread.Sleep(100);

                // Show and delete the dummy synchronously
                var uidoc = app.ActiveUIDocument;
                try
                {
                    uidoc.ShowElements(dummyId);

                    using (var cleanupTx = new Transaction(doc, "Remove Zoom Helper"))
                    {
                        cleanupTx.Start();
                        doc.Delete(dummyId);
                        cleanupTx.Commit();
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Zoom Error", ex.Message);
                }



            }
            catch (Exception ex)
            {
                TaskDialog.Show("Exception", ex.ToString());
            }
        }

        public string GetName() => "Apply Section Box";
    }
}
