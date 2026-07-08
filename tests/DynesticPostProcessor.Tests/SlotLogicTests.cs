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
        public void XGroove_ThreePositions_DistinctARANDPerPosition()
        {
            // C1 regression: the position must reach the macro — as ARAND
            // (distance from edge = the point's Y for an X-groove).
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[]
                {
                    new SlotLogic.GroovePosition { X = 0, Y = 100, SurfaceZ = 19 },
                    new SlotLogic.GroovePosition { X = 0, Y = 200, SurfaceZ = 19 },
                    new SlotLogic.GroovePosition { X = 0, Y = 300, SurfaceZ = 19 },
                },
                Width = 8.0, Depth = 8.0,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);

            Assert.That(lines.Count, Is.EqualTo(4)); // 1 tool call + 3 macros
            Assert.That(lines[0], Does.StartWith("WZF"));
            Assert.That(lines[1], Does.Contain("ARAND:=100"));
            Assert.That(lines[2], Does.Contain("ARAND:=200"));
            Assert.That(lines[3], Does.Contain("ARAND:=300"));
        }

        [Test]
        public void YGroove_ARANDComesFromX()
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
            Assert.That(lines[1], Does.Contain("ARAND:=50"));
        }

        [Test]
        public void Groove_Snapshot_MatchesReferenceShape()
        {
            // Shape verified against reference-hops/files/HZK_Boden_Deckel_602x278.hop:
            // CALL _Nuten_X_V5 ( VAL NB:=8.25,NT:=10,EBENE:=0,ARAND:=10,ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[]
                {
                    new SlotLogic.GroovePosition { X = 0, Y = 10, SurfaceZ = 19 },
                },
                Width = 8.25, Depth = 10.0, Ebene = 0,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Nuten_X_V5(VAL NB:=8.25,NT:=10,EBENE:=0,ARAND:=10," +
                "ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)",
            }));
        }

        [Test]
        public void Groove_EbeneFlagAndShortening()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[] { new SlotLogic.GroovePosition { Y = 7.5, SurfaceZ = 0 } },
                Width = 4, Depth = 10, Ebene = 1,
                ShortenStart = 20, ShortenEnd = 20,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);
            Assert.That(lines[1], Does.Contain("EBENE:=1"));
            Assert.That(lines[1], Does.Contain("ARAND:=7.5"));
            Assert.That(lines[1], Does.Contain("ALINKS:=20"));
            Assert.That(lines[1], Does.Contain("ARECHTS:=20"));
        }

        [Test]
        public void Groove_NegativePositionClampedToZero()
        {
            var input = new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[] { new SlotLogic.GroovePosition { Y = -10, SurfaceZ = 0 } },
                Width = 8, Depth = 8,
                ToolNr = 4,
            };
            var lines = SlotLogic.GenerateGroove(input);
            Assert.That(lines[1], Does.Contain("ARAND:=0"));
        }
    }
}
