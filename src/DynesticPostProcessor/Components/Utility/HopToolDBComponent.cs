using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace WallabyHop.Components.Utility
{
    /// <summary>
    /// Reads a NC-HOPS .too tool database file and provides a Value List for
    /// tool selection. Outputs the EdgeID (tool number), diameter, feedrate and
    /// name of the selected tool — ready to wire into HopContour, HopDrill, etc.
    /// </summary>
    public class HopToolDBComponent : GH_Component
    {
        // Last parsed state
        private List<ToolEntry> _tools   = new List<ToolEntry>();
        private string          _lastPath = null;

        public HopToolDBComponent() : base(
            "HopToolDB", "HopToolDB",
            "Reads the NC-HOPS .too tool database and provides a drop-down for tool selection. " +
            "Outputs tool number, diameter, and feedrate to wire into operation components.",
            "Wallaby Hop", "Utility") { }

        public override Guid ComponentGuid =>
            new Guid("c4e2f851-7b3d-4a9c-b602-3e91f7d8a043");

        protected override Bitmap Icon => IconHelper.Load("HopToolDB");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // ---------------------------------------------------------------
        // PARAMS
        // ---------------------------------------------------------------

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ToolFile", "toolFile",
                "Full path to the NC-HOPS .too tool database file (e.g. C:\\HOPS\\werkzeug.too).",
                GH_ParamAccess.item, "");
            pManager[0].Optional = true;

            pManager.AddIntegerParameter("ToolID", "toolId",
                "Tool EdgeID to look up. A Value List is auto-wired on canvas drop.",
                GH_ParamAccess.item, 10);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("ToolNr", "toolNr",
                "Tool number (EdgeID) — wire into toolNr of HopContour, HopDrill, HopSaw, etc.",
                GH_ParamAccess.item);

            pManager.AddNumberParameter("Diameter", "diameter",
                "Cutting diameter in mm.", GH_ParamAccess.item);

            pManager.AddNumberParameter("Feedrate", "feedrate",
                "Feedrate in mm/min.", GH_ParamAccess.item);

            pManager.AddTextParameter("Name", "name",
                "Tool name from the database.", GH_ParamAccess.item);
        }

        // ---------------------------------------------------------------
        // SOLVE
        // ---------------------------------------------------------------

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = @"D:\Projekte\SynologyDrive\53_post-processor\reference-hops\werkzeug klemp.too";
            DA.GetData(0, ref filePath);
            if (string.IsNullOrWhiteSpace(filePath))
                filePath = @"F:\werkzeug klemp.too";

            // Re-parse when path changes
            if (filePath != _lastPath)
            {
                _tools    = ParseTooFile(filePath);
                _lastPath = filePath;

                // Update the connected ValueList, then re-solve
                var vl = FindConnectedValueList();
                if (vl != null)
                {
                    OnPingDocument().ScheduleSolution(1, d =>
                    {
                        PopulateValueList(vl, _tools);
                        vl.ExpireSolution(false);
                    });
                    return;
                }
            }

            if (_tools.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "No tools loaded — check file path: " + filePath);
                return;
            }

            int selectedId = 10;
            DA.GetData(1, ref selectedId);

            ToolEntry tool = _tools.FirstOrDefault(t => t.EdgeID == selectedId);
            if (tool == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Tool ID " + selectedId + " not found in database.");
                return;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "ID " + tool.EdgeID + "  " + tool.Name +
                "  Ø" + tool.Diameter.ToString("F1", CultureInfo.InvariantCulture) + " mm" +
                "  " + tool.Feedrate + " mm/min");

            DA.SetData(0, tool.EdgeID);
            DA.SetData(1, tool.Diameter);
            DA.SetData(2, tool.Feedrate);
            DA.SetData(3, tool.Name);
        }

        // ---------------------------------------------------------------
        // ADDED TO DOCUMENT
        // ---------------------------------------------------------------

        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Skip(), // toolFile
                WallabyHop.AutoWire.Spec.Skip(), // toolId — we create the ValueList
            });

            // Parse with default path
            string defaultPath = @"D:\Projekte\SynologyDrive\53_post-processor\reference-hops\werkzeug klemp.too";
            _tools    = ParseTooFile(defaultPath);
            _lastPath = defaultPath;

            // Wire a GH_ValueList to input 1
            var vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new PointF(
                Attributes.Pivot.X - 220,
                Attributes.Pivot.Y + 28);

            PopulateValueList(vl, _tools);
            doc.AddObject(vl, false);
            Params.Input[1].AddSource(vl);
        }

        // ---------------------------------------------------------------
        // VALUE LIST HELPERS
        // ---------------------------------------------------------------

        private void PopulateValueList(GH_ValueList vl, List<ToolEntry> tools)
        {
            vl.ListItems.Clear();
            vl.Name     = "Tool Selection";
            vl.NickName = "Tool";

            foreach (ToolEntry t in tools.Where(t => t.EdgeID > 0).OrderBy(t => t.EdgeID))
            {
                string label = "ID " + t.EdgeID.ToString().PadLeft(3) +
                               "  " + t.Name +
                               "  Ø" + t.Diameter.ToString("F1", CultureInfo.InvariantCulture) + " mm";
                vl.ListItems.Add(new GH_ValueListItem(label, t.EdgeID.ToString()));
            }

            if (vl.ListItems.Count > 0)
                vl.ListItems[0].Selected = true;
        }

        private GH_ValueList FindConnectedValueList()
        {
            foreach (IGH_Param source in Params.Input[1].Sources)
                if (source is GH_ValueList vl) return vl;
            return null;
        }

        // ---------------------------------------------------------------
        // PARSER
        // ---------------------------------------------------------------

        private List<ToolEntry> ParseTooFile(string path)
        {
            var tools = new List<ToolEntry>();
            if (!File.Exists(path)) return tools;

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.Unicode);
                ToolEntry current = null;
                bool inMain = false, inEdge = false;

                foreach (string raw in lines)
                {
                    string line = raw.Trim();

                    if (Regex.IsMatch(line, @"^\[ToolData\d+\]$"))
                    {
                        if (current != null) tools.Add(current);
                        current = new ToolEntry();
                        inMain = true; inEdge = false;
                    }
                    else if (Regex.IsMatch(line, @"^\[ToolData\d+CuttingEdge0\]$"))
                    {
                        inMain = false; inEdge = true;
                    }
                    else if (line.StartsWith("["))
                    {
                        inMain = false; inEdge = false;
                    }

                    if (current == null) continue;

                    if (inMain && TryVal(line, "Name", out string name))
                        current.Name = name;

                    if (inEdge)
                    {
                        if (TryVal(line, "ID", out string idStr) &&
                            int.TryParse(idStr, out int eid))
                            current.EdgeID = eid;

                        if (TryVal(line, "NominalRadius", out string radStr))
                        {
                            if (double.TryParse(radStr.Replace(',', '.'),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out double r))
                                current.Diameter = r * 2.0;
                        }

                        if (TryVal(line, "Feedrate", out string frStr) &&
                            double.TryParse(frStr, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double fr))
                            current.Feedrate = fr;

                        if (TryVal(line, "RotSpeed", out string rsStr) &&
                            double.TryParse(rsStr, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double rs))
                            current.RotSpeed = rs;
                    }
                }
                if (current != null) tools.Add(current);
            }
            catch { /* file locked or inaccessible */ }

            return tools;
        }

        private static bool TryVal(string line, string key, out string value)
        {
            value = null;
            string prefix = key + "=";
            if (!line.StartsWith(prefix)) return false;
            value = line.Substring(prefix.Length);
            return true;
        }

        // ---------------------------------------------------------------
        // DATA CLASS
        // ---------------------------------------------------------------

        private class ToolEntry
        {
            public string Name     = "";
            public int    EdgeID   = -1;
            public double Diameter = 0;
            public double Feedrate = 0;
            public double RotSpeed = 0;
        }
    }
}
