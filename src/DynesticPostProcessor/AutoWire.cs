using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;

namespace DynesticPostProcessor
{
    /// <summary>
    /// Creates and wires default input sources when a component is first
    /// dropped onto the GH canvas (via AddedToDocument override).
    /// </summary>
    internal static class AutoWire
    {
        public enum Kind { Skip, Curve, Point, Brep, Float, Int, Panel, Toggle, FilePath, ValueList }

        public readonly struct Spec
        {
            public readonly Kind   Kind;
            public readonly string Code;   // slider: "min<default<max"
            public readonly string Text;   // panel default text
            public readonly (string name, string value)[] Items;  // for ValueList

            Spec(Kind k, string code = null, string text = null, (string name, string value)[] items = null)
            {
                Kind = k; Code = code; Text = text; Items = items;
            }

            public static Spec Skip()              => new Spec(Kind.Skip);
            public static Spec Curve()             => new Spec(Kind.Curve);
            public static Spec Point()             => new Spec(Kind.Point);
            public static Spec Brep()              => new Spec(Kind.Brep);
            public static Spec Float(string code)  => new Spec(Kind.Float, code);
            public static Spec Int(string code)    => new Spec(Kind.Int,   code);
            public static Spec Panel(string text = "") => new Spec(Kind.Panel, text: text);
            public static Spec Toggle()            => new Spec(Kind.Toggle);
            public static Spec FilePath()          => new Spec(Kind.FilePath);
            public static Spec ValueList(params (string name, string value)[] items)
                => new Spec(Kind.ValueList, items: items);
        }

        /// <summary>
        /// Call from AddedToDocument. Skips if any input is already wired
        /// (prevents double-wiring on copy/paste or reloaded definitions).
        ///
        /// Layout guarantees:
        ///  - Each source Y is pinned to its input grip's actual Y.
        ///  - All sources are RIGHT-aligned: their output connectors sit at
        ///    the same X column, 40 px to the left of the component's left edge.
        ///  - Bounds are measured BEFORE doc.AddObject so GH cannot move the
        ///    pivot during insertion (avoids Param_Curve/Point going off-canvas).
        /// </summary>
        public static void Apply(GH_Component comp, GH_Document doc, Spec[] specs)
        {
            foreach (var p in comp.Params.Input)
                if (p.SourceCount > 0) return;

            // Ensure component input pivots are valid
            comp.Attributes.PerformLayout();

            // All sources' RIGHT edges land here (40 px gap from component left)
            float rightEdge = comp.Attributes.Bounds.Left - 40f;
            int n = Math.Min(specs.Length, comp.Params.Input.Count);

            for (int i = 0; i < n; i++)
            {
                if (specs[i].Kind == Kind.Skip) continue;

                var src = Build(specs[i]);
                if (src == null) continue;

                float inputY = comp.Params.Input[i].Attributes.Pivot.Y;

                // Measure width BEFORE adding to doc (doc.AddObject can reset pivot)
                src.CreateAttributes();
                src.Attributes.Pivot = new PointF(rightEdge - 100f, inputY);
                src.Attributes.PerformLayout();

                float pivotToRight = src.Attributes.Bounds.Right - src.Attributes.Pivot.X;

                // Clamp: prevents off-canvas placement when layout returns garbage
                // for standalone Param_Curve/Point/Brep (typically ~12 px wide)
                if (pivotToRight < 0f || pivotToRight > 250f)
                    pivotToRight = 12f;

                src.Attributes.Pivot = new PointF(rightEdge - pivotToRight, inputY);
                doc.AddObject((IGH_ActiveObject)src, false);
                comp.Params.Input[i].AddSource(src);
            }
        }

        static IGH_Param Build(Spec s)
        {
            switch (s.Kind)
            {
                case Kind.Curve:    return (IGH_Param)new Param_Curve();
                case Kind.Point:    return (IGH_Param)new Param_Point();
                case Kind.Brep:     return (IGH_Param)new Param_Brep();
                case Kind.FilePath: return (IGH_Param)new Param_FilePath();

                case Kind.Float:
                case Kind.Int:
                {
                    var sl = new GH_NumberSlider();
                    sl.CreateAttributes();
                    sl.SetInitCode(s.Code);
                    return (IGH_Param)sl;
                }

                case Kind.Panel:
                    return null;   // skipped — user fills text inputs manually

                case Kind.Toggle:
                {
                    var t = new GH_BooleanToggle();
                    t.CreateAttributes();
                    t.Value = false;
                    return (IGH_Param)t;
                }

                case Kind.ValueList:
                {
                    var vl = new GH_ValueList();
                    vl.CreateAttributes();
                    vl.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown;
                    vl.ListItems.Clear();
                    if (s.Items != null)
                    {
                        foreach (var item in s.Items)
                            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(item.name, item.value));
                    }
                    if (vl.ListItems.Count > 0)
                        vl.SelectItem(0);
                    return (IGH_Param)vl;
                }

                default: return null;
            }
        }
    }
}
