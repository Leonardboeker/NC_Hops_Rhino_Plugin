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

namespace WallabyHop.Components.Export
{
    public class HopExportComponent : GH_Component
    {
        private bool _lastExport = false;

        public HopExportComponent()
            : base("HopExport", "HopExport",
                "Assembles and writes a complete NC-Hops .hop file for the DYNESTIC CNC. Combines sheet dimensions, tool preset, and operation lines into the standard .hop format.",
                "Wallaby Hop", "Export")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Folder", "folder",
                "Output directory path. Must exist. Example: D:\\output\\",
                GH_ParamAccess.item);
            pManager.AddTextParameter("FileName", "fileName",
                "Output file name without .hop extension. Extension is added automatically.",
                GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "export",
                "Toggle to trigger file write. False = no file written, no output. Default false.",
                GH_ParamAccess.item, false);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("DX", "dx",
                "Sheet dimension X (width) in mm. Default 800.",
                GH_ParamAccess.item, 800.0);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("DY", "dy",
                "Sheet dimension Y (height) in mm. Default 400.",
                GH_ParamAccess.item, 400.0);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("DZ", "dz",
                "Material thickness in mm. Default 19.",
                GH_ParamAccess.item, 19.0);
            pManager[5].Optional = true;
            pManager.AddTextParameter("WZGV", "wzgv",
                "Tool preset identifier string for the .hop header. Omitted from header if empty. Default: 7023K_681.",
                GH_ParamAccess.item, "7023K_681");
            pManager[6].Optional = true;
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC-Hops macro strings from operation components. Inserted between START section and file end.",
                GH_ParamAccess.list);
            pManager[7].Optional = true;

            pManager.AddTextParameter("LabelVars", "labelVars",
                "VP variable lines from HopLabel component. Injected into the VARS block after DX/DY/DZ " +
                "so the EasyTronic Label printer reads job metadata (order, position, material, etc.).",
                GH_ParamAccess.list);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("HopContent", "hopContent",
                "Generated .hop file content as a string for inspection.",
                GH_ParamAccess.item);
            pManager.AddTextParameter("StatusMsg", "statusMsg",
                "Export status message with file path on success.",
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
            string folder = null;
            if (!DA.GetData(0, ref folder)) return;

            string fileName = null;
            if (!DA.GetData(1, ref fileName)) return;

            bool export = false;
            DA.GetData(2, ref export);

            double dx = 800.0;
            DA.GetData(3, ref dx);

            double dy = 400.0;
            DA.GetData(4, ref dy);

            double dz = 19.0;
            DA.GetData(5, ref dz);

            string wzgv = null;
            DA.GetData(6, ref wzgv);

            List<string> operationLines = new List<string>();
            DA.GetDataList(7, operationLines);

            List<string> labelVars = new List<string>();
            DA.GetDataList(8, labelVars);

            // ---------------------------------------------------------------
            // 3. EXPORT GUARD -- fire only on rising edge (false -> true).
            // On non-edge solves re-emit the cached content so a downstream
            // HopAnalyzer keeps its data instead of starving after one solve.
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
            if (dx <= 0) dx = 800.0;
            if (dy <= 0) dy = 400.0;
            if (dz <= 0) dz = 19.0;
            if (wzgv == null) wzgv = "7023K_681";

            // ---------------------------------------------------------------
            // 5. VALIDATION -- error messages for invalid required inputs
            // ---------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(folder))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error, "folder is empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error, "fileName is empty");
                return;
            }

            string directory = Path.GetFullPath(folder);

            if (!Directory.Exists(directory))
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error, "Directory does not exist: " + directory);
                return;
            }

            // Strip .hop extension if user typed it, then re-add -- always clean
            string stem = fileName.EndsWith(".hop", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;

            string fullPath = Path.Combine(directory, stem + ".hop");

            if (operationLines == null)
            {
                operationLines = new List<string>();
            }

            // ---------------------------------------------------------------
            // 6-9. ASSEMBLE FILE — one tested pure function builds header,
            // VARS, START and the sorted operation lines (golden-file covered)
            // ---------------------------------------------------------------
            string ncName = stem;
            string content = NcExport.AssembleFile(
                ncName, dx, dy, dz, wzgv, labelVars, operationLines);

            // ---------------------------------------------------------------
            // 10. PRE-WRITE VALIDATION GATE
            // The analyzer runs BEFORE the file is written. Structural errors
            // and fixchip collisions block the write (the content is still
            // emitted on the HopContent output for inspection); depth warnings
            // are surfaced but do not block.
            // ---------------------------------------------------------------

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
                    "File NOT written — fix the errors above. ("
                    + analysis.Summary + ")");
                _lastContent = content;
                _lastStatus = "BLOCKED: " + analysis.Summary;
                DA.SetData(0, content);
                DA.SetData(1, _lastStatus);
                return;
            }

            // ---------------------------------------------------------------
            // 11. ATOMIC WRITE -- temp file + move so a locked file or a
            // disk-full mid-write can never leave a truncated .hop behind.
            // CRLF line endings, ASCII encoding.
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
                    "Write failed: " + ex.Message + " (" + fullPath + ")");
                _lastContent = content;
                _lastStatus = "WRITE FAILED: " + ex.Message;
                DA.SetData(0, content);
                DA.SetData(1, _lastStatus);
                return;
            }

            // ---------------------------------------------------------------
            // 12. SUCCESS OUTPUT
            // ---------------------------------------------------------------
            string status = "Exported: " + fullPath + "  [" + analysis.Summary + "]";
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, status);
            _lastContent = content;
            _lastStatus = status;
            DA.SetData(0, content);
            DA.SetData(1, status);
        }

        // Cache so downstream components (HopAnalyzer panels etc.) keep
        // receiving data on solves where the rising-edge guard skips the write.
        private string _lastContent = null;
        private string _lastStatus = null;

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopExport");

        public override Guid ComponentGuid => new Guid("4add04a3-cef7-437f-84f5-e4b13f9ceed7");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.FilePath(),
                WallabyHop.AutoWire.Spec.Panel("output"),
                WallabyHop.AutoWire.Spec.Toggle(),
                WallabyHop.AutoWire.Spec.Float("0<2440<5000"),
                WallabyHop.AutoWire.Spec.Float("0<1220<5000"),
                WallabyHop.AutoWire.Spec.Float("0<18<200"),
                WallabyHop.AutoWire.Spec.Panel(""),
                WallabyHop.AutoWire.Spec.Skip(),  // operationLines
                WallabyHop.AutoWire.Spec.Skip(),  // labelVars — wire HopLabel manually
            });
        }
    }
}
