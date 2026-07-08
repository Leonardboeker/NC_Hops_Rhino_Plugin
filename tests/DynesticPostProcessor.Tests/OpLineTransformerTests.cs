using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    /// <summary>
    /// C2 regression: nesting placements must reach the emitted coordinates.
    /// </summary>
    [TestFixture]
    public class OpLineTransformerTests
    {
        private static OpLineTransformer.Placement Move(double tx, double ty, double rot = 0)
        {
            return new OpLineTransformer.Placement { Tx = tx, Ty = ty, RotationDeg = rot };
        }

        [Test]
        public void Drill_TranslatesXY_KeepsZAndDiameter()
        {
            var r = OpLineTransformer.Apply(
                new[] { "Bohrung (100,50,19,9,8,0,0,0,0,0,0,0)" }, Move(1000, 500));
            Assert.That(r.Error, Is.Null);
            Assert.That(r.Lines[0], Is.EqualTo("Bohrung (1100,550,19,9,8,0,0,0,0,0,0,0)"));
        }

        [Test]
        public void ContourBlock_SPMovesAndArcCenterMove()
        {
            var lines = new[]
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "G03M (0,10,0,0,0,0,0,2,0)",
                "EP (0,_ANF,0)",
            };
            var r = OpLineTransformer.Apply(lines, Move(200, 300));
            Assert.That(r.Error, Is.Null);
            Assert.That(r.Lines[0], Is.EqualTo(lines[0]), "tool call untouched");
            Assert.That(r.Lines[1], Does.StartWith("SP (200,300,-10,"));
            Assert.That(r.Lines[2], Is.EqualTo("G01 (300,300,0,0,0,2)"));
            // G03M: endpoint (0,10)->(200,310); center (0,0)->(200,300)
            Assert.That(r.Lines[3], Is.EqualTo("G03M (200,310,0,200,300,0,0,2,0)"));
            Assert.That(r.Lines[4], Is.EqualTo(lines[4]), "EP untouched");
        }

        [Test]
        public void FreeSlot_NamedX1Y1X2Y2Translate()
        {
            var r = OpLineTransformer.Apply(new[]
            {
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=50,X2:=100,Y2:=50,NB:=8,Tiefe:=-10,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)",
            }, Move(10, 20));
            Assert.That(r.Error, Is.Null);
            Assert.That(r.Lines[0], Does.Contain("X1:=10"));
            Assert.That(r.Lines[0], Does.Contain("Y1:=70"));
            Assert.That(r.Lines[0], Does.Contain("X2:=110"));
            Assert.That(r.Lines[0], Does.Contain("Y2:=70"));
            Assert.That(r.Lines[0], Does.Contain("Tiefe:=-10"), "depth untouched");
        }

        [Test]
        public void Pockets_XMitteTranslates_BothCasings()
        {
            var r = OpLineTransformer.Apply(new[]
            {
                "CALL _Rechteck_V7(VAL X_MITTE:=300,Y_MITTE:=200,LAENGE:=80,BREITE:=40,RADIUS:=0,WINKEL:=0,TIEFE:=-10,ZUTIEFE:=0,RADIUSKORREKTUR:=2,AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,UMKEHREN:=0,RAMPE:=0,RAMPENLAENGE:=50,QUADRANT:=1,INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)",
                "CALL _Kreistasche_V5(VAL X_Mitte:=10,Y_Mitte:=20,Radius:=25,Tiefe:=-9,Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)",
            }, Move(50, 60));
            Assert.That(r.Error, Is.Null);
            Assert.That(r.Lines[0], Does.Contain("X_MITTE:=350"));
            Assert.That(r.Lines[0], Does.Contain("Y_MITTE:=260"));
            Assert.That(r.Lines[0], Does.Contain("LAENGE:=80"), "size untouched");
            Assert.That(r.Lines[1], Does.Contain("X_Mitte:=60"));
            Assert.That(r.Lines[1], Does.Contain("Y_Mitte:=80"));
        }

        [Test]
        public void Fixchip_SPXSPYTranslate()
        {
            var r = OpLineTransformer.Apply(new[]
            {
                "/CALL Fixchip_K ( VAL SPX:=0,SPY:=60,SPZ:=9.5,WKLXY:=0)",
            }, Move(100, 200));
            Assert.That(r.Error, Is.Null);
            Assert.That(r.Lines[0], Does.Contain("SPX:=100"));
            Assert.That(r.Lines[0], Does.Contain("SPY:=260"));
            Assert.That(r.Lines[0], Does.Contain("SPZ:=9.5"), "Z untouched");
        }

        [Test]
        public void RotatedPlacement_Refused()
        {
            var r = OpLineTransformer.Apply(
                new[] { "Bohrung (0,0,19,9,8,0,0,0,0,0,0,0)" }, Move(0, 0, rot: 90));
            Assert.That(r.Error, Is.Not.Null);
            Assert.That(r.Error, Does.Contain("rotated"));
            Assert.That(r.Lines, Is.Null);
        }

        [Test]
        public void AxisBoundMacros_Refused()
        {
            var groove = OpLineTransformer.Apply(new[]
            {
                "CALL _Nuten_X_V5(VAL NB:=8,NT:=10,EBENE:=0,ARAND:=10,ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)",
            }, Move(10, 10));
            Assert.That(groove.Error, Is.Not.Null);
            Assert.That(groove.Error, Does.Contain("edge-referenced"));

            var row = OpLineTransformer.Apply(new[]
            {
                "CALL _Bohgx_V5(VAL SPY:=100,BIX:=32,BIIX:=0,BIIIX:=0,BIIIIX:=0,SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1,USE2:=1,USE3:=1,USE4:=1)",
            }, Move(10, 10));
            Assert.That(row.Error, Is.Not.Null);
            Assert.That(row.Error, Does.Contain("drill-row"));
        }

        [Test]
        public void FullRotationMultiple360_TreatedAsUnrotated()
        {
            var r = OpLineTransformer.Apply(
                new[] { "Bohrung (1,2,19,9,8,0,0,0,0,0,0,0)" }, Move(10, 10, rot: 360));
            Assert.That(r.Error, Is.Null);
        }
    }
}
