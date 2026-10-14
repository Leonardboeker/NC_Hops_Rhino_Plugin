using NUnit.Framework;
using System.Linq;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class BackplotLogicTests
    {
        [Test]
        public void ToolTracking_TagsOpsWithActiveTool()
        {
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (10,20,8,-10,10,0,0,0,0,0,0,0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "EP (0,_ANF,0)",
            }));

            Assert.That(plan.Ops.Count, Is.EqualTo(3));
            Assert.That(plan.Ops[0].ToolType, Is.EqualTo("WZB"));
            Assert.That(plan.Ops[0].ToolNr, Is.EqualTo(1));
            Assert.That(plan.Ops[1].ToolType, Is.EqualTo("WZF"));
            Assert.That(plan.Ops[2].ToolType, Is.EqualTo("WZF"));
            Assert.That(plan.Ops[2].ToolNr, Is.EqualTo(4));
        }

        [Test]
        public void ChainBuilding_G01SegmentsLinkFromPrevious()
        {
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (10,10,-5,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (110,10,0,0,0,2)",
                "G01 (110,60,0,0,0,2)",
            }));

            var moves = plan.Ops.Where(o => o.Kind == BackplotLogic.OpKind.Move).ToList();
            Assert.That(moves.Count, Is.EqualTo(3)); // SP + 2 segments
            Assert.That(moves[0].IsChainStart, Is.True);
            Assert.That(moves[1].StartX, Is.EqualTo(10));
            Assert.That(moves[1].EndX, Is.EqualTo(110));
            Assert.That(moves[2].StartY, Is.EqualTo(10));
            Assert.That(moves[2].EndY, Is.EqualTo(60));
        }

        [Test]
        public void Arcs_CaptureEndpointCenterAndDirection()
        {
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (10,0,-5,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G03M (0,10,0,0,0,0,0,2,0)",
            }));

            var arc = plan.Ops.First(o => o.Kind == BackplotLogic.OpKind.Arc);
            Assert.That(arc.StartX, Is.EqualTo(10));
            Assert.That(arc.EndY, Is.EqualTo(10));
            Assert.That(arc.CenterX, Is.EqualTo(0));
            Assert.That(arc.IsCCW, Is.True);
        }

        [Test]
        public void Drills_ReadMachineFieldOrder_DiameterDepthReference()
        {
            // Bohrung (X, Y, DIA, DEPTH, TYPE, _, _, POS, REF, ...): a golden
            // line (5 mm, 13 deep from the top) and an emitted through hole
            // (8 mm, 0.2 past the underside, depth from the table).
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (37,37,5,-13,10,0,0,0,0,0,0,0)",
                WallabyHop.NcDrill.DrillLine(100, 50, 8, 0.2, depthFromTable: true),
            }));
            Assert.That(plan.Ops.Count, Is.EqualTo(2));
            var blind = plan.Ops[0];
            Assert.That(blind.Kind, Is.EqualTo(BackplotLogic.OpKind.Drill));
            Assert.That(blind.StartX, Is.EqualTo(37));
            Assert.That(blind.Diameter, Is.EqualTo(5), "field 5 is the drill type, not the diameter");
            Assert.That(blind.Z, Is.EqualTo(-13));
            Assert.That(blind.DepthFromTable, Is.False);
            var through = plan.Ops[1];
            Assert.That(through.Diameter, Is.EqualTo(8));
            Assert.That(through.Z, Is.EqualTo(-0.2));
            Assert.That(through.DepthFromTable, Is.True);
        }

        [Test]
        public void Drill_WithExpressionPosition_IsNotPlottedAtOrigin()
        {
            // Machine files write positions as expressions (DX-37) -- no number,
            // no point; it must not land at (0,0).
            var plan = BackplotLogic.Parse("Bohrung (DX-37,37,5,-13,10,0,0,0,0,0,0,0)");
            Assert.That(plan.Ops, Is.Empty);
        }

        [Test]
        public void Drill_WithAnchorCode_IsNotPlottedAtItsRawXY()
        {
            // Field 8 = 5: (25,25) measured from another corner, not from the origin.
            var plan = BackplotLogic.Parse("Bohrung (25,25,8,-16,10,0,0,5,0,0,0,0)");
            Assert.That(plan.Ops, Is.Empty);
        }

        [Test]
        public void Macros_SegmentAndCenterAndSlashCall()
        {
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZS (10,_VE,_V*0.3,_VA,_SD,0,'')",
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=50,X2:=800,Y2:=50,NB:=3.2,Tiefe:=0,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Kreistasche_V5(VAL X_Mitte:=200,Y_Mitte:=100,Radius:=25,Tiefe:=9,Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)",
                "/CALL Fixchip_K ( VAL SPX:=0,SPY:=60,SPZ:=9.5,WKLXY:=0)",
            }));

            var seg = plan.Ops.First(o => o.Kind == BackplotLogic.OpKind.MacroSegment);
            Assert.That(seg.EndX, Is.EqualTo(800));
            Assert.That(seg.ToolType, Is.EqualTo("WZS"));

            var pts = plan.Ops.Where(o => o.Kind == BackplotLogic.OpKind.MacroPoint).ToList();
            Assert.That(pts.Count, Is.EqualTo(2)); // Kreistasche + Fixchip (/CALL)
            Assert.That(pts[0].StartX, Is.EqualTo(200));
            Assert.That(pts[1].StartY, Is.EqualTo(60), "/CALL clamps must be plotted too");
        }

        [Test]
        public void SequenceIndex_EveryOperationGetsItsOwnNumber()
        {
            var plan = BackplotLogic.Parse(string.Join("\n", new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (10,10,8,-10,10,0,0,0,0,0,0,0)",
                "Bohrung (20,10,8,-10,10,0,0,0,0,0,0,0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "SP (200,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (300,0,0,0,0,2)",
            }));

            // Machining order: drill 1 = seq 1, drill 2 = seq 2 (each drill
            // its own number — one number per GROUP hid the order of all but
            // the first hole), first SP chain = 3, second SP chain = 4.
            Assert.That(plan.ChainCount, Is.EqualTo(4));
            Assert.That(plan.Ops[0].SequenceIndex, Is.EqualTo(1));
            Assert.That(plan.Ops[1].SequenceIndex, Is.EqualTo(2));
            var chainStarts = plan.Ops.Where(o => o.IsChainStart).ToList();
            Assert.That(chainStarts.Count, Is.EqualTo(2));
            Assert.That(chainStarts[0].SequenceIndex, Is.EqualTo(3));
            Assert.That(chainStarts[1].SequenceIndex, Is.EqualTo(4));
        }

        [Test]
        public void ParkCall_IsNotDrawnAsMacroPoint()
        {
            // Park_V7 carries POSX/POSY:=0 -- it must not become op #1 at (0,0).
            string hop = WallabyHop.NcExport.AssembleFile("p", 800, 400, 19, "", null,
                new System.Collections.Generic.List<string>
                {
                    WallabyHop.NcDrill.ToolCall(1), WallabyHop.NcDrill.DrillLine(100, 50, 8, 10),
                });
            var plan = BackplotLogic.Parse(hop);

            Assert.That(plan.Ops.Count, Is.EqualTo(1));
            Assert.That(plan.Ops[0].Kind, Is.EqualTo(BackplotLogic.OpKind.Drill));
            Assert.That(plan.Ops[0].SequenceIndex, Is.EqualTo(1));
            Assert.That(plan.ChainCount, Is.EqualTo(1));
        }
    }
}
