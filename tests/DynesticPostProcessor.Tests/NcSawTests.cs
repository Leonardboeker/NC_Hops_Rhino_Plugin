using NUnit.Framework;
using WallabyHop;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class NcSawTests
    {
        [Test]
        public void ToolCall_Snapshot()
        {
            Assert.That(NcSaw.ToolCall(2),
                Is.EqualTo("WZS (2,_VE,_V*0.3,_VA,_SD,0,'')"));
        }

        [Test]
        public void FreeSlotLine_Snapshot_Standard()
        {
            Assert.That(NcSaw.FreeSlotLine(10.5, 20.0, 110.5, 20.0, 3.2, -19.0, 45.0),
                Is.EqualTo(
                    "CALL _nuten_frei_v5(VAL X1:=10.5,Y1:=20,X2:=110.5,Y2:=20," +
                    "NB:=3.2,Tiefe:=-19,LAGE:=45,RK:=0,SPEGA:=0,EPEGA:=0," +
                    "esmd:=0,esxy1:=0,esxy2:=0)"));
        }

        [Test]
        public void FreeSlotLine_Snapshot_Origin()
        {
            Assert.That(NcSaw.FreeSlotLine(0, 0, 100, 0, 3.2, -19, 0),
                Is.EqualTo(
                    "CALL _nuten_frei_v5(VAL X1:=0,Y1:=0,X2:=100,Y2:=0," +
                    "NB:=3.2,Tiefe:=-19,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0," +
                    "esmd:=0,esxy1:=0,esxy2:=0)"));
        }

        [Test]
        public void FreeSlotLine_UsesInvariantCulture()
        {
            string result = NcSaw.FreeSlotLine(10.5, 0, 100, 0, 3.2, -19, 22.5);
            Assert.That(result, Does.Contain("22.5"));
            Assert.That(result, Does.Not.Contain("22,5"));
        }
    }
}
