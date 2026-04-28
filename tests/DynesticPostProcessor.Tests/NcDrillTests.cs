using NUnit.Framework;
using WallabyHop;

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
            Assert.That(NcDrill.DrillLine(100.5, 200.75, 19.0, 9.0, 8.0),
                Is.EqualTo("Bohrung (100.5,200.75,19,9,8,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void DrillLine_Snapshot_Origin()
        {
            Assert.That(NcDrill.DrillLine(0, 0, 0, -10, 8),
                Is.EqualTo("Bohrung (0,0,0,-10,8,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void DrillLine_UsesInvariantCulture_NoCommaDecimal()
        {
            string result = NcDrill.DrillLine(100.5, 200.75, 19.0, 9.0, 8.0);
            Assert.That(result, Does.Not.Contain("100,5"));
            Assert.That(result, Does.Not.Contain("200,75"));
        }
    }
}
