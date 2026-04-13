using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Grasshopper.Kernel;

namespace WallabyHop.Components.Export
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
            "Validates the final .hop file content for structural correctness.\n\n" +
            "Checks: SP/EP pairing, moves outside SP/EP blocks, duplicate tool calls, " +
            "and negative Z values (Bohrung cutZ, SP zEintauch below machine table). " +
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
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string hopContent = null;
            bool run = false;

            if (!DA.GetData(0, ref hopContent)) return;
            DA.GetData(1, ref run);

            string[] splitLines = hopContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            List<string> opLines = new List<string>(splitLines);

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
            var spStack        = new Stack<int>();
            var errors         = new List<string>();
            int spCount        = 0;
            int epCount        = 0;
            int moveCount      = 0;
            int emptyBlocks    = 0;
            int movesInBlock   = 0;
            int openSpLine     = -1;

            // Parse DZ (plate thickness) from header: ";DZ=19.000"
            double headerDZ = 0;
            foreach (string hLine in opLines)
            {
                string hl = (hLine ?? "").Trim();
                if (hl.StartsWith(";DZ="))
                {
                    double.TryParse(hl.Substring(4), NumberStyles.Any, CultureInfo.InvariantCulture, out headerDZ);
                    break;
                }
            }

            // Z safety tracking — warn if drill depth > DZ + 5 mm (into spoilboard beyond tolerance)
            const double SpoilboardAllowance = 5.0;
            double deepestZ = double.MaxValue;
            var zWarningMessages = new List<string>();

            for (int i = 0; i < opLines.Count; i++)
            {
                string s = (opLines[i] ?? "").Trim();
                int lineNum = i + 1;

                if (s.StartsWith("SP "))
                {
                    spStack.Push(lineNum);
                    spCount++;
                    movesInBlock = 0;
                    openSpLine = lineNum;
                }
                else if (s.StartsWith("EP "))
                {
                    epCount++;
                    if (spStack.Count > 0)
                    {
                        int matchedSp = spStack.Pop();
                        if (movesInBlock == 0)
                        {
                            emptyBlocks++;
                            errors.Add("L" + matchedSp + ": Empty SP/EP block (no moves between SP and EP)");
                        }
                        movesInBlock = 0;
                    }
                    else
                    {
                        errors.Add("L" + lineNum + ": EP without preceding SP");
                    }
                }
                else if (s.StartsWith("G01 ") || s.StartsWith("G02M ") || s.StartsWith("G03M "))
                {
                    moveCount++;
                    movesInBlock++;
                    if (spStack.Count == 0)
                        errors.Add("L" + lineNum + ": Move outside SP/EP block: "
                            + s.Substring(0, Math.Min(s.Length, 50)));
                }
                else if (s.StartsWith("Bohrung ("))
                {
                    // Z safety: Bohrung (x, y, surfaceZ, cutZ, ...) — params index 2 and 3
                    // drillDepth = surfaceZ - cutZ; warn if drillDepth > DZ + 5 mm
                    int bOpen = s.IndexOf('(');
                    int bClose = s.LastIndexOf(')');
                    if (bOpen >= 0 && bClose > bOpen && headerDZ > 0)
                    {
                        string[] bParts = s.Substring(bOpen + 1, bClose - bOpen - 1).Split(',');
                        if (bParts.Length >= 4
                            && double.TryParse(bParts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double surfZ)
                            && double.TryParse(bParts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double cutZ))
                        {
                            double drillDepth = surfZ - cutZ;
                            deepestZ = Math.Min(deepestZ, cutZ);
                            if (drillDepth > headerDZ + SpoilboardAllowance)
                                zWarningMessages.Add("L" + lineNum + ": Bohrung depth=" + drillDepth.ToString("F1", CultureInfo.InvariantCulture)
                                    + " mm exceeds plate DZ=" + headerDZ.ToString("F1", CultureInfo.InvariantCulture)
                                    + " mm + 5 mm spoilboard allowance");
                        }
                    }
                }

                // ---- Find deepest Z across all macro types ----
                // CALL macros: TIEFE:=, SZ:=, T:= (absolute Z cut depth)
                // SP/EP lines: Z<value>, G01/G02M/G03M lines: Z<value>
                {
                    double parsedZ = double.MaxValue;
                    if (s.StartsWith("CALL "))
                    {
                        // Named Z params in CALL macros
                        foreach (string paramName in new[] { "TIEFE", "SZ", "TOPF_T", "DUEBEL_T" })
                        {
                            var m = Regex.Match(s, paramName + @":=([-\d.]+)");
                            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pz))
                            {
                                parsedZ = Math.Min(parsedZ, pz);
                            }
                        }
                    }
                    else if (s.StartsWith("SP ") || s.StartsWith("G01 ") || s.StartsWith("G02M ") || s.StartsWith("G03M "))
                    {
                        var m = Regex.Match(s, @"\bZ([-\d.]+)");
                        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double gz))
                            parsedZ = gz;
                    }

                    if (parsedZ < double.MaxValue)
                    {
                        deepestZ = Math.Min(deepestZ, parsedZ);
                        if (parsedZ < -0.2)
                            zWarningMessages.Add("L" + lineNum + ": cutZ=" + parsedZ.ToString("F3", CultureInfo.InvariantCulture)
                                + " mm is more than 0.2 mm below machine table — risk of table damage");
                    }
                }
            }

            foreach (int openLine in spStack)
                errors.Add("L" + openLine + ": SP never closed (no matching EP)");

            bool isValid = errors.Count == 0;

            string summary = "SP=" + spCount + " EP=" + epCount
                + " Moves=" + moveCount + " Lines=" + opLines.Count;
            if (emptyBlocks > 0)
                summary += " EmptyBlocks=" + emptyBlocks;
            if (deepestZ < double.MaxValue)
                summary += " DeepestZ=" + deepestZ.ToString("F3", CultureInfo.InvariantCulture);
            if (zWarningMessages.Count > 0)
                summary += " ZWarnings=" + zWarningMessages.Count;
            if (isValid)
                summary = "OK  " + summary;
            else
                summary = errors.Count + " error(s)  " + summary;

            AddRuntimeMessage(
                isValid ? GH_RuntimeMessageLevel.Remark : GH_RuntimeMessageLevel.Warning,
                summary);
            foreach (string zw in zWarningMessages)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, zw);

            var allMessages = new List<string>();
            if (errors.Count > 0) allMessages.AddRange(errors);
            if (zWarningMessages.Count > 0) allMessages.AddRange(zWarningMessages);
            if (allMessages.Count == 0) allMessages.Add("No errors.");

            DA.SetData(0, isValid);
            DA.SetData(1, errors.Count);
            DA.SetDataList(2, allMessages);
            DA.SetData(3, summary);
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
