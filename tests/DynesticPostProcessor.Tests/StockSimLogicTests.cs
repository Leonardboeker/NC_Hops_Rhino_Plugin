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
        public void CircPath_TessellatesIntoMillSegments()
        {
            var plan = Parse(
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')\r\n" +
                "CALL _Kreisbahn_V5(VAL X_Mitte:=100,Y_Mitte:=100,Tiefe:=9,ZuTiefe:=0," +
                "Radius:=40,Radiuskorrektur:=0,AB:=1,Aufmass:=0,Bearb_umkehren:=1," +
                "Winkel:=360,ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)");

            Assert.That(plan.Cuts.Count, Is.GreaterThanOrEqualTo(12), "full circle tessellated");
            Assert.That(plan.Cuts.All(c => c.StepIndex == 1), Is.True);
            Assert.That(plan.Cuts.All(c => c.Z0 == 9), Is.True, "Tiefe is absolute cut Z");
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
                "CALL _saege_x_V7 ( VAL POSX:=100,POSY:=0)\r\n" +
                "CALL _saege_x_V7 ( VAL POSX:=200,POSY:=0)");
            Assert.That(plan.Cuts, Is.Empty);
            Assert.That(plan.Warnings.Count(w => w.Contains("_saege_x_V7")), Is.EqualTo(1));
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
