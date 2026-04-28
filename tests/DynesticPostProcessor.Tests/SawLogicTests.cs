using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class SawLogicTests
    {
        private static SawLogic.LineSegment Seg(double x1, double y1, double x2, double y2, double z = 19.0)
        {
            return new SawLogic.LineSegment(
                new DrillLogic.Point2dz(x1, y1, z),
                new DrillLogic.Point2dz(x2, y2, z));
        }

        private static SawLogic.SawInput Input(
            IList<SawLogic.LineSegment> segments,
            double[] angles = null,
            double length = 600.0,
            double sawKerf = 3.2,
            double depth = 19.0,
            int side = 0,
            double extend = 0.0,
            int toolNr = 2)
        {
            return new SawLogic.SawInput
            {
                Segments = (IReadOnlyList<SawLogic.LineSegment>)segments,
                BladeAngles = (IReadOnlyList<double>)(angles ?? new[] { 0.0 }),
                Length = length,
                SawKerf = sawKerf,
                Depth = depth,
                Side = side,
                Extend = extend,
                ToolNr = toolNr,
            };
        }

        // -------------------------------------------------------------
        // SNAPSHOT: simple horizontal cut, side=center, no extend
        // -------------------------------------------------------------
        [Test]
        public void HorizontalCut_CenterSide_NoExtend_Snapshot()
        {
            // Length=600 (default) → use actual segment length (1000)
            var input = Input(
                new[] { Seg(0, 50, 1000, 50) },
                length: 600.0, sawKerf: 3.2, depth: 19.0, side: 0, toolNr: 2);
            var result = SawLogic.Generate(input);

            Assert.That(result.Lines.Count, Is.EqualTo(2));
            Assert.That(result.Lines[0], Is.EqualTo("WZS (2,_VE,_V*0.3,_VA,_SD,0,'')"));
            // segment is exactly 1000 long along X, midpoint (500, 50, 19), kerf centered
            Assert.That(result.Lines[1], Is.EqualTo(
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=50,X2:=1000,Y2:=50," +
                "NB:=3.2,Tiefe:=0,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0," +
                "esmd:=0,esxy1:=0,esxy2:=0)"));
        }

        // -------------------------------------------------------------
        // SIDE OFFSET — kerf shifts perpendicular to travel direction
        // -------------------------------------------------------------
        [Test]
        public void SideRight_ShiftsKerfPositiveY_HorizontalCut()
        {
            // Travel +X → perp = -Y (cross of (1,0,0) × (0,0,1) = (0,-1,0))
            // side=+1 right shifts by +(-Y * kerf/2) = Y -1.6
            var input = Input(
                new[] { Seg(0, 0, 1000, 0) },
                length: 600.0, sawKerf: 3.2, side: 1);
            var result = SawLogic.Generate(input);

            // Y should be -1.6 (shifted to "right" of travel direction)
            Assert.That(result.Segments[0].CutP1Y, Is.EqualTo(-1.6).Within(1e-9));
            Assert.That(result.Segments[0].CutP2Y, Is.EqualTo(-1.6).Within(1e-9));
        }

        [Test]
        public void SideLeft_ShiftsKerfOppositeOfRight()
        {
            var input = Input(
                new[] { Seg(0, 0, 1000, 0) },
                length: 600.0, sawKerf: 3.2, side: -1);
            var result = SawLogic.Generate(input);

            Assert.That(result.Segments[0].CutP1Y, Is.EqualTo(1.6).Within(1e-9));
        }

        [Test]
        public void SideClampedToMinusOnePlusOne()
        {
            // side=5 should clamp to 1, side=-99 to -1 (matches component's clamp)
            var input1 = Input(new[] { Seg(0, 0, 100, 0) }, sawKerf: 3.2, side: 99);
            var r1 = SawLogic.Generate(input1);
            Assert.That(r1.Segments[0].CutP1Y, Is.EqualTo(-1.6).Within(1e-9));

            var input2 = Input(new[] { Seg(0, 0, 100, 0) }, sawKerf: 3.2, side: -99);
            var r2 = SawLogic.Generate(input2);
            Assert.That(r2.Segments[0].CutP1Y, Is.EqualTo(1.6).Within(1e-9));
        }

        // -------------------------------------------------------------
        // EXTEND — runs blade past both endpoints
        // -------------------------------------------------------------
        [Test]
        public void Extend_AddsToBothEndpointsAlongTravelDir()
        {
            // segment 0..100, extend=20 → cut endpoints become -20 and 120
            var input = Input(
                new[] { Seg(0, 0, 100, 0) },
                length: 600.0, extend: 20.0);
            var result = SawLogic.Generate(input);

            Assert.That(result.Segments[0].CutP1X, Is.EqualTo(-20).Within(1e-9));
            Assert.That(result.Segments[0].CutP2X, Is.EqualTo(120).Within(1e-9));
        }

        // -------------------------------------------------------------
        // CUT LENGTH DEFAULT-DETECTION — at length=600 use actual seg length
        // -------------------------------------------------------------
        [Test]
        public void Length600_UsesActualSegmentLength()
        {
            var input = Input(
                new[] { Seg(0, 0, 250, 0) },  // segment length = 250
                length: 600.0);
            var result = SawLogic.Generate(input);
            Assert.That(result.Segments[0].CutLength, Is.EqualTo(250).Within(1e-9));
        }

        [Test]
        public void ExplicitLength_OverridesSegmentLength()
        {
            var input = Input(
                new[] { Seg(0, 0, 250, 0) },
                length: 1000.0);  // explicit, not default
            var result = SawLogic.Generate(input);
            Assert.That(result.Segments[0].CutLength, Is.EqualTo(1000).Within(1e-9));
        }

        // -------------------------------------------------------------
        // GUARDS — zero-length segments are reported skipped, not throw
        // -------------------------------------------------------------
        [Test]
        public void ZeroLengthSegment_IsSkippedWithReason()
        {
            var input = Input(new[] { Seg(50, 50, 50, 50) });
            var result = SawLogic.Generate(input);
            Assert.That(result.Lines.Count, Is.EqualTo(0));
            Assert.That(result.Segments[0].Skipped, Is.True);
            Assert.That(result.Segments[0].SkipReason, Does.Contain("Zero-length"));
        }

        // -------------------------------------------------------------
        // CUT Z = origin Z minus depth (regardless of sign of depth input)
        // -------------------------------------------------------------
        [Test]
        public void CutZ_IsAlwaysSurfaceMinusAbsDepth()
        {
            var input = Input(
                new[] { Seg(0, 0, 100, 0, z: 19.0) },
                depth: 19.0);
            var result = SawLogic.Generate(input);
            Assert.That(result.Segments[0].CutZ, Is.EqualTo(0.0).Within(1e-9));
        }

        // -------------------------------------------------------------
        // BLADE ANGLES LIST — cycles through angles when fewer than segments
        // -------------------------------------------------------------
        [Test]
        public void MultipleSegments_BladeAnglesCycle()
        {
            var input = Input(
                new[] { Seg(0, 0, 100, 0), Seg(0, 100, 100, 100), Seg(0, 200, 100, 200) },
                angles: new[] { 0.0, 45.0 }); // 2 angles, 3 segments → cycles 0,45,0
            var result = SawLogic.Generate(input);

            Assert.That(result.Segments[0].BladeAngle, Is.EqualTo(0.0));
            Assert.That(result.Segments[1].BladeAngle, Is.EqualTo(45.0));
            Assert.That(result.Segments[2].BladeAngle, Is.EqualTo(0.0));
        }

        // -------------------------------------------------------------
        // SHAPE: each segment emits ONE tool call line + ONE NC line
        // (matches existing component behavior — even if same tool repeats,
        // sorter at export time merges them)
        // -------------------------------------------------------------
        [Test]
        public void TwoSegments_ProducesFourLines()
        {
            var input = Input(new[]
            {
                Seg(0, 0, 100, 0),
                Seg(0, 50, 100, 50),
            });
            var result = SawLogic.Generate(input);
            Assert.That(result.Lines.Count, Is.EqualTo(4));
            Assert.That(result.Lines[0], Does.StartWith("WZS"));
            Assert.That(result.Lines[1], Does.StartWith("CALL _nuten_frei_v5"));
            Assert.That(result.Lines[2], Does.StartWith("WZS"));
            Assert.That(result.Lines[3], Does.StartWith("CALL _nuten_frei_v5"));
        }
    }
}
