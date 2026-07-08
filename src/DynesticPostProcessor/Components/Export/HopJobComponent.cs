using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;

using WallabyHop.Logic;

namespace WallabyHop.Components.Export
{
    /// <summary>
    /// One-stop pre-flight check: collects operation lines, assembles the
    /// full .hop content (same pure code path HopExport uses), runs the
    /// analyzer, and shows a green/red verdict directly on the component
    /// body. NO file is written — wire HopContent into HopExport (or
    /// HopBackplot) once the verdict is OK.
    /// </summary>
    public class HopJobComponent : GH_Component
    {
        public HopJobComponent() : base(
            "HopJob", "HopJob",
            "Pre-flight for a machining job: assembles the complete .hop content from " +
            "operation lines, runs the structural/depth/clamp analyzer, and shows the " +
            "verdict on the component body. Writes NO file — wire HopContent into " +
            "HopExport when the verdict is OK, and into HopBackplot to see the toolpath.",
            "Wallaby Hop", "7 | Export") { }

        public override Guid ComponentGuid => new Guid("095bc845-202b-4834-9698-c057bbfee2b2");

        protected override Bitmap Icon => IconHelper.Load("HopExport");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "Operation lines from any Hop components. Merge multiple sources with a Merge component.",
                GH_ParamAccess.list);

            pManager.AddNumberParameter("DX", "dx",
                "Workpiece dimension X in mm. Default 2440.",
                GH_ParamAccess.item, 2440.0);
            pManager[1].Optional = true;

            pManager.AddNumberParameter("DY", "dy",
                "Workpiece dimension Y in mm. Default 1220.",
                GH_ParamAccess.item, 1220.0);
            pManager[2].Optional = true;

            pManager.AddNumberParameter("DZ", "dz",
                "Material thickness in mm. Default 19. The analyzer's depth-overshoot " +
                "check uses this value.",
                GH_ParamAccess.item, 19.0);
            pManager[3].Optional = true;

            pManager.AddTextParameter("WZGV", "wzgv",
                "Tool preset identifier for the header. Default 7023K_681.",
                GH_ParamAccess.item, "7023K_681");
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("IsValid", "isValid",
                "True when the assembled job has no structural errors or clamp collisions.",
                GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "summary",
                "One-line analyzer verdict.", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "errors",
                "Structural errors + clamp collisions + depth warnings.", GH_ParamAccess.list);
            pManager.AddTextParameter("Stats", "stats",
                "Operation breakdown (drills, chains, tools, path length, est. time).",
                GH_ParamAccess.list);
            pManager.AddTextParameter("HopContent", "hopContent",
                "Assembled .hop content — wire into HopExport to write, HopBackplot to view.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var operationLines = new List<string>();
            double dx = 2440.0, dy = 1220.0, dz = 19.0;
            string wzgv = "7023K_681";

            if (!DA.GetDataList(0, operationLines)) return;
            DA.GetData(1, ref dx);
            DA.GetData(2, ref dy);
            DA.GetData(3, ref dz);
            DA.GetData(4, ref wzgv);

            if (dx <= 0) dx = 2440.0;
            if (dy <= 0) dy = 1220.0;
            if (dz <= 0) dz = 19.0;

            string content = NcExport.AssembleFile(
                "job_preview", dx, dy, dz, wzgv, null, operationLines);

            var a = Logic.HopAnalyzer.Analyze(content);

            // Verdict on the component body — visible at canvas zoom
            if (a.IsValid)
            {
                Message = "OK — " + a.DrillCount + " drills, " + a.SpCount + " chains, "
                    + a.ToolChangeCount + " tools";
            }
            else
            {
                int issues = a.Errors.Count + a.FixchipWarnings.Count;
                Message = issues + " ERROR" + (issues == 1 ? "" : "S");
                foreach (string err in a.Errors)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                foreach (string fc in a.FixchipWarnings)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, fc);
            }
            foreach (string zw in a.ZWarnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, zw);

            var allIssues = new List<string>();
            allIssues.AddRange(a.Errors);
            allIssues.AddRange(a.FixchipWarnings);
            allIssues.AddRange(a.ZWarnings);
            if (allIssues.Count == 0) allIssues.Add("No issues.");

            DA.SetData(0, a.IsValid);
            DA.SetData(1, a.Summary);
            DA.SetDataList(2, allIssues);
            DA.SetDataList(3, a.Stats);
            DA.SetData(4, content);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),                 // OperationLines
                WallabyHop.AutoWire.Spec.Float("0<2440<5000"),   // DX
                WallabyHop.AutoWire.Spec.Float("0<1220<3000"),   // DY
                WallabyHop.AutoWire.Spec.Float("1<19<100"),      // DZ
                WallabyHop.AutoWire.Spec.Skip(),                 // WZGV
            });
        }
    }
}
