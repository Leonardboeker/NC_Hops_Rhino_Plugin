using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using WallabyHop;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class NcDrillTests
    {
        // -------------------------------------------------------------
        // Snapshot baselines — exact NC strings the HOLZHER controller
        // expects. If any of these change, machining behavior changes.
        // -------------------------------------------------------------

        [Test]
        public void ToolCall_Snapshot()
        {
            Assert.That(NcDrill.ToolCall(3),
                Is.EqualTo("WZB (3,_VE,_V*1,_VA,_SD,0,'')"));
        }

        [Test]
        public void DrillLine_Snapshot_Standard()
        {
            // 8 mm bit, 9 mm deep. Depth is written top-relative negative and the
            // 5th field is type 10, which the machine files carry for standard bits.
            Assert.That(NcDrill.DrillLine(100.5, 200.75, 8.0, 9.0),
                Is.EqualTo("Bohrung (100.5,200.75,8,-9,10,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void DrillLine_Snapshot_Origin()
        {
            // A depth handed in positive or negative must land negative either way.
            Assert.That(NcDrill.DrillLine(0, 0, 8, -10),
                Is.EqualTo("Bohrung (0,0,8,-10,10,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void DrillLine_HingeCup_WritesType30()
        {
            // Type 10 with D35 stops HOPS ("Bohrer (Type 1) mit Durchmesser 35.00
            // existiert nicht"); every D35 reference line carries 30.
            Assert.That(NcDrill.DrillLine(22.5, 128, 35, 13.5), Does.Contain(",35,-13.5,30,"));
            Assert.That(NcDrill.DrillLine(10, 10, 15, 12), Does.Contain(",15,-12,30,"));
            Assert.That(NcDrill.DrillLine(10, 10, 8, 12), Does.Contain(",8,-12,10,"));
        }

        [Test]
        public void DrillLine_UsesInvariantCulture_NoCommaDecimal()
        {
            string result = NcDrill.DrillLine(100.5, 200.75, 8.0, 9.0);
            Assert.That(result, Does.Not.Contain("100,5"));
            Assert.That(result, Does.Not.Contain("200,75"));
        }

        // -------------------------------------------------------------
        // Read-back: TryParse is the one reader for backplot, stock sim,
        // analyzer and the Korpus preview.
        // -------------------------------------------------------------

        [Test]
        public void TryParse_ReadsEmittedLine_TopReference()
        {
            Assert.That(NcDrill.TryParse(NcDrill.DrillLine(100.5, 200.75, 8, 9), out var d), Is.True);
            Assert.That(d.HasXY, Is.True);
            Assert.That(d.X, Is.EqualTo(100.5));
            Assert.That(d.Y, Is.EqualTo(200.75));
            Assert.That(d.Diameter, Is.EqualTo(8));
            Assert.That(d.Depth, Is.EqualTo(-9));
            Assert.That(d.BitType, Is.EqualTo(10));
            Assert.That(d.DepthFromTable, Is.False);
            Assert.That(d.BottomZ(19), Is.EqualTo(10), "9 mm below the 19 mm top");
        }

        [Test]
        public void TryParse_TableReference_DepthIsZAboveTable()
        {
            Assert.That(NcDrill.TryParse("/" + NcDrill.DrillLine(10, 20, 5, 0.2, depthFromTable: true), out var d), Is.True);
            Assert.That(d.DepthFromTable, Is.True);
            Assert.That(d.BottomZ(19), Is.EqualTo(-0.2).Within(1e-9));
        }

        [Test]
        public void TryParse_ExpressionPosition_KeepsDiameterAndDepth()
        {
            Assert.That(NcDrill.TryParse("Bohrung (DX-37,37,35,-13,30,0,0,0,0,0,0,0)", out var d), Is.True);
            Assert.That(d.HasXY, Is.False);
            Assert.That(d.Diameter, Is.EqualTo(35));
            Assert.That(d.BitType, Is.EqualTo(30));
        }

        [Test]
        public void TryParse_AnchorCode_IsNoAbsolutePosition()
        {
            // Field 8 = 5 anchors (25,25) to another corner (Format_1 pattern):
            // diameter and depth still read, but no point in the part frame.
            Assert.That(NcDrill.TryParse("Bohrung (25,25,8,-16,10,0,0,5,0,0,0,0)", out var d), Is.True);
            Assert.That(d.Pos, Is.EqualTo(5));
            Assert.That(d.HasXY, Is.False);
            Assert.That(d.Depth, Is.EqualTo(-16));
            Assert.That(NcDrill.TryParse("Bohrung (25,25,8,-16,10,0,0,1,0,0,0,0)", out var abs), Is.True);
            Assert.That(abs.HasXY, Is.True, "code 1 = absolute, as in a run file");
        }

        [Test]
        public void TryParse_RejectsOtherLinesAndShortFieldLists()
        {
            Assert.That(NcDrill.TryParse("SP (0,0,-0.2,1,0,_ANF,0,0,0,0,1,2,0)", out _), Is.False);
            Assert.That(NcDrill.TryParse("Bohrung (1,2,8,-10,10)", out _), Is.False);
        }

        [Test]
        public void TryParse_RejectsOldFieldOrder()
        {
            // Old (X, Y, surfZ, cutZ, DIA) order: diameter 0, type 35 -- no machine line.
            Assert.That(NcDrill.TryParse("Bohrung (22.5,128,0,-13.5,35,0,0,0,0,0,0,0)", out _), Is.False);
        }

        // -------------------------------------------------------------
        // Round trip: emitter -> backplot / stock sim / analyzer. Every
        // reader must get back the diameter and depth that went in.
        // -------------------------------------------------------------

        [Test]
        public void RoundTrip_EmitterToAllReaders_SameDiameterAndDepth()
        {
            var ops = new List<string>();
            ops.AddRange(DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = new[] { new DrillLogic.Point2dz(37, 37, 19) },
                Depth = 13, Diameter = 5, ToolNr = 1,
            }).Lines);
            ops.AddRange(DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = new[] { new DrillLogic.Point2dz(300, 200, 19) },
                Depth = 0.2, Diameter = 8, ToolNr = 1, DepthFromTable = true,
            }).Lines);
            string hop = NcExport.AssembleFile("roundtrip", 600, 400, 19, "", null, ops);

            var drills = BackplotLogic.Parse(hop).Ops
                .Where(o => o.Kind == BackplotLogic.OpKind.Drill).ToList();
            Assert.That(drills.Select(o => o.Diameter), Is.EqualTo(new[] { 5.0, 8.0 }));
            Assert.That(drills.Select(o => o.Z), Is.EqualTo(new[] { -13.0, -0.2 }));
            Assert.That(drills.Select(o => o.DepthFromTable), Is.EqualTo(new[] { false, true }));

            var cuts = StockSimLogic.Parse(hop, null, 8.0).Cuts;
            Assert.That(cuts.Select(c => c.Width), Is.EqualTo(new[] { 5.0, 8.0 }));
            Assert.That(cuts[0].Z0, Is.EqualTo(6).Within(1e-9), "13 deep from the top");
            Assert.That(cuts[1].Z0, Is.EqualTo(-0.2).Within(1e-9), "0.2 past the underside");
            Assert.That(cuts.All(c => c.Z1 == 19), Is.True);

            var r = HopAnalyzer.Analyze(hop);
            Assert.That(r.DrillCount, Is.EqualTo(2));
            Assert.That(r.ZWarnings, Is.Empty);
        }
    }
}
