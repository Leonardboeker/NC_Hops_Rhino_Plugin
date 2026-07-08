using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using WallabyHop;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    /// <summary>
    /// Golden-file (approval) tests — the industry-standard pattern for post
    /// processors: each benchmark scenario assembles a COMPLETE .hop file via
    /// the same pure code path the exporters use, and diffs it byte-for-byte
    /// against an approved reference in tests/Golden/.
    ///
    /// When a test fails, the diff IS the protocol change. If the change is
    /// intended, re-approve by deleting the golden file and re-running (the
    /// test regenerates it and fails once with "regenerated — review + rerun");
    /// if unintended, you just caught a machining regression.
    /// </summary>
    [TestFixture]
    public class GoldenFileTests
    {
        private static string GoldenDir
        {
            get
            {
                // tests/DynesticPostProcessor.Tests/bin/Debug/net48 -> tests/.../Golden
                string dir = TestContext.CurrentContext.TestDirectory;
                return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "Golden"));
            }
        }

        private static void AssertMatchesGolden(string name, string content)
        {
            Directory.CreateDirectory(GoldenDir);
            string path = Path.Combine(GoldenDir, name + ".hop");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, content);
                Assert.Fail("Golden file regenerated: " + path
                    + " — review its content, then re-run the tests.");
            }

            string expected = File.ReadAllText(path);
            if (expected != content)
            {
                // Point at the first differing line for a fast diagnosis
                var expLines = expected.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var actLines = content.Split(new[] { "\r\n" }, StringSplitOptions.None);
                int n = Math.Min(expLines.Length, actLines.Length);
                for (int i = 0; i < n; i++)
                {
                    if (expLines[i] != actLines[i])
                    {
                        Assert.Fail("Golden mismatch '" + name + "' at line " + (i + 1)
                            + "\n  expected: " + expLines[i]
                            + "\n  actual:   " + actLines[i]);
                    }
                }
                Assert.Fail("Golden mismatch '" + name + "': line count "
                    + expLines.Length + " vs " + actLines.Length);
            }
        }

        // -------------------------------------------------------------
        // 1. DRILL-ONLY — System-32 shelf pins + connector holes
        // -------------------------------------------------------------
        [Test]
        public void Golden_01_DrillOnly()
        {
            var ops = new List<string>();
            var drill = DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = new[]
                {
                    new DrillLogic.Point2dz(37, 37, 19),
                    new DrillLogic.Point2dz(37, 69, 19),
                    new DrillLogic.Point2dz(37, 101, 19),
                    new DrillLogic.Point2dz(523, 37, 19),
                    new DrillLogic.Point2dz(523, 69, 19),
                },
                Depth = 13.0, Diameter = 5.0, Stepdown = 0, ToolNr = 1,
            });
            ops.AddRange(drill.Lines);

            string content = NcExport.AssembleFile(
                "bench_drill_only", 600, 400, 19, "7023K_681", null, ops);
            AssertMatchesGolden("bench_01_drill_only", content);
        }

        // -------------------------------------------------------------
        // 2. SAW + MITER — straight cut + 45deg miter + drill mix
        //    (also exercises the WZB-before-WZS bucket sort)
        // -------------------------------------------------------------
        [Test]
        public void Golden_02_SawMiterAndDrill()
        {
            var ops = new List<string>();

            var saw = SawLogic.Generate(new SawLogic.SawInput
            {
                Segments = new[]
                {
                    new SawLogic.LineSegment(
                        new DrillLogic.Point2dz(0, 100, 19),
                        new DrillLogic.Point2dz(800, 100, 19)),
                    new SawLogic.LineSegment(
                        new DrillLogic.Point2dz(0, 300, 19),
                        new DrillLogic.Point2dz(800, 300, 19)),
                },
                BladeAngles = new[] { 0.0, 45.0 },
                Length = 0, SawKerf = 3.2, Depth = 19.0,
                Side = 0, Extend = 20.0, ToolNr = 10,
            });
            ops.AddRange(saw.Lines);

            var drill = DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = new[] { new DrillLogic.Point2dz(400, 200, 19) },
                Depth = 10.0, Diameter = 8.0, Stepdown = 0, ToolNr = 1,
            });
            ops.AddRange(drill.Lines);

            string content = NcExport.AssembleFile(
                "bench_saw_miter", 800, 400, 19, "7023K_681", null, ops);
            AssertMatchesGolden("bench_02_saw_miter", content);
        }

        // -------------------------------------------------------------
        // 3. CONTOUR WITH ARCS — SP/G01/G03M blocks, lead-in/out, 2 passes
        // -------------------------------------------------------------
        [Test]
        public void Golden_03_ContourWithArcs()
        {
            var piece = new[]
            {
                ContourLogic.ContourSegment.Line(0, 0, 100, 0),
                ContourLogic.ContourSegment.Arc(100, 0, 110, 10, 100, 10, true, 1, 0, 0, 1),
                ContourLogic.ContourSegment.Line(110, 10, 110, 100),
            };
            var lines = ContourLogic.Generate(new ContourLogic.ContourInput
            {
                Pieces = new[] { piece },
                SurfaceZ = 19, Depth = 19, Passes = 2, Overcut = 0.2,
                LeadIn = 10, LeadOut = 10, ToolNr = 4, Tolerance = 0.01,
            });

            string content = NcExport.AssembleFile(
                "bench_contour_arcs", 300, 300, 19, "7023K_681", null, lines);
            AssertMatchesGolden("bench_03_contour_arcs", content);
        }

        // -------------------------------------------------------------
        // 4. CABINET SIDE PANEL — planner math + drills + groove + fixchip
        // -------------------------------------------------------------
        [Test]
        public void Golden_04_CabinetSidePanel()
        {
            var ops = new List<string>();

            // System-32 rows on a 720mm side, front + back columns (planner math)
            var ys = CabinetPlanner.System32YPositions(720, 37, 32);
            var cols = CabinetPlanner.System32ColumnsX(542, 37, 32);
            var pts = new List<DrillLogic.Point2dz>();
            foreach (double y in ys)
            {
                pts.Add(new DrillLogic.Point2dz(cols.FrontX, y, 0));
                pts.Add(new DrillLogic.Point2dz(cols.BackX, y, 0));
            }
            var s32 = DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = pts, Depth = 13.0, Diameter = 5.0, Stepdown = 0, ToolNr = 1,
            });
            ops.AddRange(s32.Lines);

            // Back-panel groove (reference-verified _Nuten_X_V5 semantics)
            ops.AddRange(SlotLogic.GenerateGroove(new SlotLogic.GrooveInput
            {
                IsXGroove = true,
                Positions = new[] { new SlotLogic.GroovePosition { Y = 10, SurfaceZ = 0 } },
                Width = 8.2, Depth = 10, Ebene = 0, ToolNr = 12,
            }));

            // Clamps (machine syntax)
            ops.AddRange(FixchipLogic.Generate(new FixchipLogic.FixchipInput
            {
                Positions = new[]
                {
                    new DrillLogic.Point2dz(0, 60, 9.5),
                    new DrillLogic.Point2dz(0, 660, 9.5),
                },
                Angle = 0, Optional = true,
            }));

            string content = NcExport.AssembleFile(
                "bench_cabinet_side", 560, 720, 19, "7535D_250", null, ops);
            AssertMatchesGolden("bench_04_cabinet_side", content);
        }

        // -------------------------------------------------------------
        // 5. NESTED SHEET — two parts translated onto one sheet (C2 path)
        // -------------------------------------------------------------
        [Test]
        public void Golden_05_NestedSheet()
        {
            // Part A: drill + pocket at local origin
            var partA = new List<string>();
            partA.AddRange(DrillLogic.Generate(new DrillLogic.DrillInput
            {
                Points = new[] { new DrillLogic.Point2dz(50, 50, 19) },
                Depth = 10, Diameter = 8, Stepdown = 0, ToolNr = 1,
            }).Lines);
            partA.AddRange(PocketLogic.GenerateRect(new PocketLogic.RectPocketInput
            {
                CenterX = 150, CenterY = 100, SurfaceZ = 19,
                Width = 80, Height = 40, Depth = 5, ToolNr = 4,
            }));

            // Part B: circular pocket
            var partB = PocketLogic.GenerateCirc(new PocketLogic.CircPocketInput
            {
                CenterX = 60, CenterY = 60, SurfaceZ = 19,
                Radius = 25, Depth = 8, ToolNr = 4,
            });

            // Nest placements (translation-only)
            var placedA = OpLineTransformer.Apply(partA,
                new OpLineTransformer.Placement { Tx = 10, Ty = 10 });
            var placedB = OpLineTransformer.Apply(partB,
                new OpLineTransformer.Placement { Tx = 400, Ty = 200 });
            Assert.That(placedA.Error, Is.Null);
            Assert.That(placedB.Error, Is.Null);

            var ops = new List<string>();
            ops.AddRange(placedA.Lines);
            ops.AddRange(placedB.Lines);

            string content = NcExport.AssembleFile(
                "bench_nested_sheet", 2440, 1220, 19, "7535D_250", null, ops);
            AssertMatchesGolden("bench_05_nested_sheet", content);
        }
    }
}
