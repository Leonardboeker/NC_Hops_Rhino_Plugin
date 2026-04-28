using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class MaterialListBuilderTests
    {
        [Test]
        public void EmptyList_ReturnsPlaceholder()
        {
            Assert.That(MaterialListBuilder.Build(new List<MaterialListBuilder.Part>()),
                Is.EqualTo("(no Parts connected)"));
            Assert.That(MaterialListBuilder.Build(null),
                Is.EqualTo("(no Parts connected)"));
        }

        [Test]
        public void SinglePart_FormatsAreaAndSum_Snapshot()
        {
            // 600x400 = 0.240 m²
            var parts = new[]
            {
                new MaterialListBuilder.Part
                {
                    Name = "Bottom", Thickness = 19, DxMm = 600, DyMm = 400,
                },
            };
            string result = MaterialListBuilder.Build(parts);
            string[] expected =
            {
                "t=19mm:",
                "  Bottom         600x400=0.240m²",
                "  Σ 1 parts / 0.240m²",
            };
            Assert.That(result, Is.EqualTo(string.Join("\r\n", expected)));
        }

        [Test]
        public void MultipleThicknesses_GroupedAscending()
        {
            // Mix of 8 and 19 mm panels — 8mm group should appear first
            var parts = new[]
            {
                new MaterialListBuilder.Part { Name = "Bottom", Thickness = 19, DxMm = 600, DyMm = 400 },
                new MaterialListBuilder.Part { Name = "Back",   Thickness = 8,  DxMm = 562, DyMm = 682 },
                new MaterialListBuilder.Part { Name = "Top",    Thickness = 19, DxMm = 600, DyMm = 400 },
            };
            string result = MaterialListBuilder.Build(parts);

            int idx8  = result.IndexOf("t=8mm");
            int idx19 = result.IndexOf("t=19mm");
            Assert.That(idx8, Is.GreaterThanOrEqualTo(0));
            Assert.That(idx19, Is.GreaterThan(idx8), "8mm group must come before 19mm");
        }

        [Test]
        public void SumPerGroup_AccountsForAllPartsExactly()
        {
            // Two 19mm panels: 600x400 (0.240) + 562x682 (0.383284) → sum 0.623
            var parts = new[]
            {
                new MaterialListBuilder.Part { Name = "Bottom", Thickness = 19, DxMm = 600, DyMm = 400 },
                new MaterialListBuilder.Part { Name = "Top",    Thickness = 19, DxMm = 562, DyMm = 682 },
            };
            string result = MaterialListBuilder.Build(parts);
            // Bottom 0.240, Top ≈ 0.383 → sum ≈ 0.623
            Assert.That(result, Does.Contain("Σ 2 parts / 0.623m²"));
        }

        [Test]
        public void UsesInvariantCulture_NoCommaDecimal()
        {
            // Even on a German locale machine the output must use '.' decimal separator
            var parts = new[]
            {
                new MaterialListBuilder.Part { Name = "Test", Thickness = 19, DxMm = 1500, DyMm = 333 },
            };
            string result = MaterialListBuilder.Build(parts);
            Assert.That(result, Does.Not.Contain("0,"));
            Assert.That(result, Does.Contain("0.4995m²")
                .Or.Contain("0.500m²")); // depending on rounding
        }
    }
}
