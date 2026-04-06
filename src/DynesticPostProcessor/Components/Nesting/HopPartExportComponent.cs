using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace DynesticPostProcessor.Components.Nesting
{
    public class HopPartExportComponent : GH_Component
    {
        public HopPartExportComponent()
            : base("HopPartExport", "HopPartExport",
                "Exports one .hop file per part from a list of HopPart or HopKorpus panel dictionaries. " +
                "When CabinetData is connected, automatically creates a subfolder named Korpus_{Nr}_{W}x{H}x{D}.",
                "DYNESTIC", "Nesting")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Index 0
            pManager.AddGenericParameter("Parts", "parts",
                "List of HopPart dictionary objects (GH_ObjectWrapper). Wire the 'Panels' output of HopKorpus or the 'Part' output of HopPart here.",
                GH_ParamAccess.list);

            // Index 1
            pManager.AddTextParameter("Folder", "folder",
                "Base output directory. Must exist. Example: D:\\output\\",
                GH_ParamAccess.item);

            // Index 2 -- optional, drives auto subfolder
            pManager.AddGenericParameter("CabinetData", "cabinet",
                "Optional. CabinetData dictionary from HopKorpus. When connected, exports into a subfolder " +
                "named Korpus_{CorpusNr}_{W}x{H}x{D}, creating the folder if needed.",
                GH_ParamAccess.item);
            pManager[2].Optional = true;

            // Index 3
            pManager.AddIntegerParameter("CorpusNr", "nr",
                "Sequential cabinet number used in the auto-generated subfolder name. Default 1.",
                GH_ParamAccess.item, 1);
            pManager[3].Optional = true;

            // Index 4
            pManager.AddTextParameter("WZGV", "wzgv",
                "Tool preset identifier string for the .hop header. Default: 7023K_681.",
                GH_ParamAccess.item, "7023K_681");
            pManager[4].Optional = true;

            // Index 5
            pManager.AddNumberParameter("DZ", "dz",
                "Material thickness in mm used in VARS block. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[5].Optional = true;

            // Index 6
            pManager.AddBooleanParameter("Export", "export",
                "Toggle to trigger file write. False = no files written. Default false.",
                GH_ParamAccess.item, false);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("FilePaths", "filePaths",
                "List of written .hop file paths, one per exported part.",
                GH_ParamAccess.list);
            pManager.AddTextParameter("StatusMsg", "statusMsg",
                "Export status summary.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ---------------------------------------------------------------
            // 1. DEFAULTS
            // ---------------------------------------------------------------
            DA.SetDataList(0, new List<string>());
            DA.SetData(1, "");

            // ---------------------------------------------------------------
            // 2. READ INPUTS
            // ---------------------------------------------------------------
            List<object> parts = new List<object>();
            DA.GetDataList(0, parts);

            string folder = null;
            if (!DA.GetData(1, ref folder)) return;

            GH_ObjectWrapper cabinetWrap = null;
            DA.GetData(2, ref cabinetWrap);
            var cabinetDict = cabinetWrap?.Value as Dictionary<string, object>;

            int corpusNr = 1;
            DA.GetData(3, ref corpusNr);
            if (corpusNr < 1) corpusNr = 1;

            string wzgv = "7023K_681";
            DA.GetData(4, ref wzgv);
            if (wzgv == null) wzgv = "7023K_681";

            double dz = 19.0;
            DA.GetData(5, ref dz);
            if (dz <= 0) dz = 19.0;

            bool export = false;
            DA.GetData(6, ref export);

            // ---------------------------------------------------------------
            // 3. EXPORT GUARD
            // ---------------------------------------------------------------
            if (!export) return;

            // ---------------------------------------------------------------
            // 4. VALIDATE BASE FOLDER
            // ---------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(folder))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPartExport: folder is empty");
                return;
            }

            string baseDir = Path.GetFullPath(folder);
            if (!Directory.Exists(baseDir))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPartExport: base folder does not exist: " + baseDir);
                return;
            }

            // ---------------------------------------------------------------
            // 5. RESOLVE OUTPUT DIRECTORY
            //    If CabinetData connected: create Korpus_{Nr}_{W}x{H}x{D} subfolder
            // ---------------------------------------------------------------
            string outputDir = baseDir;

            if (cabinetDict != null)
            {
                double cabW = Convert.ToDouble(cabinetDict["W"]);
                double cabH = Convert.ToDouble(cabinetDict["H"]);
                double cabD = Convert.ToDouble(cabinetDict["D"]);

                string subName = string.Format(CultureInfo.InvariantCulture,
                    "Korpus_{0}_{1:F0}x{2:F0}x{3:F0}",
                    corpusNr, cabW, cabH, cabD);

                outputDir = Path.Combine(baseDir, subName);

                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "HopPartExport: output subfolder -> " + outputDir);
            }

            // ---------------------------------------------------------------
            // 6. VALIDATE PARTS LIST
            // ---------------------------------------------------------------
            if (parts == null || parts.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HopPartExport: no parts connected");
                return;
            }

            // ---------------------------------------------------------------
            // 7. EXPORT ONE FILE PER PART
            // ---------------------------------------------------------------
            var writtenPaths = new List<string>();
            int successCount = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                var wrapper = parts[i] as GH_ObjectWrapper;
                if (wrapper == null) continue;
                var dict = wrapper.Value as Dictionary<string, object>;
                if (dict == null) continue;

                // -- Extract fields --
                Curve outline = dict.ContainsKey("outline") ? dict["outline"] as Curve : null;
                if (outline == null) continue;

                string panelName = dict.ContainsKey("panelName")
                    ? (dict["panelName"] as string ?? "")
                    : "";
                if (panelName.Length == 0)
                    panelName = "part_" + (i + 1);

                var opLineGroups = dict.ContainsKey("operationLines")
                    ? dict["operationLines"] as List<List<string>>
                    : null;

                // -- Part dimensions from outline bounding box --
                BoundingBox bbox = outline.GetBoundingBox(true);
                double partDx = bbox.Max.X - bbox.Min.X;
                double partDy = bbox.Max.Y - bbox.Min.Y;

                // -- Per-panel thickness overrides global DZ if present --
                double partDz = dict.ContainsKey("thickness")
                    ? Convert.ToDouble(dict["thickness"])
                    : dz;

                // -- Sanitise filename (strip reserved chars) --
                string stem = SanitiseFileName(panelName);
                string fullPath = Path.Combine(outputDir, stem + ".hop");

                // -- Build .hop content --
                string content = BuildHopContent(stem, partDx, partDy, partDz, wzgv, opLineGroups);

                // -- Write file --
                File.WriteAllText(fullPath, content, Encoding.ASCII);
                writtenPaths.Add(fullPath);
                successCount++;
            }

            // ---------------------------------------------------------------
            // 8. OUTPUTS
            // ---------------------------------------------------------------
            string status = string.Format(
                "HopPartExport: {0}/{1} parts exported -> {2}",
                successCount, parts.Count, outputDir);

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, status);
            DA.SetDataList(0, writtenPaths);
            DA.SetData(1, status);
        }

        // ---------------------------------------------------------------
        // BUILD .HOP FILE CONTENT
        // Matches header structure of HopExportComponent / HopSheetExportComponent
        // ---------------------------------------------------------------
        private static string BuildHopContent(
            string ncName,
            double dx, double dy, double dz,
            string wzgv,
            List<List<string>> opLineGroups)
        {
            var lines = new List<string>();

            // Header
            lines.Add(";MAKROTYP=0");
            lines.Add(";INSTVERSION=");
            lines.Add(";EXEVERSION=");
            lines.Add(";BILD=");
            lines.Add(";INFO=");

            if (!string.IsNullOrEmpty(wzgv))
                lines.Add(";WZGV=" + wzgv);

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

            // VARS block
            lines.Add("VARS");
            lines.Add("   DX := " + dx.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension X");
            lines.Add("   DY := " + dy.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Y");
            lines.Add("   DZ := " + dz.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Z");

            // START section
            lines.Add("START");
            lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
            lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

            // Operation lines
            if (opLineGroups != null)
            {
                foreach (var group in opLineGroups)
                    foreach (string line in group)
                        lines.Add(line);
            }

            return string.Join("\r\n", lines) + "\r\n";
        }

        // ---------------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------------
        private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();

        private static string SanitiseFileName(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
                sb.Append(Array.IndexOf(_invalidChars, c) >= 0 ? '_' : c);
            return sb.Length > 0 ? sb.ToString() : "part";
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopPartExport");

        public override Guid ComponentGuid => new Guid("b3c9d1e2-f4a5-4b67-8c90-de12345678ab");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Skip(),             // Parts
                DynesticPostProcessor.AutoWire.Spec.FilePath(),         // Folder
                DynesticPostProcessor.AutoWire.Spec.Skip(),             // CabinetData
                DynesticPostProcessor.AutoWire.Spec.Int("1<1<99"),      // CorpusNr
                DynesticPostProcessor.AutoWire.Spec.Panel("7023K_681"), // WZGV
                DynesticPostProcessor.AutoWire.Spec.Float("1<19<200"),  // DZ
                DynesticPostProcessor.AutoWire.Spec.Toggle(),           // Export
            });
        }
    }
}
