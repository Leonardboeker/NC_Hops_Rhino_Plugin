using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class EngravingLogicTests
    {
        private static EngravingLogic.EngravingInput Make(
            IReadOnlyList<IReadOnlyList<ContourLogic.ContourSegment>> curves,
            IReadOnlyList<double> surfaceZs,
            double depth = 0.5,
            int toolNr = 7)
        {
            return new EngravingLogic.EngravingInput
            {
                Curves = curves,
                SurfaceZPerCurve = surfaceZs,
                Depth = depth,
                ToolNr = toolNr,
                Tolerance = 0.01,
                ToolType = "WZF",
                FeedFactor = 1.0,
            };
        }

        [Test]
        public void SingleStraightLine_DepthHalfMm_Snapshot()
        {
            var seg = ContourLogic.ContourSegment.Line(0, 0, 50, 0);
            var input = Make(
                new[] { new[] { seg } },
                new[] { 19.0 },  // surfaceZ = 19 (top of plate)
                depth: 0.5, toolNr: 7);
            var lines = EngravingLogic.Generate(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (7,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,18.5,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (50,0,0,0,0,2)",
                "EP (0,_ANF,0)",
            }));
        }

        [Test]
        public void TwoSeparateCurves_EachGetsOwnSPEPBlock()
        {
            var c1 = new[] { ContourLogic.ContourSegment.Line(0, 0, 50, 0) };
            var c2 = new[] { ContourLogic.ContourSegment.Line(100, 0, 150, 0) };
            var input = Make(
                new[] { c1, c2 },
                new[] { 19.0, 19.0 },
                depth: 0.5, toolNr: 7);
            var lines = EngravingLogic.Generate(input);

            int sp = lines.FindAll(l => l.StartsWith("SP ")).Count;
            int ep = lines.FindAll(l => l.StartsWith("EP ")).Count;
            Assert.That(sp, Is.EqualTo(2));
            Assert.That(ep, Is.EqualTo(2));
        }

        [Test]
        public void NoLeadInOrLeadOut_OnlyOneG01PerLineSegment()
        {
            var seg = ContourLogic.ContourSegment.Line(0, 0, 50, 0);
            var input = Make(new[] { new[] { seg } }, new[] { 0.0 }, depth: 0.5);
            var lines = EngravingLogic.Generate(input);

            int g01 = lines.FindAll(l => l.StartsWith("G01")).Count;
            Assert.That(g01, Is.EqualTo(1));
        }

        [Test]
        public void MultipleCurvesShareOneToolCall()
        {
            var c1 = new[] { ContourLogic.ContourSegment.Line(0, 0, 10, 0) };
            var c2 = new[] { ContourLogic.ContourSegment.Line(20, 0, 30, 0) };
            var c3 = new[] { ContourLogic.ContourSegment.Line(40, 0, 50, 0) };
            var input = Make(new[] { c1, c2, c3 }, new[] { 0.0, 0.0, 0.0 });
            var lines = EngravingLogic.Generate(input);

            int wzf = lines.FindAll(l => l.StartsWith("WZF")).Count;
            Assert.That(wzf, Is.EqualTo(1), "All curves should share a single tool call");
        }
    }
}
