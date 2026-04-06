using NUnit.Framework;
using DynesticPostProcessor;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class NcSawTests
    {
        [Test]
        public void ToolCall_ReturnsCorrectPrefix()
        {
            string result = NcSaw.ToolCall(2);
            Assert.That(result, Does.StartWith("WZS"));
        }

        [Test]
        public void ToolCall_ContainsFeedFactor()
        {
            string result = NcSaw.ToolCall(2);
            Assert.That(result, Does.Contain("0.3"));
        }

        [Test]
        public void NutenFreiLine_StartsWithCall()
        {
            string result = NcSaw.NutenFreiLine(0, 0, 100, 0, 3.2, -19, 0);
            Assert.That(result, Does.StartWith("CALL _nuten_frei_v5"));
        }

        [Test]
        public void NutenFreiLine_ContainsAllParams()
        {
            string result = NcSaw.NutenFreiLine(10.5, 20.0, 110.5, 20.0, 3.2, -19.0, 45.0);
            Assert.That(result, Does.Contain("X1:=10.5"));
            Assert.That(result, Does.Contain("Y1:=20"));
            Assert.That(result, Does.Contain("X2:=110.5"));
            Assert.That(result, Does.Contain("NB:=3.2"));
            Assert.That(result, Does.Contain("LAGE:=45"));
        }

        [Test]
        public void NutenFreiLine_UsesInvariantCulture()
        {
            string result = NcSaw.NutenFreiLine(10.5, 0, 100, 0, 3.2, -19, 22.5);
            Assert.That(result, Does.Contain("22.5"));
            Assert.That(result, Does.Not.Contain("22,5"));
        }
    }
}
