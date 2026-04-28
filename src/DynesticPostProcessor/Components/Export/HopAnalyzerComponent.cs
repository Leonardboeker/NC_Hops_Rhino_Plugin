using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;

using WallabyHop.Logic;

namespace WallabyHop.Components.Export
{
    /// <summary>
    /// Validates assembled .hop content for SP/EP structural correctness
    /// and Z-depth safety. Wraps the pure HopAnalyzer logic and routes
    /// its results into Grasshopper outputs + runtime messages.
    /// </summary>
    public class HopAnalyzerComponent : GH_Component
    {
        public HopAnalyzerComponent() : base(
            "HopAnalyzer", "HopAnalyzer",
            "Validates the final .hop file content for structural correctness.\n\n" +
            "Checks: SP/EP pairing, moves outside SP/EP blocks, duplicate tool calls, " +
            "and negative Z values (drill cutZ, SP plunge Z below machine table). " +
            "Wire in HopContent from HopExport so the check runs on the fully assembled output.",
            "Wallaby Hop", "Export") { }

        public override Guid ComponentGuid => new Guid("9e4f1a2b-c3d5-4e6f-8a7b-0c1d2e3f4a5b");

        protected override Bitmap Icon => IconHelper.Load("HopAnalyzer");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("HopContent", "hopContent",
                "Full .hop file content string. Wire from HopExport's HopContent output.",
                GH_ParamAccess.item);

            pManager.AddBooleanParameter("Run", "run",
                "Set True to run the analysis. Prevents unnecessary computation.",
                GH_ParamAccess.item, false);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("IsValid", "isValid",
                "True if no structural or Z-depth errors were found.", GH_ParamAccess.item);

            pManager.AddIntegerParameter("ErrorCount", "errorCount",
                "Total number of structural errors found.", GH_ParamAccess.item);

            pManager.AddTextParameter("Errors", "errors",
                "List of error messages with line numbers.", GH_ParamAccess.list);

            pManager.AddTextParameter("Summary", "summary",
                "One-line summary: SP count, EP count, errors.", GH_ParamAccess.item);

            pManager.AddTextParameter("Stats", "stats",
                "Operation breakdown: drill count, contour count, tool changes, estimated time.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string hopContent = null;
            bool run = false;

            if (!DA.GetData(0, ref hopContent)) return;
            DA.GetData(1, ref run);

            if (!run)
            {
                DA.SetData(0, false);
                DA.SetData(1, 0);
                DA.SetDataList(2, new List<string> { "Set Run=true to analyze." });
                DA.SetData(3, "Idle");
                return;
            }

            // Delegate the entire analysis to pure HopAnalyzer logic
            var result = HopAnalyzer.Analyze(hopContent);

            AddRuntimeMessage(
                result.IsValid ? GH_RuntimeMessageLevel.Remark : GH_RuntimeMessageLevel.Warning,
                result.Summary);
            foreach (string zw in result.ZWarnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, zw);

            var allMessages = new List<string>();
            if (result.Errors.Count > 0) allMessages.AddRange(result.Errors);
            if (result.ZWarnings.Count > 0) allMessages.AddRange(result.ZWarnings);
            if (allMessages.Count == 0) allMessages.Add("No errors.");

            DA.SetData(0, result.IsValid);
            DA.SetData(1, result.ErrorCount);
            DA.SetDataList(2, allMessages);
            DA.SetData(3, result.Summary);
            DA.SetDataList(4, result.Stats);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(),
                WallabyHop.AutoWire.Spec.Skip(),
            });
        }
    }
}
