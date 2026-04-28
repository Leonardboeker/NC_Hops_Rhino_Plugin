using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class DrillLogicTests
    {
        private static DrillLogic.DrillInput MakeInput(
            IList<DrillLogic.Point2dz> points,
            double depth = 15.0,
            double diameter = 8.0,
            double stepdown = 0.0,
            int toolNr = 3)
        {
            return new DrillLogic.DrillInput
            {
                Points = (IReadOnlyList<DrillLogic.Point2dz>)points,
                Depth = depth,
                Diameter = diameter,
                Stepdown = stepdown,
                ToolNr = toolNr,
            };
        }

        // -------------------------------------------------------------
        // SINGLE-PASS DRILLING (stepdown = 0)
        // -------------------------------------------------------------

        [Test]
        public void SinglePass_OnePoint_Snapshot()
        {
            var input = MakeInput(
                new[] { new DrillLogic.Point2dz(100, 200, 19) },
                depth: 15.0, diameter: 8.0, stepdown: 0, toolNr: 3);
            var result = DrillLogic.Generate(input);

            Assert.That(result.SurfaceZ, Is.EqualTo(19));
            Assert.That(result.CutZ, Is.EqualTo(4));
            Assert.That(result.Lines, Is.EquivalentTo(new[]
            {
                "WZB (3,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (100,200,19,4,8,0,0,0,0,0,0,0)",
            }));
        }

        [Test]
        public void SinglePass_ThreePoints_OneToolCallThenThreeDrills()
        {
            var input = MakeInput(new[]
            {
                new DrillLogic.Point2dz(0, 0, 19),
                new DrillLogic.Point2dz(50, 0, 19),
                new DrillLogic.Point2dz(50, 50, 19),
            }, depth: 10.0, diameter: 8.0, toolNr: 1);
            var result = DrillLogic.Generate(input);

            Assert.That(result.Lines.Count, Is.EqualTo(4)); // 1 tool call + 3 drills
            Assert.That(result.Lines[0], Does.StartWith("WZB"));
            for (int i = 1; i < 4; i++)
                Assert.That(result.Lines[i], Does.StartWith("Bohrung ("));
        }

        [Test]
        public void SinglePass_SurfaceZ_TakesHighestPoint()
        {
            // surfaceZ should be the MAX z of all points, not the first
            var input = MakeInput(new[]
            {
                new DrillLogic.Point2dz(0, 0, 5.0),
                new DrillLogic.Point2dz(0, 0, 19.0),  // highest
                new DrillLogic.Point2dz(0, 0, 12.0),
            }, depth: 10.0);
            var result = DrillLogic.Generate(input);

            Assert.That(result.SurfaceZ, Is.EqualTo(19.0));
            Assert.That(result.CutZ, Is.EqualTo(9.0));
        }

        // -------------------------------------------------------------
        // MULTI-PASS / PECK DRILLING (stepdown > 0)
        // -------------------------------------------------------------

        [Test]
        public void MultiPass_OnePoint_StepdownDividesDepth_Snapshot()
        {
            // depth=15, stepdown=5 → 3 passes at z=14, 9, 4 (surfaceZ=19)
            var input = MakeInput(
                new[] { new DrillLogic.Point2dz(0, 0, 19) },
                depth: 15.0, diameter: 8.0, stepdown: 5.0, toolNr: 3);
            var result = DrillLogic.Generate(input);

            Assert.That(result.Lines, Is.EquivalentTo(new[]
            {
                "WZB (3,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,19,14,8,0,0,0,0,0,0,0)",
                "Bohrung (0,0,19,9,8,0,0,0,0,0,0,0)",
                "Bohrung (0,0,19,4,8,0,0,0,0,0,0,0)",
            }));
        }

        [Test]
        public void MultiPass_StepdownLargerThanRemaining_ClampsToFullDepth()
        {
            // depth=12, stepdown=5 → passes at 5, 10, 12 (last clamped, not 15)
            var input = MakeInput(
                new[] { new DrillLogic.Point2dz(0, 0, 0) },
                depth: 12.0, stepdown: 5.0, toolNr: 1);
            var result = DrillLogic.Generate(input);

            // Last drill must hit cutZ = -12 (full depth), not -15
            Assert.That(result.Lines[result.Lines.Count - 1],
                Does.Contain(",-12,"));
        }

        // -------------------------------------------------------------
        // INPUT DEFAULTS
        // -------------------------------------------------------------

        [Test]
        public void ZeroOrNegativeDepth_FallsBackToOne()
        {
            var input = MakeInput(
                new[] { new DrillLogic.Point2dz(0, 0, 0) },
                depth: 0.0, diameter: 8.0);
            var result = DrillLogic.Generate(input);
            Assert.That(result.CutZ, Is.EqualTo(-1.0));
        }

        [Test]
        public void ZeroOrNegativeDiameter_FallsBackToEight()
        {
            var input = MakeInput(
                new[] { new DrillLogic.Point2dz(0, 0, 0) },
                depth: 5.0, diameter: 0.0);
            var result = DrillLogic.Generate(input);
            // Verify the drill line carries diameter=8 (the default)
            Assert.That(result.Lines[1], Does.Contain(",8,0,0,0,0,0,0,0)"));
        }
    }
}
