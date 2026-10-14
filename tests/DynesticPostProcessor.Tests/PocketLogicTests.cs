using NUnit.Framework;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class PocketLogicTests
    {
        // -------------------------------------------------------------
        // RECTANGULAR POCKET (_Rechteck_V7)
        // -------------------------------------------------------------

        [Test]
        public void RectPocket_BasicSnapshot()
        {
            var input = new PocketLogic.RectPocketInput
            {
                CenterX = 100, CenterY = 50, SurfaceZ = 19,
                Width = 80, Height = 40,
                CornerRadius = 0,
                Angle = 0,
                Depth = 5,
                Stepdown = 0,
                ToolNr = 4,
                ToolType = "WZF", FeedFactor = 1.0,
            };
            var lines = PocketLogic.GenerateRect(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Rechteck_V7(VAL X_MITTE:=100,Y_MITTE:=50,LAENGE:=80,BREITE:=40," +
                "RADIUS:=0,WINKEL:=0,TIEFE:=-5,ZUTIEFE:=0,RADIUSKORREKTUR:=2," +
                "AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,UMKEHREN:=0,RAMPE:=0," +
                "RAMPENLAENGE:=50,QUADRANT:=1,INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)",
            }));
        }

        [Test]
        public void RectPocket_WithCornerRadiusAndStepdown()
        {
            var input = new PocketLogic.RectPocketInput
            {
                CenterX = 0, CenterY = 0, SurfaceZ = 0,
                Width = 100, Height = 50,
                CornerRadius = 5,
                Angle = 30,
                Depth = 10,
                Stepdown = 3,
                ToolNr = 4,
            };
            var lines = PocketLogic.GenerateRect(input);
            Assert.That(lines[1], Does.Contain("RADIUS:=5"));
            Assert.That(lines[1], Does.Contain("WINKEL:=30"));
            Assert.That(lines[1], Does.Contain("ZUTIEFE:=3"));
            Assert.That(lines[1], Does.Contain("TIEFE:=-10"));
        }

        [Test]
        public void RectPocket_NegativeCornerRadiusClampedToZero()
        {
            var input = new PocketLogic.RectPocketInput
            {
                CenterX = 0, CenterY = 0, SurfaceZ = 0,
                Width = 100, Height = 50,
                CornerRadius = -5,
                Depth = 10,
                ToolNr = 4,
            };
            var lines = PocketLogic.GenerateRect(input);
            Assert.That(lines[1], Does.Contain("RADIUS:=0"));
        }

        // -------------------------------------------------------------
        // CIRCULAR POCKET (_Kreistasche_V5)
        // -------------------------------------------------------------

        [Test]
        public void CircPocket_BasicSnapshot()
        {
            var input = new PocketLogic.CircPocketInput
            {
                CenterX = 200, CenterY = 100, SurfaceZ = 19,
                Radius = 25, Depth = 10, Stepdown = 0,
                ToolNr = 4,
            };
            var lines = PocketLogic.GenerateCirc(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Kreistasche_V5(VAL X_Mitte:=200,Y_Mitte:=100,Radius:=25,Tiefe:=-10," +
                "Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)",
            }));
        }

        [Test]
        public void CircPocket_WithStepdown()
        {
            var input = new PocketLogic.CircPocketInput
            {
                CenterX = 0, CenterY = 0, SurfaceZ = 0,
                Radius = 10, Depth = 8, Stepdown = 2,
                ToolNr = 4,
            };
            var lines = PocketLogic.GenerateCirc(input);
            Assert.That(lines[1], Does.Contain("Zustellung:=2"));
            Assert.That(lines[1], Does.Contain("Tiefe:=-8"));
        }

        // ---- ESMD depth reference ---------------------------------------
        // From the file Leo corrected at the machine on 2026-08-10: a pocket that
        // cuts THROUGH carries TIEFE:=-0.2 with ESMD:=1, i.e. the depth counts up
        // from the table, not down from the part. ESMD was the only parameter that
        // differed besides the depth itself.
        [Test]
        public void RectPocket_DepthFromTable_SetsEsmdOne()
        {
            var input = new PocketLogic.RectPocketInput
            {
                CenterX = 233.5301, CenterY = 402, Width = 300, Height = 280,
                CornerRadius = 10, Angle = 0,
                Depth = 0.2, Stepdown = 9.5, ToolNr = 10, FeedFactor = 1.0,
                DepthFromTable = true,
            };
            var lines = PocketLogic.GenerateRect(input);
            Assert.That(lines[1], Does.Contain("TIEFE:=-0.2"));
            Assert.That(lines[1], Does.Contain("ZUTIEFE:=9.5"));
            Assert.That(lines[1], Does.Contain("ESMD:=1"));
        }

        // The default must stay top-referenced, or every existing definition
        // silently changes meaning.
        [Test]
        public void RectPocket_DefaultDepthReference_IsTop()
        {
            var input = new PocketLogic.RectPocketInput
            {
                CenterX = 0, CenterY = 0, Width = 100, Height = 50,
                Depth = 5, ToolNr = 4, FeedFactor = 1.0,
            };
            var lines = PocketLogic.GenerateRect(input);
            Assert.That(lines[1], Does.Contain("TIEFE:=-5"));
            Assert.That(lines[1], Does.Contain("ESMD:=0"));
        }
    }
}
