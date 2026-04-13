using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace WallabyHop.Components.Utility
{
    public class HopLayerScanComponent : GH_Component, IGH_VariableParameterComponent
    {
        // ---------------------------------------------------------------
        // OPERATION TYPE TABLE
        // ---------------------------------------------------------------
        private static readonly (string OpName, Color LayerColor, bool IsDrill)[] _opTypes =
        {
            ("HopContour",    Color.Yellow,       false),
            ("HopDrill",      Color.Red,          true),
            ("HopSaw",        Color.DeepSkyBlue,  false),
            ("HopRectPocket", Color.Cyan,         false),
            ("HopEngraving",  Color.MediumPurple, false),
            ("HopCircPath",   Color.LimeGreen,    false),
            ("HopFreeSlot",   Color.Orange,       false),
            ("HopCircPocket", Color.Cyan,         false),
        };

        public HopLayerScanComponent() : base(
            "HopLayerScan", "HopLayerScan",
            "Scans the DYNESTIC layer tree for geometry and outputs one list per occupied sub-layer. Drop on canvas to auto-create the DYNESTIC layer structure in Rhino.",
            "Wallaby Hop", "Utility") { }

        public override Guid ComponentGuid => new Guid("b3f1a042-9c7e-4d85-a631-2f80e5c6d917");

        protected override Bitmap Icon => IconHelper.Load("HopLayerScan");

        // ---------------------------------------------------------------
        // REGISTER PARAMS
        // ---------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Toggle", "toggle",
                "Set to True to scan DYNESTIC sub-layers for geometry. Each occupied sub-layer becomes a dynamic output.",
                GH_ParamAccess.item, false);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // No static outputs — all outputs are dynamic via IGH_VariableParameterComponent
        }

        // ---------------------------------------------------------------
        // IGH_VariableParameterComponent — allow outputs only; disallow user UI modifications
        // ---------------------------------------------------------------
        public bool CanInsertParameter(GH_ParameterSide side, int index) => false;
        public bool CanRemoveParameter(GH_ParameterSide side, int index) => false;
        public IGH_Param CreateParameter(GH_ParameterSide side, int index) => null;
        public bool DestroyParameter(GH_ParameterSide side, int index) => false;
        public void VariableParameterMaintenance() { }

        // ---------------------------------------------------------------
        // AddedToDocument — AutoWire + EnsureLayerTree
        // ---------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.Toggle(),
            });
            EnsureLayerTree();
        }

        // ---------------------------------------------------------------
        // EnsureLayerTree — create DYNESTIC root + 8 op layers + 3 sub-layers each
        // ---------------------------------------------------------------
        private void EnsureLayerTree()
        {
            RhinoDoc rhinoDoc = RhinoDoc.ActiveDoc;
            if (rhinoDoc == null) return;

            // 1. Ensure root "Wallaby Hop"
            int rootIdx = rhinoDoc.Layers.FindByFullPath("Wallaby Hop", RhinoMath.UnsetIntIndex);
            if (rootIdx == RhinoMath.UnsetIntIndex)
            {
                var root = new Rhino.DocObjects.Layer { Name = "Wallaby Hop" };
                rootIdx = rhinoDoc.Layers.Add(root);
            }
            Guid rootId = rhinoDoc.Layers[rootIdx].Id;

            // 2. Each operation type
            foreach (var op in _opTypes)
            {
                string opPath = "DYNESTIC::" + op.OpName;
                int opIdx = rhinoDoc.Layers.FindByFullPath(opPath, RhinoMath.UnsetIntIndex);
                if (opIdx == RhinoMath.UnsetIntIndex)
                {
                    var opLayer = new Rhino.DocObjects.Layer
                    {
                        Name          = op.OpName,
                        ParentLayerId = rootId,
                        Color         = op.LayerColor,
                    };
                    opIdx = rhinoDoc.Layers.Add(opLayer);
                }
                Guid opId = rhinoDoc.Layers[opIdx].Id;

                // 3. Three default sub-layers
                for (int s = 1; s <= 3; s++)
                {
                    string subName = op.OpName + "_" + s;
                    string subPath = opPath + "::" + subName;
                    int subIdx = rhinoDoc.Layers.FindByFullPath(subPath, RhinoMath.UnsetIntIndex);
                    if (subIdx == RhinoMath.UnsetIntIndex)
                    {
                        var subLayer = new Rhino.DocObjects.Layer
                        {
                            Name          = subName,
                            ParentLayerId = opId,
                            Color         = op.LayerColor,
                        };
                        rhinoDoc.Layers.Add(subLayer);
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // SolveInstance
        // ---------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read toggle input
            bool toggle = false;
            DA.GetData(0, ref toggle);

            // 2. If Toggle=False — remove all dynamic output params if any, return
            if (!toggle)
            {
                var needed = new List<(string Name, bool IsDrill)>();
                if (!ParamsMatchNeeded(needed))
                    RebuildOutputParams(needed);
                return;
            }

            // 3. Scan layers to find occupied sub-layers
            var occupied = ScanLayers();

            // 4. Build needed param descriptors (name + type) in operation-type order
            var neededParams = occupied.Select(o => (o.SubLayerName, o.IsDrill)).ToList();

            // 5. If current params differ from needed — schedule rebuild, return early
            if (!ParamsMatchNeeded(neededParams))
            {
                OnPingDocument().ScheduleSolution(1, d =>
                {
                    // Remove all existing output params
                    while (Params.Output.Count > 0)
                        Params.UnregisterOutputParameter(Params.Output[0]);

                    // Add new params in order
                    foreach (var (name, isDrill) in neededParams)
                    {
                        IGH_Param p;
                        if (isDrill)
                        {
                            var pp = new Param_Point();
                            pp.Name        = name;
                            pp.NickName    = name;
                            pp.Description = "Point3d list from layer " + name;
                            pp.Access      = GH_ParamAccess.list;
                            p = pp;
                        }
                        else
                        {
                            var pc = new Param_Curve();
                            pc.Name        = name;
                            pc.NickName    = name;
                            pc.Description = "Curve list from layer " + name;
                            pc.Access      = GH_ParamAccess.list;
                            p = pc;
                        }
                        Params.RegisterOutputParam(p);
                    }
                    Params.OnParametersChanged();
                    ExpireSolution(false);
                });
                return; // first solve exits here; re-solve with correct params follows
            }

            // 6. Params are correct — set output data and emit remark
            var remarkParts = new List<string>();
            for (int i = 0; i < occupied.Count; i++)
            {
                var entry = occupied[i];
                if (entry.IsDrill)
                {
                    var pts = entry.Geometry.OfType<Point3d>().ToList();
                    DA.SetDataList(i, pts);
                    remarkParts.Add(entry.SubLayerName + ": " + pts.Count + " pts");
                }
                else
                {
                    var crvs = entry.Geometry.OfType<Curve>().ToList();
                    DA.SetDataList(i, crvs);
                    remarkParts.Add(entry.SubLayerName + ": " + crvs.Count + " curves");
                }
            }

            if (remarkParts.Count > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    string.Join(", ", remarkParts));
        }

        // ---------------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------------

        private bool ParamsMatchNeeded(List<(string Name, bool IsDrill)> needed)
        {
            if (Params.Output.Count != needed.Count) return false;
            for (int i = 0; i < needed.Count; i++)
            {
                var p = Params.Output[i];
                if (p.Name != needed[i].Name) return false;
                bool currentIsDrill = p is Param_Point;
                if (currentIsDrill != needed[i].IsDrill) return false;
            }
            return true;
        }

        private void RebuildOutputParams(List<(string Name, bool IsDrill)> needed)
        {
            OnPingDocument().ScheduleSolution(1, d =>
            {
                while (Params.Output.Count > 0)
                    Params.UnregisterOutputParameter(Params.Output[0]);
                Params.OnParametersChanged();
                ExpireSolution(false);
            });
        }

        private List<(string SubLayerName, bool IsDrill, IEnumerable<object> Geometry)> ScanLayers()
        {
            var result = new List<(string, bool, IEnumerable<object>)>();
            RhinoDoc rhinoDoc = RhinoDoc.ActiveDoc;
            if (rhinoDoc == null) return result;

            foreach (var op in _opTypes)
            {
                string opPath = "DYNESTIC::" + op.OpName;
                int opIdx = rhinoDoc.Layers.FindByFullPath(opPath, RhinoMath.UnsetIntIndex);
                if (opIdx == RhinoMath.UnsetIntIndex) continue;

                Guid opId = rhinoDoc.Layers[opIdx].Id;

                // Collect all sub-layers (direct children of this op layer), sorted by name
                var subLayers = rhinoDoc.Layers
                    .Where(l => l.ParentLayerId == opId && !l.IsDeleted)
                    .OrderBy(l => l.Name)
                    .ToList();

                foreach (var sub in subLayers)
                {
                    RhinoObject[] objs = rhinoDoc.Objects.FindByLayer(sub);
                    if (objs == null || objs.Length == 0) continue; // skip empty

                    var geometries = new List<object>();
                    foreach (var obj in objs)
                    {
                        GeometryBase geom = obj.Geometry;
                        if (op.IsDrill)
                        {
                            Rhino.Geometry.Point pt = geom as Rhino.Geometry.Point;
                            if (pt != null) geometries.Add(pt.Location);
                        }
                        else
                        {
                            Curve crv = geom as Curve;
                            if (crv != null) geometries.Add(crv);
                        }
                    }

                    if (geometries.Count > 0)
                        result.Add((sub.Name, op.IsDrill, geometries));
                }
            }
            return result;
        }
    }
}
