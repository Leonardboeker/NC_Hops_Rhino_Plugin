using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class StockSimLogicTests
    {
        private static StockSimLogic.StockSimPlan Parse(
            string body, Dictionary<int, double> dias = null)
        {
            // Realistic wrapper: header ;DZ=0 lies, VARS has the truth (C4)
            string content = string.Join("\r\n", new[]
            {
                ";MAKROTYP=0", ";DZ=0.000",
                "VARS",
                "   DX := 600;*VAR*Dimension X",
                "   DY := 400;*VAR*Dimension Y",
                "   DZ := 19;*VAR*Dimension Z",
                "START",
                "Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)",
            }) + "\r\n" + body;
            return StockSimLogic.Parse(content, dias, 8.0);
        }

        [Test]
        public void StockDimensions_ComeFromVarsNotHeader()
        {
            var plan = Parse("");
            Assert.That(plan.Dx, Is.EqualTo(600));
            Assert.That(plan.Dy, Is.EqualTo(400));
            Assert.That(plan.Dz, Is.EqualTo(19));
        }

        [Test]
        public void Drill_BecomesCylinderWithMacroDiameterAndZ()
        {
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "Bohrung (100,50,19,9,5,0,0,0,0,0,0,0)");

            Assert.That(plan.Cuts.Count, Is.EqualTo(1));
            var c = plan.Cuts[0];
            Assert.That(c.Kind, Is.EqualTo(StockSimLogic.SolidKind.Cylinder));
            Assert.That(c.X1, Is.EqualTo(100));
            Assert.That(c.Y1, Is.EqualTo(50));
            Assert.That(c.Width, Is.EqualTo(5));
            Assert.That(c.Z1, Is.EqualTo(19));
            Assert.That(c.Z0, Is.EqualTo(9)); // 10 mm deep
            Assert.That(plan.StepLabels.Count, Is.EqualTo(1));
        }

        [Test]
        public void SawCut_IsSquareEndedSlab_ThroughCutReachesZero()
        {
            var plan = Parse(
                "WZS (10,_VE,_V*0.3,_VA,_SD,0,'')\r\n" +
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=100,X2:=600,Y2:=100,NB:=3.2," +
                "Tiefe:=0,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            var c = plan.Cuts.Single();
            Assert.That(c.Kind, Is.EqualTo(StockSimLogic.SolidKind.Slab));
            Assert.That(c.RoundEnds, Is.False, "saw kerf ends are square");
            Assert.That(c.Width, Is.EqualTo(3.2));
            Assert.That(c.Z0, Is.EqualTo(0));   // through cut
            Assert.That(c.Z1, Is.EqualTo(19));
        }

        [Test]
        public void FreeSlot_MilledWithWzf_HasRoundEnds()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _nuten_frei_v5(VAL X1:=50,Y1:=50,X2:=250,Y2:=50,NB:=8," +
                "Tiefe:=11,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            var c = plan.Cuts.Single();
            Assert.That(c.RoundEnds, Is.True, "router slot ends are round");
            Assert.That(c.Z0, Is.EqualTo(11)); // 19 - 8 deep
        }

        [Test]
        public void NegativeTiefe_MeansDepthBelowTop_KorpusPanelConvention()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=50,X2:=600,Y2:=50,NB:=8," +
                "Tiefe:=-8,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            Assert.That(plan.Cuts.Single().Z0, Is.EqualTo(11)); // 19 + (-8)
        }

        [Test]
        public void AngledSawCut_GetsVerticalApproximationWarning()
        {
            var plan = Parse(
                "WZS (10,_VE,_V*0.3,_VA,_SD,0,'')\r\n" +
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=100,X2:=600,Y2:=100,NB:=3.2," +
                "Tiefe:=0,LAGE:=45,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            Assert.That(plan.Warnings.Any(w => w.Contains("LAGE=45")), Is.True);
        }

        [Test]
        public void GrooveX_SpansPanelAtArand_ShortenedByAlinksArechts()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Nuten_X_V5(VAL NB:=8.25,NT:=10,EBENE:=0,ARAND:=60," +
                "ALINKS:=15,ARECHTS:=25,RK:=0,ESMD:=1)");

            var c = plan.Cuts.Single();
            Assert.That(c.Y1, Is.EqualTo(60));       // ARAND = position
            Assert.That(c.Y2, Is.EqualTo(60));
            Assert.That(c.X1, Is.EqualTo(15));       // ALINKS
            Assert.That(c.X2, Is.EqualTo(575));      // DX - ARECHTS
            Assert.That(c.Z0, Is.EqualTo(9));        // DZ - NT
            Assert.That(c.Width, Is.EqualTo(8.25));
        }

        [Test]
        public void MillChain_UsesToolDiameterFromMap_UnknownToolWarnsOnce()
        {
            var body =
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "SP (10,10,7,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)\r\n" +
                "G01 (110,10,0,0,0,2)\r\n" +
                "G01 (110,60,0,0,0,2)";

            var known = Parse(body, new Dictionary<int, double> { { 4, 12.0 } });
            Assert.That(known.Cuts.Count, Is.EqualTo(2));
            Assert.That(known.Cuts.All(c => c.Width == 12.0), Is.True);
            Assert.That(known.Cuts.All(c => c.Z0 == 7), Is.True, "SP Z persists for the chain");
            Assert.That(known.Cuts.All(c => c.StepIndex == 1), Is.True, "one chain = one step");
            Assert.That(known.Warnings, Is.Empty);

            var unknown = Parse(body);
            Assert.That(unknown.Cuts.All(c => c.Width == 8.0), Is.True, "default diameter");
            Assert.That(unknown.Warnings.Count(w => w.Contains("tool 4")), Is.EqualTo(1),
                "diameter warning appears once per tool, not per segment");
        }

        [Test]
        public void CircPocket_IsCylinderAtDoubleRadius()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Kreistasche_V5(VAL X_Mitte:=200,Y_Mitte:=150,Radius:=17.5," +
                "Tiefe:=6,Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)");

            var c = plan.Cuts.Single();
            Assert.That(c.Kind, Is.EqualTo(StockSimLogic.SolidKind.Cylinder));
            Assert.That(c.Width, Is.EqualTo(35));
            Assert.That(c.Z0, Is.EqualTo(6));
        }

        [Test]
        public void CircPath_FullCircle_IsOneSmoothRing()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Kreisbahn_V5(VAL X_Mitte:=100,Y_Mitte:=100,Tiefe:=9,ZuTiefe:=0," +
                "Radius:=40,Radiuskorrektur:=0,AB:=1,Aufmass:=0,Bearb_umkehren:=1," +
                "Winkel:=360,ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

            var c = plan.Cuts.Single();
            Assert.That(c.Kind, Is.EqualTo(StockSimLogic.SolidKind.Ring));
            Assert.That(c.PathRadius, Is.EqualTo(40));
            Assert.That(c.Z0, Is.EqualTo(9), "Tiefe is absolute cut Z");
        }

        [Test]
        public void CircPath_PartialArc_TessellatesAt5Degrees()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Kreisbahn_V5(VAL X_Mitte:=100,Y_Mitte:=100,Tiefe:=9,ZuTiefe:=0," +
                "Radius:=40,Radiuskorrektur:=0,AB:=1,Aufmass:=0,Bearb_umkehren:=1," +
                "Winkel:=90,ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

            Assert.That(plan.Cuts.Count, Is.GreaterThanOrEqualTo(18), "90° / 5° = 18 segments");
            Assert.That(plan.Cuts.All(c => c.StepIndex == 1), Is.True);
        }

        [Test]
        public void RectPocket_CornerRadius_AtLeastToolRadius()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Rechteck_V7(VAL X_MITTE:=100,Y_MITTE:=100,LAENGE:=80,BREITE:=50," +
                "RADIUS:=0,WINKEL:=0,TIEFE:=9,ZUTIEFE:=0,RADIUSKORREKTUR:=2," +
                "AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,UMKEHREN:=0,RAMPE:=0," +
                "RAMPENLAENGE:=50,QUADRANT:=1,INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)",
                new Dictionary<int, double> { { 4, 12.0 } });

            Assert.That(plan.Cuts.Single().CornerRadius, Is.EqualTo(6.0),
                "the machined pocket can never be sharper than the tool radius");
        }

        [Test]
        public void DrillRow_BixIsFirstHole_IncrementsAndUseFlagsRespected()
        {
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Bohgx_V5(VAL SPY:=100,BIX:=50,BIIX:=32,BIIIX:=0,BIIIIX:=32," +
                "SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1," +
                "USE2:=1,USE3:=0,USE4:=1)");

            var xs = plan.Cuts.Select(c => c.X1).ToArray();
            Assert.That(xs, Is.EqualTo(new[] { 50.0, 82.0, 114.0 }), "50, +32, (skip), +32");
            Assert.That(plan.Cuts.All(c => c.Y1 == 100), Is.True);
            Assert.That(plan.Cuts.All(c => c.Width == 5), Is.True);
            Assert.That(plan.Cuts.All(c => c.Z0 == 6), Is.True, "T is absolute cut Z");
        }

        [Test]
        public void BlumHinge_CupPlusTwoDowelsPerPosition()
        {
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Topf_V5(VAL SEITE:=0,DISTANCE:=22.5,POS1:=100,POS2:=0,POS3:=0,POS4:=0," +
                "A:=9.5,B:=45,TOPF_D:=35,TOPF_T:=-12.8,DUEBEL_D:=8,DUEBEL_T:=-13," +
                "ESX1:=0,ESX2:=0,ESX3:=0,ESX4:=0,ESY1:=0,ESY2:=0,ESY3:=0,ESY4:=0," +
                "USE2:=0,USE3:=0,USE4:=0)");

            Assert.That(plan.Cuts.Count, Is.EqualTo(3), "cup + 2 dowels");
            var cup = plan.Cuts[0];
            Assert.That(cup.Width, Is.EqualTo(35));
            Assert.That(cup.X1, Is.EqualTo(22.5));
            Assert.That(cup.Y1, Is.EqualTo(100));
            Assert.That(cup.Z0, Is.EqualTo(19 - 12.8).Within(1e-9));
            var dowelYs = plan.Cuts.Skip(1).Select(c => c.Y1).OrderBy(y => y).ToArray();
            Assert.That(dowelYs, Is.EqualTo(new[] { 77.5, 122.5 }));
            Assert.That(plan.Cuts.Skip(1).All(c => c.X1 == 32.0), Is.True, "DISTANCE + A");
        }

        [Test]
        public void FormatCut_FullLengthKerfSlab()
        {
            var plan = Parse(
                "WZS (10,_VE,_V*0.3,_VA,_SD,0,'')\r\n" +
                "CALL _saege_x_V7(VAL SX:=0,SY:=250,SZ:=0,EX:=0,EZ:=-0.2,BL:=2," +
                "EINPASSEN:=0,EL:=0,AL:=0,PARALLEL:=0,K:=2,KW:=0,BH:=0," +
                "RITZVERSATZ:=0.05,ESZ:=0,ESXY1:=1,ESX:=3)");

            var c = plan.Cuts.Single();
            Assert.That(c.Kind, Is.EqualTo(StockSimLogic.SolidKind.Slab));
            Assert.That(c.Y1, Is.EqualTo(250));
            Assert.That(c.X1, Is.EqualTo(0));
            Assert.That(c.X2, Is.EqualTo(600), "full panel DX");
            Assert.That(c.Z0, Is.EqualTo(-0.2), "EZ overshoot below the plate");
        }

        [Test]
        public void Arg_HandlesMachineExpressionsAndUnits()
        {
            // Machine-generated files contain "SPX:=19+41" and "D:=5mm"
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Bohgy_V5(VAL SPX:=19+41,BIY:=37,BIIY:=0,BIIIY:=0,BIIIIY:=0," +
                "SPIEGELN:=0,T:=-13,D:=5mm,TLF:=10,INKREMENT:=0,ESXY:=1,ESD:=0," +
                "USE2:=0,USE3:=0,USE4:=0)");

            var c = plan.Cuts.Single();
            Assert.That(c.X1, Is.EqualTo(60), "19+41 evaluated");
            Assert.That(c.Width, Is.EqualTo(5), "unit 'mm' stripped");
            Assert.That(c.Z0, Is.EqualTo(6), "T:=-13 = depth below the 19 mm top");
        }

        [Test]
        public void Fixchip_RemovesNoMaterial_AndRaisesNoWarning()
        {
            var plan = Parse("/CALL Fixchip_K ( VAL SPX:=0,SPY:=60,SPZ:=9.5,WKLXY:=0)");
            Assert.That(plan.Cuts, Is.Empty);
            Assert.That(plan.Warnings, Is.Empty);
        }

        [Test]
        public void UnknownMacro_WarnsOnceInsteadOfSilentlyMissing()
        {
            var plan = Parse(
                "CALL _Fasenschnitt_V9 ( VAL POSX:=100,POSY:=0)\r\n" +
                "CALL _Fasenschnitt_V9 ( VAL POSX:=200,POSY:=0)");
            Assert.That(plan.Cuts, Is.Empty);
            Assert.That(plan.Warnings.Count(w => w.Contains("_Fasenschnitt_V9")), Is.EqualTo(1));
        }

        [Test]
        public void SkippableSlashPrefix_StillRemovesMaterial()
        {
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "/Bohrung (100,50,19,9,5,0,0,0,0,0,0,0)");
            Assert.That(plan.Cuts.Count, Is.EqualTo(1));
        }

        [Test]
        public void StepIndices_AreSequentialAcrossMixedOperations()
        {
            var plan = Parse(
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "Bohrung (100,50,19,9,5,0,0,0,0,0,0,0)\r\n" +
                "Bohrung (200,50,19,9,5,0,0,0,0,0,0,0)\r\n" +
                "WZS (10,_VE,_V*0.3,_VA,_SD,0,'')\r\n" +
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=100,X2:=600,Y2:=100,NB:=3.2," +
                "Tiefe:=0,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)");

            Assert.That(plan.StepLabels.Count, Is.EqualTo(3));
            Assert.That(plan.Cuts.Select(c => c.StepIndex).ToArray(),
                Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }
}
