using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using WallabyHop.Logic;

namespace WallabyHop.Components.Nesting
{
    public class HopSheetExportComponent : GH_Component
    {
        public HopSheetExportComponent()
            : base("HopSheetExport", "HopSheetExport",
                "Exports a per-sheet .hop file from nested HopPart objects. Filters parts by OpenNest sheet index, extracts sheet dimensions from curve, and writes NC-Hops format.",
                "Wallaby Hop", "Nesting")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Parts", "parts",
                "HopPart dictionary objects from OpenNest transformed output. Must be GH_ObjectWrapper-wrapped dictionaries.",
                GH_ParamAccess.list);
            pManager.AddIntegerParameter("IDS", "ids",
                "OpenNest sheet assignment indices. Each entry maps a part to a sheet number. -1 = unfitted.",
                GH_ParamAccess.list);
            pManager.AddTransformParameter("Transforms", "transforms",
                "OpenNest per-part placement transforms (parallel to Parts/IDS). Each part's " +
                "operation coordinates are moved by its transform before export. Only " +
                "UNROTATED placements are supported so far -- lock rotation in OpenNest " +
                "(Rotations=1 / grain direction) or the part is refused with an error.",
                GH_ParamAccess.list);
            pManager.AddCurveParameter("SheetCurve", "sheetCurve",
                "Sheet boundary curve from OpenNest. Used to extract sheet dx/dy dimensions via bounding box.",
                GH_ParamAccess.item);
            pManager.AddIntegerParameter("SheetIndex", "sheetIndex",
                "Which sheet to export (0-based index matching OpenNest sheet numbering).",
                GH_ParamAccess.item);
            pManager.AddTextParameter("Folder", "folder",
                "Output directory path. Must exist. Example: D:\\output\\",
                GH_ParamAccess.item);
            pManager.AddTextParameter("FileName", "fileName",
                "Output file name without .hop extension. Example: sheet_01",
                GH_ParamAccess.item);
            pManager.AddTextParameter("WZGV", "wzgv",
                "Tool preset identifier string for the .hop header. Default: 7023K_681.",
                GH_ParamAccess.item, "7023K_681");
            pManager[6].Optional = true;
            pManager.AddNumberParameter("DZ", "dz",
                "Material thickness in mm. Cannot be derived from 2D sheet curve, so must be specified. Default 19.0.",
                GH_ParamAccess.item, 19.0);
            pManager[7].Optional = true;
            pManager.AddBooleanParameter("Export", "export",
                "Toggle to trigger file write. False = no file written. Default false.",
                GH_ParamAccess.item, false);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("HopContent", "hopContent",
                "Generated .hop file content as a string for inspection.",
                GH_ParamAccess.item);
            pManager.AddTextParameter("StatusMsg", "statusMsg",
                "Export status message showing file path and part count.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ---------------------------------------------------------------
            // 1. DEFAULTS -- downstream gets these if guards trigger
            // ---------------------------------------------------------------
            DA.SetData(0, "");
            DA.SetData(1, "");

            // ---------------------------------------------------------------
            // 2. READ INPUTS
            // ---------------------------------------------------------------
            List<object> parts = new List<object>();
            DA.GetDataList(0, parts);

            List<int> ids = new List<int>();
            DA.GetDataList(1, ids);

            List<Transform> transforms = new List<Transform>();
            DA.GetDataList(2, transforms);

            Curve sheetCurve = null;
            if (!DA.GetData(3, ref sheetCurve)) return;

            int sheetIndex = 0;
            if (!DA.GetData(4, ref sheetIndex)) return;

            string folder = null;
            if (!DA.GetData(5, ref folder)) return;

            string fileName = null;
            if (!DA.GetData(6, ref fileName)) return;

            string wzgv = null;
            DA.GetData(7, ref wzgv);

            double dz = 19.0;
            DA.GetData(8, ref dz);

            bool export = false;
            DA.GetData(9, ref export);

            // ---------------------------------------------------------------
            // 3. EXPORT GUARD -- rising edge only; re-emit cached content on
            // non-edge solves so downstream components keep their data.
            // ---------------------------------------------------------------
            bool risingEdge = export && !_lastExport;
            _lastExport = export;
            if (!risingEdge)
            {
                if (_lastContent != null)
                {
                    DA.SetData(0, _lastContent);
                    DA.SetData(1, _lastStatus);
                }
                return;
            }

            // ---------------------------------------------------------------
            // 4. INPUT DEFAULTS -- fallback for disconnected inputs
            // ---------------------------------------------------------------
            if (wzgv == null) wzgv = "7023K_681";
            if (dz <= 0) dz = 19.0;

            // ---------------------------------------------------------------
            // 5. VALIDATION -- error messages for invalid required inputs
            // ---------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(folder))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error, "HopSheetExport: folder is empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error, "HopSheetExport: fileName is empty");
                return;
            }

            string directory = Path.GetFullPath(folder);

            if (!Directory.Exists(directory))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: directory does not exist: " + directory);
                return;
            }

            // Strip .hop extension if user typed it, then re-add -- always clean
            string stem = fileName.EndsWith(".hop", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;

            string fullPath = Path.Combine(directory, stem + ".hop");

            if (parts == null || parts.Count == 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: no parts connected");
                return;
            }

            if (ids == null || ids.Count == 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: no IDS connected (connect OpenNest IDS output)");
                return;
            }

            if (parts.Count != ids.Count)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: parts count (" + parts.Count
                    + ") != ids count (" + ids.Count + ") -- lists must match");
                return;
            }

            if (transforms.Count == 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: no Transforms connected. Wire OpenNest's Transform "
                    + "output -- without it every part would machine at its local origin.");
                return;
            }

            if (transforms.Count != parts.Count)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: transforms count (" + transforms.Count
                    + ") != parts count (" + parts.Count + ") -- lists must match");
                return;
            }

            if (sheetCurve == null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: no sheet curve connected");
                return;
            }

            if (sheetIndex < 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: sheetIndex must be >= 0");
                return;
            }

            // ---------------------------------------------------------------
            // 6. SHEET DIMENSIONS -- BoundingBox pattern from HopSheet.cs
            // ---------------------------------------------------------------
            BoundingBox sheetBB = sheetCurve.GetBoundingBox(true);
            double sheetDx = sheetBB.Max.X - sheetBB.Min.X;
            double sheetDy = sheetBB.Max.Y - sheetBB.Min.Y;
            // dz comes from input parameter (cannot derive from 2D sheet curve)

            // ---------------------------------------------------------------
            // 7. FILTER PARTS BY SHEET INDEX -- OpenNest IDS semantics
            // ---------------------------------------------------------------
            List<string> allOpLines = new List<string>();
            int partsOnSheet = 0;
            int unfittedCount = 0;
            bool exportBlocked = false;

            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == -1)
                {
                    unfittedCount++;
                    continue;
                }
                if (ids[i] != sheetIndex) continue;

                // Unwrap HopPart dictionary
                var wrapper = parts[i] as GH_ObjectWrapper;
                if (wrapper == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Part " + i + " is not a HopPart wrapper -- skipped");
                    continue;
                }
                var dict = wrapper.Value as Dictionary<string, object>;
                if (dict == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Part " + i + " does not contain a part dictionary -- skipped");
                    continue;
                }
                if (!dict.ContainsKey("operationLines"))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Part " + i + " has no 'operationLines' key (is this a HopPart dict?) -- skipped");
                    continue;
                }

                string partLabel = dict.ContainsKey("panelName")
                    ? (dict["panelName"] as string ?? ("part " + i))
                    : ("part " + i);

                var opLineGroups = dict["operationLines"] as List<List<string>>;
                var partLines = new List<string>();
                if (opLineGroups != null)
                    foreach (var group in opLineGroups)
                        foreach (string line in group)
                            partLines.Add(line);

                // C2: rewrite part-local coordinates into sheet coordinates.
                Transform xf = transforms[i];
                var placement = new OpLineTransformer.Placement
                {
                    Tx = xf.M03,
                    Ty = xf.M13,
                    RotationDeg = RotationDegreesOf(xf),
                };
                var tr = OpLineTransformer.Apply(partLines, placement);
                if (tr.Error != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "HopSheetExport: " + partLabel + " " + tr.Error);
                    exportBlocked = true;
                    continue;
                }
                allOpLines.AddRange(tr.Lines);
                partsOnSheet++;
            }

            if (unfittedCount > 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    unfittedCount + " part(s) did not fit on any sheet (IDS=-1)");
            }

            if (exportBlocked)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: file NOT written -- one or more parts could not "
                    + "be placed (see errors above)");
                return;
            }

            if (partsOnSheet == 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "HopSheetExport: no parts assigned to sheet " + sheetIndex);
                return;
            }

            // ---------------------------------------------------------------
            // 8. BUILD HEADER -- match HopExport.cs / Muster_DXF_Import.hop
            //    order exactly
            // ---------------------------------------------------------------
            string ncName = stem;

            List<string> lines = new List<string>();
            lines.Add(";MAKROTYP=0");
            lines.Add(";INSTVERSION=");
            lines.Add(";EXEVERSION=");
            lines.Add(";BILD=");
            lines.Add(";INFO=");

            // WZGV is conditional -- omit line entirely if empty string
            if (!string.IsNullOrEmpty(wzgv))
            {
                lines.Add(";WZGV=" + wzgv);
            }

            lines.Add(";WZGVCONFIG=");
            lines.Add(";MASCHINE=HOLZHER");
            lines.Add(";NCNAME=" + ncName);
            lines.Add(";KOMMENTAR=");
            lines.Add(";DX=0.000");
            lines.Add(";DY=0.000");
            lines.Add(";DZ=0");
            lines.Add(";DIALOGDLL=Dialoge.Dll");
            lines.Add(";DIALOGPROC=StandardFormAnzeigen");
            lines.Add(";AUTOSCRIPTSTART=1");
            lines.Add(";BUTTONBILD=");
            lines.Add(";DIMENSION_UNIT=0");

            // ---------------------------------------------------------------
            // 9. BUILD VARS BLOCK -- 3-space indent, InvariantCulture decimals
            // ---------------------------------------------------------------
            lines.Add("VARS");
            lines.Add("   DX := " + NcFmt.F(sheetDx) + ";*VAR*Dimension X");
            lines.Add("   DY := " + NcFmt.F(sheetDy) + ";*VAR*Dimension Y");
            lines.Add("   DZ := " + NcFmt.F(dz) + ";*VAR*Dimension Z");

            // ---------------------------------------------------------------
            // 10. BUILD START SECTION -- Fertigteil + HH_Park
            // ---------------------------------------------------------------
            lines.Add("START");
            lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
            lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

            // ---------------------------------------------------------------
            // 11. INSERT OPERATION LINES -- bucket-sorted so duplicate tool
            // calls from different parts merge (same as HopExport)
            // ---------------------------------------------------------------
            foreach (string line in NcExport.SortOperationLines(allOpLines))
                lines.Add(line);

            // ---------------------------------------------------------------
            // 12. ASSEMBLE + PRE-WRITE VALIDATION GATE
            // ---------------------------------------------------------------
            string content = string.Join("\r\n", lines) + "\r\n";

            var analysis = Logic.HopAnalyzer.Analyze(content);
            foreach (string zw in analysis.ZWarnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, zw);
            if (!analysis.IsValid)
            {
                foreach (string err in analysis.Errors)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                foreach (string fc in analysis.FixchipWarnings)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, fc);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: file NOT written -- fix the errors above ("
                    + analysis.Summary + ")");
                _lastContent = content;
                _lastStatus = "BLOCKED: " + analysis.Summary;
                DA.SetData(0, content);
                DA.SetData(1, _lastStatus);
                return;
            }

            // ---------------------------------------------------------------
            // 13. ATOMIC WRITE + SUCCESS OUTPUT
            // ---------------------------------------------------------------
            try
            {
                string tmpPath = fullPath + ".tmp";
                File.WriteAllText(tmpPath, content, Encoding.ASCII);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.Move(tmpPath, fullPath);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopSheetExport: write failed: " + ex.Message);
                return;
            }

            string status = "Exported sheet " + sheetIndex + " (" + partsOnSheet
                + " parts) -> " + fullPath + "  [" + analysis.Summary + "]";
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, status);
            _lastContent = content;
            _lastStatus = status;
            DA.SetData(0, content);
            DA.SetData(1, status);
        }

        // Rising-edge memory + content cache
        private bool _lastExport = false;
        private string _lastContent = null;
        private string _lastStatus = null;

        // Rotation angle (degrees) of an XY-plane transform. Pure translations
        // return 0.
        private static double RotationDegreesOf(Transform xf)
        {
            return Math.Atan2(xf.M10, xf.M00) * 180.0 / Math.PI;
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopSheetExport");

        public override Guid ComponentGuid => new Guid("f9663298-da7b-432a-b38b-e0ded18ade94");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Skip(),   // Transforms
                WallabyHop.AutoWire.Spec.Curve(),
                WallabyHop.AutoWire.Spec.Int("0<0<20"),
                WallabyHop.AutoWire.Spec.FilePath(),
                WallabyHop.AutoWire.Spec.Panel("sheet_1"),
                WallabyHop.AutoWire.Spec.Panel(""),
                WallabyHop.AutoWire.Spec.Float("1<18<200"),
                WallabyHop.AutoWire.Spec.Toggle(),
            });
        }
    }
}
