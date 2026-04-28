using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class CircPathLogicTests
    {
        [Test]
        public void FullCircle_BasicSnapshot()
        {
            var input = new CircPathLogic.CircPathInput
            {
                CenterX = 100, CenterY = 50, SurfaceZ = 19,
                Radius = 25, RadiusCorr = 0,
                Depth = 10, Stepdown = 0, Angle = 360,
                ToolNr = 4, ToolType = "WZF", FeedFactor = 1.0,
            };
            var lines = CircPathLogic.Generate(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Kreisbahn_V5(VAL X_Mitte:=100,Y_Mitte:=50,Tiefe:=9,ZuTiefe:=0," +
                "Radius:=25,Radiuskorrektur:=0,AB:=1,Aufmass:=0,Bearb_umkehren:=1," +
                "Winkel:=360,ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)",
            }));
        }

        [Test]
        public void RadiusCorr_Negative_PreservedInOutput()
        {
            var input = new CircPathLogic.CircPathInput
            {
                CenterX = 0, CenterY = 0, SurfaceZ = 0,
                Radius = 25, RadiusCorr = -1, Depth = 10, Angle = 360,
                ToolNr = 4,
            };
            var lines = CircPathLogic.Generate(input);
            Assert.That(lines[1], Does.Contain("Radiuskorrektur:=-1"));
        }

        [Test]
        public void DefaultsApplied_AngleZeroBecomes360()
        {
            var input = new CircPathLogic.CircPathInput
            {
                CenterX = 0, CenterY = 0, SurfaceZ = 0,
                Radius = 25, Depth = 10, Angle = 0,
                ToolNr = 4,
            };
            var lines = CircPathLogic.Generate(input);
            Assert.That(lines[1], Does.Contain("Winkel:=360"));
        }
    }

    [TestFixture]
    public class BlumHingeLogicTests
    {
        [Test]
        public void TwoHinges_Snapshot()
        {
            var input = new BlumHingeLogic.BlumHingeInput
            {
                Positions = new[]
                {
                    new BlumHingeLogic.HingePosition { X = 22.5, Y = 100, SurfaceZ = 19 },
                    new BlumHingeLogic.HingePosition { X = 22.5, Y = 500, SurfaceZ = 19 },
                },
                Distance = 22.5, Side = 0,
                CupDiameter = 35, CupDepth = 12.8,
                DowelDiameter = 8, DowelDepth = 13,
                ToolNr = 1,
            };
            var lines = BlumHingeLogic.Generate(input);

            Assert.That(lines.Count, Is.EqualTo(3)); // tool call + 2 hinges
            Assert.That(lines[0], Is.EqualTo("WZB (1,_VE,_V*1,_VA,_SD,0,'')"));
            Assert.That(lines[1], Is.EqualTo(
                "CALL _Topf_V5(VAL SEITE:=0,DISTANCE:=22.5,POS1:=100," +
                "POS2:=0,POS3:=0,POS4:=0,A:=9.5,B:=45,TOPF_D:=35,TOPF_T:=-12.8," +
                "DUEBEL_D:=8,DUEBEL_T:=-13," +
                "ESX1:=0,ESX2:=0,ESX3:=0,ESX4:=0,ESY1:=0,ESY2:=0,ESY3:=0,ESY4:=0," +
                "USE2:=0,USE3:=0,USE4:=0)"));
            Assert.That(lines[2], Does.Contain("POS1:=500"));
        }

        [Test]
        public void CupDepthAlwaysOutputAsNegative()
        {
            // User passes positive cup depth → macro should emit -cupDepth
            var input = new BlumHingeLogic.BlumHingeInput
            {
                Positions = new[] { new BlumHingeLogic.HingePosition { Y = 100, SurfaceZ = 0 } },
                Distance = 22.5, CupDiameter = 35, CupDepth = 12.8,
                DowelDiameter = 8, DowelDepth = 13,
                ToolNr = 1,
            };
            var lines = BlumHingeLogic.Generate(input);
            Assert.That(lines[1], Does.Contain("TOPF_T:=-12.8"));
            Assert.That(lines[1], Does.Contain("DUEBEL_T:=-13"));
        }

        [Test]
        public void Side_BackEdge_PreservedInOutput()
        {
            var input = new BlumHingeLogic.BlumHingeInput
            {
                Positions = new[] { new BlumHingeLogic.HingePosition { Y = 100, SurfaceZ = 0 } },
                Distance = 22.5, Side = 1, CupDiameter = 35, CupDepth = 12.8,
                ToolNr = 1,
            };
            var lines = BlumHingeLogic.Generate(input);
            Assert.That(lines[1], Does.Contain("SEITE:=1"));
        }
    }

    [TestFixture]
    public class FormatCutLogicTests
    {
        [Test]
        public void XCut_AtFixedY_Snapshot()
        {
            var input = new FormatCutLogic.FormatCutInput
            {
                IsXCut = true,
                Positions = new[]
                {
                    new FormatCutLogic.CutPosition { X = 0, Y = 100, SurfaceZ = 19 },
                },
                Thickness = 19, Kw = 0, LengthOverride = 0, ToolNr = 2,
            };
            var lines = FormatCutLogic.Generate(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "WZS (2,_VE,_V*0.3,_VA,_SD,0,'')",
                "CALL _saege_x_V7(VAL SX:=0,SY:=100,SZ:=0,EX:=0,EZ:=-0.2,BL:=2," +
                "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,K:=2,KW:=0," +
                "BH:=0,RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)",
            }));
        }

        [Test]
        public void YCut_WithMiterAngle()
        {
            var input = new FormatCutLogic.FormatCutInput
            {
                IsXCut = false,
                Positions = new[]
                {
                    new FormatCutLogic.CutPosition { X = 200, Y = 0, SurfaceZ = 19 },
                },
                Thickness = 19, Kw = 45, ToolNr = 2,
            };
            var lines = FormatCutLogic.Generate(input);
            Assert.That(lines[1], Does.StartWith("CALL _saege_y_V7"));
            Assert.That(lines[1], Does.Contain("SX:=200"));
            Assert.That(lines[1], Does.Contain("SY:=0"));
            Assert.That(lines[1], Does.Contain("KW:=45"));
        }

        [Test]
        public void LengthOverride_ReplacesEXFromZeroToValue()
        {
            var input = new FormatCutLogic.FormatCutInput
            {
                IsXCut = true,
                Positions = new[] { new FormatCutLogic.CutPosition { Y = 0, SurfaceZ = 0 } },
                Thickness = 19, LengthOverride = 1500, ToolNr = 2,
            };
            var lines = FormatCutLogic.Generate(input);
            Assert.That(lines[1], Does.Contain("EX:=1500"));
        }
    }

    [TestFixture]
    public class FixchipLogicTests
    {
        [Test]
        public void ThreeClamps_OneLineEach_Snapshot()
        {
            var input = new FixchipLogic.FixchipInput
            {
                Positions = new[]
                {
                    new DrillLogic.Point2dz(100, 100, 19),
                    new DrillLogic.Point2dz(500, 100, 19),
                    new DrillLogic.Point2dz(900, 100, 19),
                },
                Angle = 0,
            };
            var lines = FixchipLogic.Generate(input);

            Assert.That(lines, Is.EquivalentTo(new[]
            {
                "Fixchip_K (100,100,19,0)",
                "Fixchip_K (500,100,19,0)",
                "Fixchip_K (900,100,19,0)",
            }));
        }

        [Test]
        public void NonZeroAngle_PreservedInOutput()
        {
            var input = new FixchipLogic.FixchipInput
            {
                Positions = new[] { new DrillLogic.Point2dz(0, 0, 0) },
                Angle = 45,
            };
            var lines = FixchipLogic.Generate(input);
            Assert.That(lines[0], Is.EqualTo("Fixchip_K (0,0,0,45)"));
        }
    }
}
