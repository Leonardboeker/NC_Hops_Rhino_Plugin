using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Korpus
{
    public class HopCabinetDoorComponent : GH_Component
    {
        public HopCabinetDoorComponent()
            : base("HopCabinetDoor", "HopCabinetDoor",
                "Cabinet door with hinge (Topfband) hole layout. Computes door dimensions from overlay type and generates hinge cup holes. Wire into HopKorpus 'door' input.",
                "Wallaby Hop", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Count", "count",
                "Number of doors. 1 = single door. 2 = pair. Default 1.",
                GH_ParamAccess.item, 1);
            pManager[0].Optional = true;

            pManager.AddIntegerParameter("Overlay", "overlay",
                "Door overlay type. 0=FullOverlay (Vollanschlag: door covers full cabinet front, width = innerW + 2*MS - gap), "
                + "1=HalfOverlay (Halbeinschlag: used for adjacent cabinets, width = innerW/2 + MS/2), "
                + "2=Inset (Einliegend: door sits inside opening, width = innerW - 2mm gap). Default 0.",
                GH_ParamAccess.item, 0);
            pManager[1].Optional = true;

            pManager.AddIntegerParameter("HingeType", "hinge",
                "Hinge type. 0=Blum ClipTop, 1=Haefele Duomatic. Both use identical 35mm Topf dimensions. Default 0.",
                GH_ParamAccess.item, 0);
            pManager[2].Optional = true;

            pManager.AddIntegerParameter("HingeSide", "side",
                "Hinge side. 0=Left (hinge on left edge), 1=Right (hinge on right edge), "
                + "2=Center (for 2-door: left door left-hinged, right door right-hinged). Default 0.",
                GH_ParamAccess.item, 0);
            pManager[3].Optional = true;

            pManager.AddNumberParameter("Gap", "gap",
                "Gap between door edge and cabinet edge in mm. Default 2.",
                GH_ParamAccess.item, 2.0);
            pManager[4].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("DoorData", "door",
                "Door configuration with hinge layout. Wire into HopKorpus 'door' input.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int doorCount = 1;
            int overlay = 0;
            int hingeType = 0;
            int hingeSide = 0;
            double gap = 2.0;

            DA.GetData(0, ref doorCount);
            DA.GetData(1, ref overlay);
            DA.GetData(2, ref hingeType);
            DA.GetData(3, ref hingeSide);
            DA.GetData(4, ref gap);

            // Guards
            if (doorCount < 1 || doorCount > 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Count must be 1 or 2.");
                return;
            }
            if (overlay < 0 || overlay > 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Overlay must be 0 (FullOverlay), 1 (HalfOverlay), or 2 (Inset).");
                return;
            }
            if (hingeType < 0 || hingeType > 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HingeType must be 0 (BlumClipTop) or 1 (HaefeleDuomatic).");
                return;
            }
            if (hingeSide < 0 || hingeSide > 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "HingeSide must be 0 (Left), 1 (Right), or 2 (Center).");
                return;
            }
            if (gap < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Gap must be >= 0.");
                return;
            }

            // Name lookup tables
            string[] hingeNames = { "BlumClipTop", "HaefeleDuomatic" };
            string[] overlayNames = { "FullOverlay", "HalfOverlay", "Inset" };
            string[] sideNames = { "Left", "Right", "Center" };

            // Build output dictionary
            var dict = new Dictionary<string, object>();
            dict["count"]         = doorCount;
            dict["overlay"]       = overlay;
            dict["overlayName"]   = overlayNames[overlay];
            dict["hingeType"]     = hingeType;
            dict["hingeTypeName"] = hingeNames[hingeType];
            dict["hingeSide"]     = hingeSide;
            dict["hingeSideName"] = sideNames[hingeSide];
            dict["gap"]           = gap;

            // Hinge cup dimensions (same for Blum and Haefele)
            dict["hinge_cupDia"]      = 35.0;    // Topf diameter
            dict["hinge_cupDepth"]    = 13.5;    // drill depth
            dict["hinge_edgeDist"]    = 22.5;    // cup centre from door short edge
            dict["hinge_s32Pos"]      = 128.0;   // first hinge from top/bottom (4x System-32)
            dict["hinge_s32_raster"]  = 32.0;    // System-32 grid for additional hinges

            // Output
            DA.SetData(0, new GH_ObjectWrapper(dict));

            // Remark
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopCabinetDoor: " + doorCount + "x " + overlayNames[overlay]
                + ", " + hingeNames[hingeType] + " " + sideNames[hingeSide]);
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopCabinetDoor");

        public override Guid ComponentGuid =>
            new Guid("f6a7b8c9-d0e1-2345-f012-345678901234");

        // -----------------------------------------------------------------
        // AUTO-WIRE (5 inputs)
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.ValueList(("1 T\u00fcr", "1"), ("2 T\u00fcren", "2")),        // Count
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Vollanschlag (Full Overlay)", "0"),
                    ("Halbeinschlag (Half Overlay)", "1"),
                    ("Einliegend (Inset)", "2")),                                                          // Overlay
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Blum Clip Top", "0"),
                    ("H\u00e4fele Duomatic", "1")),                                                            // HingeType
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Links (Left)", "0"),
                    ("Rechts (Right)", "1"),
                    ("Mitte (Center, for 2-door)", "2")),                                                 // HingeSide
                WallabyHop.AutoWire.Spec.Float("0<2<5"),                                      // Gap
            });
        }
    }
}
