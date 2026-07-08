using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    /// <summary>
    /// C3 regression: floating-point noise (1e-15, 0.30000000000000004) must
    /// never leak into NC output as scientific notation. Every emitter routes
    /// through NcFmt.F (4-decimal round, InvariantCulture) — these tests feed
    /// noisy values through each public emitter and assert no 'E'/'e' exponent
    /// marker appears in any numeric position.
    /// </summary>
    [TestFixture]
    public class NumericFormattingTests
    {
        // The classic FP-noise values: tiny epsilon and accumulation noise
        private const double Tiny = 3.55e-15;
        private const double Noisy = 0.30000000000000004;

        private static void AssertNoScientificNotation(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                // E only ever appears in NC keywords (EBENE, TIEFE, SPEGA, _VE...).
                // A sci-notation leak looks like a digit followed by E/e and a
                // sign or digit: 3.55E-15, 1e+10.
                Assert.That(line, Does.Not.Match(@"\dE[-+0-9]").IgnoreCase,
                    "Scientific notation leaked into NC line: " + line);
            }
        }

        [Test]
        public void NcFmt_RoundsTinyToZero_AndTruncatesNoise()
        {
            Assert.That(NcFmt.F(Tiny), Is.EqualTo("0"));
            Assert.That(NcFmt.F(Noisy), Is.EqualTo("0.3"));
            Assert.That(NcFmt.F(-1.2e-16), Is.EqualTo("0"));
        }

        [Test]
        public void DrillLine_NoisyInputs()
        {
            string line = NcDrill.DrillLine(Tiny, Noisy, Tiny, -19.0 + Tiny, 8.0);
            AssertNoScientificNotation(new[] { line });
            Assert.That(line, Does.Contain("Bohrung (0,0.3,0,-19,8,"));
        }

        [Test]
        public void FreeSlotLine_NoisyInputs()
        {
            string line = NcSaw.FreeSlotLine(Tiny, Noisy, 100 + Tiny, Tiny, 3.2, -19 + Tiny, Tiny);
            AssertNoScientificNotation(new[] { line });
        }

        [Test]
        public void SawLogic_AxisAlignedCut_ProducesCleanCoordinates()
        {
            // Midpoint/unit-vector math on axis-aligned segments is the classic
            // source of -1.2E-16 Y-coordinates.
            var input = new SawLogic.SawInput
            {
                Segments = new[]
                {
                    new SawLogic.LineSegment(
                        new DrillLogic.Point2dz(Tiny, Noisy, 19),
                        new DrillLogic.Point2dz(1000 + Tiny, Noisy, 19)),
                },
                BladeAngles = new[] { 0.0 },
                Length = 600.0, SawKerf = 3.2, Depth = 19.0,
                Side = 1, Extend = 20.0, ToolNr = 2,
            };
            var result = SawLogic.Generate(input);
            AssertNoScientificNotation(result.Lines);
        }

        [Test]
        public void ContourLogic_NoisySegments()
        {
            var seg = ContourLogic.ContourSegment.Line(Tiny, Tiny, 100 + Tiny, Noisy);
            var lines = ContourLogic.Generate(new ContourLogic.ContourInput
            {
                Pieces = new[] { new[] { seg } },
                SurfaceZ = Tiny, Depth = 10, Passes = 1,
                LeadIn = 5.0, LeadOut = 5.0,
                ToolNr = 4, Tolerance = 0.01,
            });
            AssertNoScientificNotation(lines);
        }

        [Test]
        public void PocketAndCircPath_NoisyInputs()
        {
            var rect = PocketLogic.GenerateRect(new PocketLogic.RectPocketInput
            {
                CenterX = Tiny, CenterY = Noisy, SurfaceZ = Tiny,
                Width = 100 + Tiny, Height = 50 + Tiny,
                CornerRadius = Tiny, Angle = Tiny, Depth = 5, ToolNr = 4,
            });
            AssertNoScientificNotation(rect);

            var circ = PocketLogic.GenerateCirc(new PocketLogic.CircPocketInput
            {
                CenterX = Tiny, CenterY = Noisy, SurfaceZ = 0,
                Radius = 25 + Tiny, Depth = 10, ToolNr = 4,
            });
            AssertNoScientificNotation(circ);

            var path = CircPathLogic.Generate(new CircPathLogic.CircPathInput
            {
                CenterX = Tiny, CenterY = Noisy, SurfaceZ = 0,
                Radius = 25 + Tiny, Depth = 10, Angle = 360 - Tiny, ToolNr = 4,
            });
            AssertNoScientificNotation(path);
        }

        [Test]
        public void GrooveAndFixchip_NoisyInputs()
        {
            var groove = SlotLogic.GenerateGroove(new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[] { new SlotLogic.GroovePosition { Y = 10 + Tiny, SurfaceZ = 19 } },
                Width = 8 + Tiny, Depth = 8 + Tiny, ToolNr = 4,
            });
            AssertNoScientificNotation(groove);

            var fixchip = FixchipLogic.Generate(new FixchipLogic.FixchipInput
            {
                Positions = new[] { new DrillLogic.Point2dz(Tiny, 60 + Tiny, 9.5) },
                Angle = Tiny, Optional = true,
            });
            AssertNoScientificNotation(fixchip);
        }

        [Test]
        public void DrillRowAndBlumAndFormatCut_NoisyInputs()
        {
            var row = DrillRowLogic.Generate(new DrillRowLogic.DrillRowInput
            {
                IsXRow = true, StartX = Tiny, StartY = 100 + Tiny, StartZ = 19,
                Spacings = new[] { 32 + Tiny }, Depth = 13, Diameter = 5, ToolNr = 1,
            });
            AssertNoScientificNotation(row.Lines);

            var blum = BlumHingeLogic.Generate(new BlumHingeLogic.BlumHingeInput
            {
                Positions = new[] { new BlumHingeLogic.HingePosition { Y = 100 + Tiny, SurfaceZ = 19 } },
                Distance = 22.5 + Tiny, CupDiameter = 35, CupDepth = 12.8,
                DowelDiameter = 8, DowelDepth = 13, ToolNr = 1,
            });
            AssertNoScientificNotation(blum);

            var cut = FormatCutLogic.Generate(new FormatCutLogic.FormatCutInput
            {
                IsXCut = true,
                Positions = new[] { new FormatCutLogic.CutPosition { Y = 100 + Tiny, SurfaceZ = 19 } },
                Thickness = 19, Kw = Tiny, ToolNr = 2,
            });
            AssertNoScientificNotation(cut);
        }
    }
}
