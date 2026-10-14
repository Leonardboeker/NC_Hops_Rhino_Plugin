using NUnit.Framework;
using WallabyHop;
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
            // DZ=19, 8 mm drill 30 deep from the top → 30 > 19 + 5 → warning
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,8,-30,10,0,0,0,0,0,0,0)",
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
                "Bohrung (0,0,8,-23,10,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.ZWarnings.Count, Is.EqualTo(0));
        }

        [Test]
        public void HingeCupOn19mm_NoFalseDepthWarning()
        {
            // 35 mm cup (type 30), 13 deep. The pre-machine-order reader took
            // 35 - (-13) = 48 mm as the depth and warned on every cup.
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "Bohrung (100,37,35,-13,30,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.ZWarnings, Is.Empty);
        }

        [Test]
        public void DrillDepthFromTable_ThroughHoleIsFine_DeepSpoilboardCutWarns()
        {
            // Field 9 = 1: the depth is a Z above the table. -0.2 = through by
            // 0.2 mm (fine); -6 = 6 mm into the spoilboard = 25 from the top (> 24).
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                NcDrill.DrillLine(50, 50, 8, 0.2, depthFromTable: true),
                NcDrill.DrillLine(80, 50, 8, 6, depthFromTable: true),
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.ZWarnings.Count, Is.EqualTo(1));
            Assert.That(r.ZWarnings[0], Does.StartWith("L3: Drill depth=25.0"));
        }

        [Test]
        public void SkippableDrill_WithExpressionPosition_IsStillDepthChecked()
        {
            // Machine files write "/Bohrung (DX-37,…)": no XY number, but the
            // depth fields are plain numbers and must still be checked.
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "/Bohrung (DX-37,37,8,-30,10,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DrillCount, Is.EqualTo(1));
            Assert.That(r.ZWarnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeepestZ_TracksMostNegativeAcrossSPAndDrills()
        {
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,8,-15,10,0,0,0,0,0,0,0)",
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

        [Test]
        public void CallMacro_MixedCaseTiefe_SawSlotCircFamily_NoLongerBlind()
        {
            // C6 regression: _nuten_frei_v5 / _Kreisbahn_V5 / _Kreistasche_V5
            // emit "Tiefe:=" (mixed case) — the old case-sensitive scan missed
            // every saw cut, slot and circular pocket.
            var content = string.Join("\n", new[]
            {
                "WZS (2,_VE,_V*0.3,_VA,_SD,0,'')",
                "CALL _nuten_frei_v5(VAL X1:=0,Y1:=0,X2:=100,Y2:=0,NB:=3.2,Tiefe:=-22,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DeepestZ, Is.EqualTo(-22).Within(1e-9));
        }

        [Test]
        public void CallMacro_ZUTIEFE_DoesNotFalseMatchTIEFE()
        {
            // ZUTIEFE:=0 must not clobber the real TIEFE:=-10 reading
            var content = "CALL _Rechteck_V7(VAL ZUTIEFE:=0,TIEFE:=-10,RADIUS:=0)";
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DeepestZ, Is.EqualTo(-10).Within(1e-9));
        }

        [Test]
        public void GrooveNT_ContributesToDeepestZ_AsNegative()
        {
            var content = "CALL _Nuten_X_V5(VAL NB:=8,NT:=10,EBENE:=0,ARAND:=10,ALINKS:=0,ARECHTS:=0,RK:=0,ESMD:=1)";
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.DeepestZ, Is.EqualTo(-10).Within(1e-9));
        }

        // -------------------------------------------------------------
        // DZ FROM VARS BLOCK (machine files have ;DZ=0 header + VARS value)
        // -------------------------------------------------------------

        [Test]
        public void VarsDZ_OverridesZeroHeaderDZ()
        {
            // Mirrors reference-hops/files/HZK_Boden_Deckel_602x278.hop:
            // header says ;DZ=0, the truth is in VARS.
            var content = string.Join("\n", new[]
            {
                ";DZ=0",
                "VARS",
                "   DX := 902;*VAR*Breite X",
                "   DY := 278;*VAR*Breite Y",
                "   DZ := 19;Dicke Z",
                "START",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (0,0,8,-30,10,0,0,0,0,0,0,0)",  // 30mm deep into 19mm plate
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.HeaderDZ, Is.EqualTo(19).Within(1e-9));
            Assert.That(r.ZWarnings.Count, Is.EqualTo(1),
                "depth check must fire — VARS DZ=19 + 5 allowance < 30");
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
                "Bohrung (0,0,8,-10,10,0,0,0,0,0,0,0)",
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
                "/CALL Fixchip_K ( VAL SPX:=100,SPY:=100,SPZ:=9.5,WKLXY:=0)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (100,100,8,-10,10,0,0,0,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings.Count, Is.EqualTo(1));
            Assert.That(r.FixchipWarnings[0], Does.Contain("Drill"));
            Assert.That(r.FixchipWarnings[0], Does.Contain("Fixchip"));
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.FixchipCount, Is.EqualTo(1));
        }

        [Test]
        public void FixchipCollision_DrillWithoutAbsolutePosition_IsNotChecked()
        {
            // An expression position (read as 0,0) and an anchored (25,25) would both
            // sit inside the clamp radius if taken as points; neither is one. The
            // depth check still runs: -30 on 19 mm warns.
            var content = string.Join("\n", new[]
            {
                ";DZ=19.000",
                "/CALL Fixchip_K ( VAL SPX:=10,SPY:=10,SPZ:=9.5,WKLXY:=0)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (DX-37,37,8,-10,10,0,0,0,0,0,0,0)",
                "Bohrung (25,25,8,-30,10,0,0,5,0,0,0,0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings, Is.Empty);
            Assert.That(r.DrillCount, Is.EqualTo(2));
            Assert.That(r.ZWarnings.Count, Is.EqualTo(1));
            Assert.That(r.ZWarnings[0], Does.StartWith("L5: Drill depth=30.0"));
        }

        [Test]
        public void FixchipCollision_DrillFarFromClamp_NoWarning()
        {
            // Clamp at (100,100); drill at (500,500) -> ~566 mm distance, way outside 25 mm
            var content = string.Join("\n", new[]
            {
                "/CALL Fixchip_K ( VAL SPX:=100,SPY:=100,SPZ:=9.5,WKLXY:=0)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "Bohrung (500,500,8,-10,10,0,0,0,0,0,0,0)",
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
                "/CALL Fixchip_K ( VAL SPX:=200,SPY:=200,SPZ:=9.5,WKLXY:=0)",
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
                "/CALL Fixchip_K ( VAL SPX:=305,SPY:=302,SPZ:=9.5,WKLXY:=0)",
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Rechteck_V7(VAL X_MITTE:=300,Y_MITTE:=300,LAENGE:=80,BREITE:=40,RADIUS:=0,WINKEL:=0,TIEFE:=-10,ZUTIEFE:=0,RADIUSKORREKTUR:=2,AB:=2,AUFMASS:=0,ANF:=_ANF,ABF:=_ANF,UMKEHREN:=0,RAMPE:=0,RAMPENLAENGE:=50,QUADRANT:=1,INTERPOL:=1,ESXY:=0,ESMD:=0,LASER:=0)",
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.FixchipWarnings, Has.Some.Contains("CALL"));
        }

        // ---- Part-frame guard -------------------------------------------
        // Real accident 2026-08-10: DX/DY declared 1470 x 471 while the slots were
        // drawn for a part 471 x 1470 -- run-out to Y=1520 put the saw a metre
        // beside the plate.
        [Test]
        public void Analyze_OpsFarOutsideDeclaredPart_IsError()
        {
            var content = string.Join("\r\n", new[]
            {
                "VARS",
                "   DX := 1470;*VAR*Dimension X",
                "   DY := 470.97;*VAR*Dimension Y",
                "   DZ := 19;*VAR*Dimension Z",
                "START",
                "CALL _nuten_frei_v5(VAL X1:=3.5,Y1:=-50,X2:=3.5,Y2:=1520,NB:=3.2,Tiefe:=-5,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)"
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(r.IsValid, Is.False);
            Assert.That(string.Join(" | ", r.Errors), Does.Contain("outside the declared part"));
        }

        // The normal 50 mm through-cut overshoot must pass untouched.
        [Test]
        public void Analyze_NormalRunOutOvershoot_IsNoError()
        {
            var content = string.Join("\r\n", new[]
            {
                "VARS",
                "   DX := 470.97;*VAR*Dimension X",
                "   DY := 1470;*VAR*Dimension Y",
                "   DZ := 19;*VAR*Dimension Z",
                "START",
                "CALL _nuten_frei_v5(VAL X1:=3.5,Y1:=-50,X2:=3.5,Y2:=1520,NB:=3.2,Tiefe:=-5,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)"
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(string.Join(" | ", r.Errors), Does.Not.Contain("outside the declared part"));
        }

        // ---- Degenerate contour ------------------------------------------
        // Real case, "Einlege Boeden 6 Stk.hop" 2026-08-10: SP and its single G01
        // carried identical XY, so the router plunged at one point and retracted.
        // Move count is 1, so the empty-block check stayed silent.
        [Test]
        public void Analyze_ContourMovesNeverLeaveStartPoint_IsError()
        {
            var content = string.Join("\r\n", new[]
            {
                "SP (1151.4847,437.5733,14,2,0,_ANF,0,0,0,0,1,0,2,0,0,0,0,0,0,0,0,0,0)",
                "G01 (1151.4847,437.5733,0,0,0,2)",
                "EP (0,_ANF,0)"
            });
            var r = HopAnalyzer.Analyze(content);
            // Warning, not error: a point plunge can be intentional, so the export
            // must still be allowed to run.
            Assert.That(string.Join(" | ", r.ZWarnings), Does.Contain("Contour goes nowhere"));
        }

        // A contour that genuinely travels must stay silent.
        [Test]
        public void Analyze_ContourThatTravels_IsNoError()
        {
            var content = string.Join("\r\n", new[]
            {
                "SP (362.2285,899.743,18.8,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)",
                "G01 (1417.9909,899.743,0,0,0,2)",
                "EP (0,_ANF,0)"
            });
            var r = HopAnalyzer.Analyze(content);
            Assert.That(string.Join(" | ", r.ZWarnings), Does.Not.Contain("Contour goes nowhere"));
        }
    }
}
