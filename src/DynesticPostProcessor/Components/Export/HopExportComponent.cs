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

namespace DynesticPostProcessor.Components.Export
{
    public class HopExportComponent : GH_Component
    {
        public HopExportComponent()
            : base("HopExport", "HopExport",
                "Assembles and writes a complete NC-Hops .hop file for the DYNESTIC CNC. Combines sheet dimensions, tool preset, and operation lines into the standard .hop format.",
                "DYNESTIC", "Export")
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

            // ---------------------------------------------------------------
            // 3. EXPORT GUARD -- silent return when not exporting
            // ---------------------------------------------------------------
            if (!export) return;

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
            // 6. BUILD HEADER -- match Muster_DXF_Import.hop order exactly
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
            // 7. BUILD VARS BLOCK -- 3-space indent, InvariantCulture decimals
            // ---------------------------------------------------------------
            lines.Add("VARS");
            lines.Add("   DX := " + dx.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension X");
            lines.Add("   DY := " + dy.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Y");
            lines.Add("   DZ := " + dz.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Z");

            // ---------------------------------------------------------------
            // 8. BUILD START SECTION -- Fertigteil + HH_Park
            // ---------------------------------------------------------------
            lines.Add("START");
            lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
            lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

            // ---------------------------------------------------------------
            // 9. INSERT OPERATION LINES -- Phase 3+ integration point
            // ---------------------------------------------------------------
            for (int i = 0; i < operationLines.Count; i++)
            {
                lines.Add(operationLines[i]);
            }

            // ---------------------------------------------------------------
            // 10. ASSEMBLE AND WRITE -- CRLF line endings, ASCII encoding
            // ---------------------------------------------------------------
            string content = string.Join("\r\n", lines) + "\r\n";
            File.WriteAllText(fullPath, content, Encoding.ASCII);

            // ---------------------------------------------------------------
            // 11. SUCCESS OUTPUT
            // ---------------------------------------------------------------
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Remark, "Exported: " + fullPath);
            DA.SetData(0, content);
            DA.SetData(1, "Exported: " + fullPath);
        }

        protected override System.Drawing.Bitmap Icon => IconHelper.Load("HopExport");

        public override Guid ComponentGuid => new Guid("4add04a3-cef7-437f-84f5-e4b13f9ceed7");

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.FilePath(),
                DynesticPostProcessor.AutoWire.Spec.Panel("output"),
                DynesticPostProcessor.AutoWire.Spec.Toggle(),
                DynesticPostProcessor.AutoWire.Spec.Float("0<2440<5000"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<1220<5000"),
                DynesticPostProcessor.AutoWire.Spec.Float("0<18<200"),
                DynesticPostProcessor.AutoWire.Spec.Panel(""),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
