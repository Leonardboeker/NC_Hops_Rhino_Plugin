using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WallabyHop.Components.Korpus
{
    public class HopConnectorComponent : GH_Component
    {
        public HopConnectorComponent()
            : base("HopConnector", "HopConnector",
                "Cabinet corner connector options. Generates corner drill/routing operations for the panels. Wire into HopKorpus 'connectors' input.",
                "Wallaby Hop", "Cabinet")
        {
        }

        // -----------------------------------------------------------------
        // INPUTS
        // -----------------------------------------------------------------
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Type", "type",
                "Connector type. 0=Dowel8x30 (d8 15mm deep each side), 1=Dowel8x40 (d8 20mm deep), 2=Cabineo8 (d20x12.5mm face + d8x28mm end), 3=ClamexP14 (routing op, requires special saw), 4=NestingDowel (d8x30 press-fit). Default 0.",
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            pManager.AddBooleanParameter("AutoCount", "auto",
                "Automatically determine connector count by cabinet depth. True: D<=300->2, D<=500->3, D>500->4 per joint. False: use Count input. Default True.",
                GH_ParamAccess.item, true);
            pManager[1].Optional = true;

            pManager.AddIntegerParameter("Count", "count",
                "Connectors per joint edge (used when AutoCount=False). Default 2.",
                GH_ParamAccess.item, 2);
            pManager[2].Optional = true;
        }

        // -----------------------------------------------------------------
        // OUTPUTS
        // -----------------------------------------------------------------
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ConnectorData", "connectors",
                "Connector configuration. Wire into HopKorpus 'connectors' input.",
                GH_ParamAccess.item);
        }

        // -----------------------------------------------------------------
        // SOLVE
        // -----------------------------------------------------------------
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int type = 0;
            bool autoCount = true;
            int count = 2;

            DA.GetData(0, ref type);
            DA.GetData(1, ref autoCount);
            DA.GetData(2, ref count);

            // Guards
            if (type < 0 || type > 4)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Type must be 0, 1, 2, 3, or 4");
                return;
            }

            if (count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Count must be >= 1");
                return;
            }

            // Connector dimension table
            // [typeName, drillDiameter, drillDepth, isRouting]
            var connectorDims = new Dictionary<int, object[]>
            {
                { 0, new object[] { "Dowel8x30",    8.0, 15.0, false } },
                { 1, new object[] { "Dowel8x40",    8.0, 20.0, false } },
                { 2, new object[] { "Cabineo8",    20.0, 12.5, false } },
                { 3, new object[] { "ClamexP14",    0.0,  0.0, true  } },
                { 4, new object[] { "NestingDowel", 8.0, 15.0, false } },
            };

            var dims = connectorDims[type];

            // Build configuration dictionary
            var dict = new Dictionary<string, object>();
            dict["type"] = type;
            dict["typeName"] = (string)dims[0];
            dict["drillDiameter"] = (double)dims[1];
            dict["drillDepth"] = (double)dims[2];
            dict["isRouting"] = (bool)dims[3];
            dict["autoCount"] = autoCount;
            dict["count"] = count;
            // Cabineo8: additional end-grain hole dimensions
            dict["cabineo_endDia"] = 8.0;
            dict["cabineo_endDepth"] = 28.0;
            // System-32 grid
            dict["grid"] = 32.0;    // SYSTEM32_RASTER
            dict["edge"] = 37.0;    // SYSTEM32_RAND -- distance from front/back edge to first hole center

            // Output
            DA.SetData(0, new GH_ObjectWrapper(dict));

            // Remark
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "HopConnector: " + (string)dims[0]
                + (autoCount ? " (auto count)" : " count=" + count));
        }

        // -----------------------------------------------------------------
        // ICON + GUID
        // -----------------------------------------------------------------
        protected override Bitmap Icon => IconHelper.Load("HopConnector");

        public override Guid ComponentGuid =>
            new Guid("c3d4e5f6-a7b8-9012-cdef-012345678901");

        // -----------------------------------------------------------------
        // AUTO-WIRE
        // -----------------------------------------------------------------
        public override void AddedToDocument(GH_Document doc)
        {
            base.AddedToDocument(doc);
            WallabyHop.AutoWire.Apply(this, doc, new[]
            {
                WallabyHop.AutoWire.Spec.ValueList(
                    ("Holzd\u00fcbel 8\u00d730", "0"),
                    ("Holzd\u00fcbel 8\u00d740", "1"),
                    ("Cabineo 8", "2"),
                    ("Clamex P-14", "3"),
                    ("Nesting-D\u00fcbel", "4")),       // Type
                WallabyHop.AutoWire.Spec.Toggle(),           // AutoCount
                WallabyHop.AutoWire.Spec.Int("1<2<6"),       // Count
            });
        }
    }
}
