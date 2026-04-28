using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class SlotLogicTests
    {
        // -------------------------------------------------------------
        // FREE SLOT (single segment, milled)
        // -------------------------------------------------------------

        [Test]
        public void FreeSlot_BasicSnapshot()
        {
            var input = new SlotLogic.FreeSlotInput
            {
                P1X = 0, P1Y = 0, P1Z = 19,
                P2X = 100, P2Y = 50, P2Z = 19,
                SlotWidth = 8.0,
                Depth = 10.0,
                ToolNr = 4,
                ToolType = "WZF",
                FeedFactor = 1.0,
            };
            var lines = SlotLogic.GenerateFreeSlot(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=0,X2:=100,Y2:=50," +
                "NB:=8,Tiefe:=9,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0," +
                "esmd:=0,esxy1:=0,esxy2:=0)",
            }));
        }

        [Test]
        public void FreeSlot_TopZIsHigherEndpoint()
        {
            // P1Z=10, P2Z=19 → topZ=19 → cutZ = 19 - 10 = 9
            var input = new SlotLogic.FreeSlotInput
            {
                P1X = 0, P1Y = 0, P1Z = 10,
                P2X = 100, P2Y = 0, P2Z = 19,
                SlotWidth = 8, Depth = 10, ToolNr = 4,
            };
            var lines = SlotLogic.GenerateFreeSlot(input);
            Assert.That(lines[1], Does.Contain("Tiefe:=9"));
        }

        // -------------------------------------------------------------
        // GROOVE ROW (axis-aligned, list of positions)
        // -------------------------------------------------------------

        [Test]
        public void XGroove_ThreePositions_OneToolCallThreeMacros()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[]
                {
                    new SlotLogic.GroovePosition { X = 0, Y = 100, SurfaceZ = 19 },
                    new SlotLogic.GroovePosition { X = 0, Y = 200, SurfaceZ = 19 },
                    new SlotLogic.GroovePosition { X = 0, Y = 300, SurfaceZ = 19 },
                },
                Width = 8.0, Depth = 8.0, EdgeDist = 0.0,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);

            Assert.That(lines.Count, Is.EqualTo(4)); // 1 tool call + 3 macros
            Assert.That(lines[0], Does.StartWith("WZF"));
            for (int i = 1; i < 4; i++)
                Assert.That(lines[i], Does.StartWith("CALL _Nuten_X_V5"));
        }

        [Test]
        public void YGroove_ProducesNutenY_V5()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = false,
                Positions = new[]
                {
                    new SlotLogic.GroovePosition { X = 50, Y = 0, SurfaceZ = 19 },
                },
                Width = 8.0, Depth = 8.0, ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);
            Assert.That(lines[1], Does.StartWith("CALL _Nuten_Y_V5"));
        }

        [Test]
        public void Groove_Snapshot()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[]
                {
                    new SlotLogic.GroovePosition { X = 0, Y = 100, SurfaceZ = 19 },
                },
                Width = 8.0, Depth = 8.0, EdgeDist = 5.0,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Nuten_X_V5(VAL NB:=8,NT:=8,EBENE:=19,ARAND:=5," +
                "ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)",
            }));
        }

        [Test]
        public void Groove_NegativeEdgeDistClampedToZero()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[] { new SlotLogic.GroovePosition { SurfaceZ = 0 } },
                Width = 8, Depth = 8, EdgeDist = -10,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);
            Assert.That(lines[1], Does.Contain("ARAND:=0"));
        }
    }
}
