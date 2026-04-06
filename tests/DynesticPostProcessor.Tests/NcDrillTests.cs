using NUnit.Framework;
using System.Collections.Generic;
using DynesticPostProcessor;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class NcDrillTests
    {
        [Test]
        public void ToolCall_ReturnsCorrectPrefix()
        {
            string result = NcDrill.ToolCall(3);
            Assert.That(result, Does.StartWith("WZB"));
        }

        [Test]
        public void ToolCall_ContainsToolNr()
        {
            string result = NcDrill.ToolCall(5);
            Assert.That(result, Does.Contain("5"));
        }

        [Test]
        public void BohrungLine_UsesInvariantCulture()
        {
            string result = NcDrill.BohrungLine(100.5, 200.75, 19.0, 9.0, 8.0);
            Assert.That(result, Does.Contain("100.5"));
            Assert.That(result, Does.Contain("200.75"));
            Assert.That(result, Does.Not.Contain("100,5")); // no comma decimal
        }

        [Test]
        public void BohrungLine_StartsWithBohrung()
        {
            string result = NcDrill.BohrungLine(0, 0, 0, -10, 8);
            Assert.That(result, Does.StartWith("Bohrung ("));
        }

        [Test]
        public void BohrungLine_EndsWithZeroParams()
        {
            string result = NcDrill.BohrungLine(10, 20, 19, 9, 8);
            Assert.That(result, Does.EndWith(",0,0,0,0,0,0,0)"));
        }
    }
}
