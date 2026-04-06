using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

using Grasshopper.Kernel;

namespace DynesticPostProcessor.Components.Export
{
    /// <summary>
    /// Validates NC-Hops operationLines for SP/EP structural correctness.
    /// Accepts the raw string list from any HopXxx component so you can catch
    /// errors before writing to disk.
    /// </summary>
    public class HopAnalyzerComponent : GH_Component
    {
        public HopAnalyzerComponent() : base(
            "HopAnalyzer", "HopAnalyzer",
            "Validates NC-Hops operationLines for SP/EP structural correctness.\n\n" +
            "Checks that every SP has a matching EP, and that no G01/G02M/G03M moves " +
            "appear outside an SP/EP block. Wire in the operationLines from HopExport " +
            "or any Hop operation component.",
            "DYNESTIC", "Export") { }

        public override Guid ComponentGuid => new Guid("9e4f1a2b-c3d5-4e6f-8a7b-0c1d2e3f4a5b");

        protected override Bitmap Icon => IconHelper.Load("HopAnalyzer");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("OperationLines", "operationLines",
                "NC lines to validate. Wire from HopExport or any operation component.",
                GH_ParamAccess.list);

            pManager.AddBooleanParameter("Run", "run",
                "Set True to run the analysis. Prevents unnecessary computation.",
                GH_ParamAccess.item, false);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("IsValid", "isValid",
                "True if no structural SP/EP errors were found.", GH_ParamAccess.item);

            pManager.AddIntegerParameter("ErrorCount", "errorCount",
                "Total number of structural errors found.", GH_ParamAccess.item);

            pManager.AddTextParameter("Errors", "errors",
                "List of error messages with line numbers.", GH_ParamAccess.list);

            pManager.AddTextParameter("Summary", "summary",
                "One-line summary: SP count, EP count, errors.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> opLines = new List<string>();
            bool run = false;

            if (!DA.GetDataList(0, opLines)) return;
            DA.GetData(1, ref run);

            if (!run)
            {
                DA.SetData(0, false);
                DA.SetData(1, 0);
                DA.SetDataList(2, new List<string> { "Set Run=true to analyze." });
                DA.SetData(3, "Idle");
                return;
            }

            // ---------------------------------------------------------------
            // ANALYZE
            // ---------------------------------------------------------------
            var spStack   = new Stack<int>();
            var errors    = new List<string>();
            int spCount   = 0;
            int epCount   = 0;
            int moveCount = 0;

            for (int i = 0; i < opLines.Count; i++)
            {
                string s = (opLines[i] ?? "").Trim();
                int lineNum = i + 1;

                if (s.StartsWith("SP "))
                {
                    spStack.Push(lineNum);
                    spCount++;
                }
                else if (s.StartsWith("EP "))
                {
                    epCount++;
                    if (spStack.Count > 0)
                        spStack.Pop();
                    else
                        errors.Add("L" + lineNum + ": EP without preceding SP");
                }
                else if (s.StartsWith("G01 ") || s.StartsWith("G02M ") || s.StartsWith("G03M "))
                {
                    moveCount++;
                    if (spStack.Count == 0)
                        errors.Add("L" + lineNum + ": Move outside SP/EP block: "
                            + s.Substring(0, Math.Min(s.Length, 50)));
                }
            }

            foreach (int openLine in spStack)
                errors.Add("L" + openLine + ": SP never closed (no matching EP)");

            bool isValid = errors.Count == 0;

            string summary = "SP=" + spCount + " EP=" + epCount
                + " Moves=" + moveCount + " Lines=" + opLines.Count;
            if (isValid)
                summary = "OK  " + summary;
            else
                summary = errors.Count + " error(s)  " + summary;

            AddRuntimeMessage(
                isValid ? GH_RuntimeMessageLevel.Remark : GH_RuntimeMessageLevel.Warning,
                summary);

            DA.SetData(0, isValid);
            DA.SetData(1, errors.Count);
            DA.SetDataList(2, isValid ? new List<string> { "No errors." } : errors);
            DA.SetData(3, summary);
        }

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            DynesticPostProcessor.AutoWire.Apply(this, doc, new[]
            {
                DynesticPostProcessor.AutoWire.Spec.Skip(),
                DynesticPostProcessor.AutoWire.Spec.Skip(),
            });
        }
    }
}
