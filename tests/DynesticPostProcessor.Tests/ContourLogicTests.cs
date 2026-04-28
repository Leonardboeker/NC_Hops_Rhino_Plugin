using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class ContourLogicTests
    {
        private static ContourLogic.ContourInput Input(
            IReadOnlyList<IReadOnlyList<ContourLogic.ContourSegment>> pieces,
            double surfaceZ = 0.0,
            double depth = 10.0,
            int passes = 1,
            double overcut = 0.0,
            double leadIn = 0.0,
            double leadOut = 0.0,
            int toolNr = 4,
            double tolerance = 0.01)
        {
            return new ContourLogic.ContourInput
            {
                Pieces = pieces,
                SurfaceZ = surfaceZ,
                Depth = depth,
                Passes = passes,
                Overcut = overcut,
                LeadIn = leadIn,
                LeadOut = leadOut,
                ToolNr = toolNr,
                Tolerance = tolerance,
                ToolType = "WZF",
                FeedFactor = 1.0,
            };
        }

        // -------------------------------------------------------------
        // SINGLE LINE — minimum viable contour
        // -------------------------------------------------------------
        [Test]
        public void SingleLine_SinglePass_NoLeadInOut_Snapshot()
        {
            var seg = ContourLogic.ContourSegment.Line(0, 0, 100, 0);
            var pieces = new List<IReadOnlyList<ContourLogic.ContourSegment>>
            {
                new[] { seg },
            };
            var lines = ContourLogic.Generate(Input(pieces, surfaceZ: 0, depth: 10, toolNr: 4));

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "EP (0,_ANF,0)",
            }));
        }

        [Test]
        public void TwoPasses_FirstZIsSurfaceMinusDepthOverPasses_Snapshot()
        {
            // depth=10, passes=2 → first plunge at -5
            var pieces = new[] { new[] { ContourLogic.ContourSegment.Line(0, 0, 50, 0) } };
            var lines = ContourLogic.Generate(Input(pieces, surfaceZ: 0, depth: 10, passes: 2, toolNr: 4));

            // SP line should encode -5 plunge AND multi-pass tail
            Assert.That(lines[1],
                Is.EqualTo("SP (0,0,-5,2,0,_ANF,0,0,0,0,1,0,2,0,0,0,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void Overcut_AddsExtraBlockAtFullDepthMinusOvercut_Snapshot()
        {
            var pieces = new[] { new[] { ContourLogic.ContourSegment.Line(0, 0, 50, 0) } };
            var lines = ContourLogic.Generate(Input(pieces, surfaceZ: 0, depth: 10, overcut: 0.2, toolNr: 4));

            // Expect TWO blocks (regular + overcut). Find the second SP.
            int firstSP = lines.FindIndex(l => l.StartsWith("SP "));
            int secondSP = lines.FindIndex(firstSP + 1, l => l.StartsWith("SP "));
            Assert.That(secondSP, Is.GreaterThan(firstSP), "Expected a second SP for overcut block");
            Assert.That(lines[secondSP], Does.Contain(",-10.2,"));
        }

        // -------------------------------------------------------------
        // ARC SEGMENT — emits G03M (CCW) or G02M (CW)
        // -------------------------------------------------------------
        [Test]
        public void ArcCCW_EmitsG03M()
        {
            // Arc from (10,0) to (0,10) around (0,0), going CCW
            var arc = ContourLogic.ContourSegment.Arc(
                sx: 10, sy: 0, ex: 0, ey: 10,
                cx: 0, cy: 0, isCCW: true,
                tangentStartX: 0, tangentStartY: 1, tangentEndX: -1, tangentEndY: 0);
            var pieces = new[] { new[] { arc } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 5));

            int g03 = lines.FindIndex(l => l.StartsWith("G03M"));
            Assert.That(g03, Is.GreaterThanOrEqualTo(0), "Expected G03M for CCW arc");
            Assert.That(lines[g03], Is.EqualTo("G03M (0,10,0,0,0,0,0,2,0)"));
        }

        [Test]
        public void ArcCW_EmitsG02M()
        {
            var arc = ContourLogic.ContourSegment.Arc(
                sx: 0, sy: 10, ex: 10, ey: 0,
                cx: 0, cy: 0, isCCW: false,
                tangentStartX: 1, tangentStartY: 0, tangentEndX: 0, tangentEndY: -1);
            var pieces = new[] { new[] { arc } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 5));
            Assert.That(lines, Has.Some.StartsWith("G02M"));
        }

        // -------------------------------------------------------------
        // LEAD-IN / LEAD-OUT
        // -------------------------------------------------------------
        [Test]
        public void LeadIn_ShiftsSPBackwardAlongStartTangent()
        {
            // line segment 0,0 → 100,0 has tangent (1,0), leadIn=10 → SP at (-10, 0)
            var seg = ContourLogic.ContourSegment.Line(0, 0, 100, 0);
            var pieces = new[] { new[] { seg } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 10, leadIn: 10.0));

            Assert.That(lines[1], Does.Contain("SP (-10,0,"));
            // Plus the explicit G01 to the actual start point
            Assert.That(lines[2], Is.EqualTo("G01 (0,0,0,0,0,2)"));
        }

        [Test]
        public void LeadOut_ExtendsPastEndAlongEndTangent()
        {
            var seg = ContourLogic.ContourSegment.Line(0, 0, 100, 0);
            var pieces = new[] { new[] { seg } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 10, leadOut: 5.0));

            // Expect an extra G01 to (105, 0) before EP
            int epIdx = lines.FindIndex(l => l.StartsWith("EP "));
            Assert.That(lines[epIdx - 1], Is.EqualTo("G01 (105,0,0,0,0,2)"));
        }

        // -------------------------------------------------------------
        // CONNECTIVITY SPLIT — disconnected segments produce separate SP/EP blocks
        // -------------------------------------------------------------
        [Test]
        public void DisconnectedSegments_SplitIntoSeparateBlocks()
        {
            // Two segments that don't connect (100,0 → 100,0 vs 200,0 → 300,0)
            var s1 = ContourLogic.ContourSegment.Line(0, 0, 100, 0);
            var s2 = ContourLogic.ContourSegment.Line(200, 0, 300, 0);
            var pieces = new[] { new[] { s1, s2 } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 10));

            int spCount = lines.FindAll(l => l.StartsWith("SP ")).Count;
            int epCount = lines.FindAll(l => l.StartsWith("EP ")).Count;
            Assert.That(spCount, Is.EqualTo(2), "Two disconnected runs → two SP lines");
            Assert.That(epCount, Is.EqualTo(2));
        }

        [Test]
        public void ConnectedSegments_SingleBlock()
        {
            // Two segments end-to-end → one block
            var s1 = ContourLogic.ContourSegment.Line(0, 0, 50, 0);
            var s2 = ContourLogic.ContourSegment.Line(50, 0, 100, 0);
            var pieces = new[] { new[] { s1, s2 } };
            var lines = ContourLogic.Generate(Input(pieces, depth: 10));

            int spCount = lines.FindAll(l => l.StartsWith("SP ")).Count;
            Assert.That(spCount, Is.EqualTo(1));
        }

        // -------------------------------------------------------------
        // FLOATING-POINT NOISE GUARD — Fmt rounds to 4 decimals
        // -------------------------------------------------------------
        [Test]
        public void Fmt_RoundsTinyValuesToZero()
        {
            // 3.55e-15 should become 0, not "3.55E-15"
            string s = ContourLogic.Fmt(3.55e-15);
            Assert.That(s, Is.EqualTo("0"));
        }

        [Test]
        public void Fmt_KeepsFourDecimals()
        {
            Assert.That(ContourLogic.Fmt(1.23456789), Is.EqualTo("1.2346"));
        }
    }
}
