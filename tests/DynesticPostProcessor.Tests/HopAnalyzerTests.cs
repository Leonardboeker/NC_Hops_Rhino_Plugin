using NUnit.Framework;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class HopAnalyzerTests
    {
        // -------------------------------------------------------------
        // VALID PROGRAMS
        // -------------------------------------------------------------

        [Test]
        public void EmptyContent_ReportsValidWithZeroCounts()
        {
            var r = HopAnalyzer.Analyze("");
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.SpCount, Is.EqualTo(0));
            Assert.That(r.EpCount, Is.EqualTo(0));
            Assert.That(r.MoveCount, Is.EqualTo(0));
        }

        [Test]
        public void WellFormedContour_OneSPOneEP_NoErrors()
        {
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "EP (0,_ANF,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.SpCount, Is.EqualTo(1));
            Assert.That(r.EpCount, Is.EqualTo(1));
            Assert.That(r.MoveCount, Is.EqualTo(1));
            Assert.That(r.ToolChangeCount, Is.EqualTo(1));
        }

        // -------------------------------------------------------------
        // STRUCTURAL ERRORS
        // -------------------------------------------------------------

        [Test]
        public void EmptySPEPBlock_ReportedAsError()
        {
            var content = string.Join("\n", new[]
            {
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "EP (0,_ANF,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.EmptyBlocks, Is.EqualTo(1));
            Assert.That(r.Errors[0], Does.Contain("Empty SP/EP block"));
        }

        [Test]
        public void UnclosedSP_ReportedAsError()
        {
            var content = string.Join("\n", new[]
            {
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                // ← no EP
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.Errors[0], Does.Contain("SP never closed"));
        }

        [Test]
        public void EPWithoutSP_ReportedAsError()
        {
            var content = "EP (0,_ANF,0)";
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.Errors[0], Does.Contain("EP without preceding SP"));
        }

        [Test]
        public void MoveOutsideBlock_ReportedAsError()
        {
            var content = "G01 (100,0,0,0,0,2)";
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.Errors[0], Does.Contain("Move outside SP/EP block"));
        }

        // -------------------------------------------------------------
        // Z DEPTH WARNINGS
        // -------------------------------------------------------------

        [Test]
        public void DrillDepthExceedsDZPlusAllowance_TriggersWarning()
        {
            // DZ=19, drill depth = 30 (surf=0, cut=-30) → 30 > 19 + 5 → warning
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,0,-30,8,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.ZWarnings.Count, Is.EqualTo(1));
            Assert.That(r.ZWarnings[0], Does.Contain("Drill depth=30"));
        }

        [Test]
        public void DrillDepthWithinAllowance_NoWarning()
        {
            // DZ=19, drill depth = 23 (under 24 = 19+5 allowance)
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,0,-23,8,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.ZWarnings.Count, Is.EqualTo(0));
        }

        [Test]
        public void DeepestZ_TracksMostNegativeAcrossSPAndDrills()
        {
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,0,-15,8,0,0,0,0,0,0,0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (0,0,-18,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "EP (0,_ANF,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DeepestZ, Is.EqualTo(-18).Within(1e-9));
        }

        // -------------------------------------------------------------
        // CALL macro depth scanning
        // -------------------------------------------------------------

        [Test]
        public void CallMacro_TIEFEParam_ContributesToDeepestZ()
        {
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Rechteck_V7(VAL TIEFE:=-15.5,RADIUS:=0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DeepestZ, Is.EqualTo(-15.5).Within(1e-9));
            Assert.That(r.CallCount, Is.EqualTo(1));
        }

        // -------------------------------------------------------------
        // STATS
        // -------------------------------------------------------------

        [Test]
        public void PathLength_AccumulatedFromXYMoves()
        {
            // SP at (0,0), G01 to (100,0), G01 to (100,200) → length = 100 + 200 = 300
            var content = string.Join("\n", new[]
            {
                "SP (0,0,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (100,0,0,0,0,2)",
                "G01 (100,200,0,0,0,2)",
                "EP (0,_ANF,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.PathLength, Is.EqualTo(300).Within(1e-9));
        }

        [Test]
        public void Summary_FormatsCountsAndOKMarker()
        {
            var content = string.Join("\n", new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,0,-10,8,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.Summary, Does.StartWith("OK  "));
            Assert.That(r.Summary, Does.Contain("Lines=2"));
        }

        // -------------------------------------------------------------
        // FIXCHIP COLLISION (clamps + operations within ClampRadius)
        // -------------------------------------------------------------

        [Test]
        public void FixchipCollision_DrillRightOnClamp_TriggersError()
        {
            // Clamp at (100,100); drill at exact same XY -> 0 mm distance, well inside 25 mm radius
            var content = string.Join("\n", new[]
            {
                "Fixchip_K (100,100,19,0)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (100,100,19,9,8,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings.Count, Is.EqualTo(1));
            Assert.That(r.FixchipWarnings[0], Does.Contain("Drill"));
            Assert.That(r.FixchipWarnings[0], Does.Contain("Fixchip"));
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.FixchipCount, Is.EqualTo(1));
        }

        [Test]
        public void FixchipCollision_DrillFarFromClamp_NoWarning()
        {
            // Clamp at (100,100); drill at (500,500) -> ~566 mm distance, way outside 25 mm
            var content = string.Join("\n", new[]
            {
                "Fixchip_K (100,100,19,0)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (500,500,19,9,8,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings.Count, Is.EqualTo(0));
            Assert.That(r.IsValid, Is.True);
        }

        [Test]
        public void FixchipCollision_ContourSPInsideClamp_TriggersError()
        {
            // SP within clamp footprint
            var content = string.Join("\n", new[]
            {
                "Fixchip_K (200,200,19,0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "SP (210,205,-10,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (250,205,0,0,0,2)",
                "EP (0,_ANF,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            // SP is ~11 mm from clamp -> within 25 mm radius
            Assert.That(r.FixchipWarnings, Has.Some.Contains("SP"));
        }

        [Test]
        public void FixchipCollision_RectPocketCenterInsideClamp_TriggersError()
        {
            // Pocket centered on (300,300); clamp at (305,302) -> ~5.4 mm distance
            var content = string.Join("\n", new[]
            {
                "Fixchip_K (305,302,19,0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Rechteck_V7(VAL X_MITTE:=300,Y_MITTE:=300,LAENGE:=80,BREITE:=40,RADIUS:=0,WINKEL:=0,TIEFE:=-10,ZUTIEFE:=0,RADIUSKORREKTUR:=2,AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,UMKEHREN:=0,RAMPE:=0,RAMPENLAENGE:=50,QUADRANT:=1,INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings, Has.Some.Contains("CALL"));
        }
    }
}
