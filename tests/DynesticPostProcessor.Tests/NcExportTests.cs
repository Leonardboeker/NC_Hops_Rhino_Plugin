using NUnit.Framework;
using System.Collections.Generic;
using WallabyHop;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class NcExportTests
    {
        [Test]
        public void BuildHeader_StartsWithMakrotyp()
        {
            var lines = NcExport.BuildHeader("test", 800, 400, 19, "7023K_681");
            Assert.That(lines[0], Is.EqualTo(";MAKROTYP=0"));
        }

        [Test]
        public void BuildHeader_WritesZeroOffsets_NeverDimensions()
        {
            // ;DX/;DY/;DZ in the header are offsets. Every machine-generated
            // reference file has 0.000/0.000/0 there; the dimensions belong in
            // the VARS block only. This test pinned the old, wrong behaviour.
            var lines = NcExport.BuildHeader("test", 2440.0, 1220.0, 18.0, "");
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Contain(";DX=0.000"));
            Assert.That(joined, Does.Contain(";DY=0.000"));
            Assert.That(joined, Does.Contain(";DZ=0"));
            Assert.That(joined, Does.Not.Contain(";DX=2440.000"));
        }

        [Test]
        public void BuildHeader_OmitsWzgvWhenEmpty()
        {
            var lines = NcExport.BuildHeader("test", 800, 400, 19, "");
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Not.Contain(";WZGV="));
        }

        [Test]
        public void BuildHeader_IncludesWzgvWhenSet()
        {
            var lines = NcExport.BuildHeader("test", 800, 400, 19, "7023K_681");
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Contain(";WZGV=7023K_681"));
        }

        [Test]
        public void BuildHeader_ContainsMaschineHolzher()
        {
            var lines = NcExport.BuildHeader("mypart", 800, 400, 19, "");
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Contain(";MASCHINE=HOLZHER"));
            Assert.That(joined, Does.Contain(";NCNAME=mypart"));
        }

        [Test]
        public void SortOperationLines_WzbBeforeWzfBeforeWzs()
        {
            var lines = new List<string>
            {
                "WZS (2,...)", "CALL _nuten...",
                "WZF (7,...)", "SP (...)", "G01 (...)", "EP (...)",
                "WZB (1,...)", "Bohrung (...)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            Assert.That(sorted[0], Does.StartWith("WZB"));  // WZB block: [0-1]
            Assert.That(sorted[2], Does.StartWith("WZF"));  // WZF block: [2-5]
            Assert.That(sorted[6], Does.StartWith("WZS"));  // WZS block: [6-7]
        }

        [Test]
        public void SortOperationLines_KeepsBlockIntact()
        {
            // WZF block has multiple SP/EP lines -- all must stay together after sort
            var lines = new List<string>
            {
                "WZS (2,...)", "CALL _nuten_saw",
                "WZF (7,...)", "SP (1,2,3,...)", "G01 (4,5,...)", "EP (0,...)",
                "WZB (1,...)", "Bohrung (drill)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            // WZB block
            Assert.That(sorted[0], Does.StartWith("WZB"));
            Assert.That(sorted[1], Is.EqualTo("Bohrung (drill)"));
            // WZF block (all 4 lines intact)
            Assert.That(sorted[2], Does.StartWith("WZF"));
            Assert.That(sorted[3], Is.EqualTo("SP (1,2,3,...)"));
            Assert.That(sorted[4], Is.EqualTo("G01 (4,5,...)"));
            Assert.That(sorted[5], Is.EqualTo("EP (0,...)"));
            // WZS block
            Assert.That(sorted[6], Does.StartWith("WZS"));
            Assert.That(sorted[7], Is.EqualTo("CALL _nuten_saw"));
        }

        [Test]
        public void SortOperationLines_TwoWzfBlocksPreserveRelativeOrder()
        {
            // Two WZF blocks: tool 7 then tool 9 -- relative order must be preserved
            var lines = new List<string>
            {
                "WZF (7,...)", "SP (a)", "G01 (b)", "EP (c)",
                "WZF (9,...)", "SP (d)", "G03M (e)", "EP (f)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            Assert.That(sorted[0], Does.Contain("7"));
            Assert.That(sorted[4], Does.Contain("9"));
            // SP/EP blocks must not be mixed
            Assert.That(sorted[1], Is.EqualTo("SP (a)"));
            Assert.That(sorted[5], Is.EqualTo("SP (d)"));
        }

        [Test]
        public void SortOperationLines_NoNestedSpsAfterSort()
        {
            // Regression: old 2-line-pair sort caused WZF+SP to be separated from
            // its subsequent G01/EP lines, leading to nested SPs in output.
            var lines = new List<string>
            {
                "WZF (4,...)",
                "SP (100,200,-10,...)", "G01 (150,200,...)", "EP (0,...)",
                "WZF (7,...)",
                "SP (10,20,-0.4,...)", "G03M (15,25,...)", "EP (0,...)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            // Verify no two consecutive SP lines (nested SP)
            for (int i = 0; i < sorted.Count - 1; i++)
                if (sorted[i].StartsWith("SP "))
                    Assert.That(sorted[i + 1], Does.Not.StartWith("SP "),
                        "Nested SP detected at position " + i);
        }

        private static List<string> StartSection(string content)
        {
            var all = new List<string>(content.Split(new[] { "\r\n" }, System.StringSplitOptions.None));
            return all.GetRange(all.IndexOf("START"), all.Count - all.IndexOf("START"));
        }

        [Test]
        public void AssembleFile_ParksWithMachineStandardRightAfterFertigteil()
        {
            var start = StartSection(NcExport.AssembleFile("p", 800, 400, 19, "", null,
                new List<string> { "WZB (1,_VE,_V*1,_VA,_SD,0,'')", "Bohrung (1,2,8,-10,10,0,0,0,0,0,0,0)" }));
            Assert.That(start[2], Is.EqualTo("CALL Park_V7 ( VAL MODE:=2,POSX:=0,POSY:=0)"));
            Assert.That(start.FindAll(l => l.Contains("Park")).Count, Is.EqualTo(1));
        }

        [Test]
        public void AssembleFile_HopNestingParkLineReplacesDefault()
        {
            // A HopNesting block wired after the cuts brings its own Park_V7 with
            // its ParkMode: it moves up behind Fertigteil, never a second park call.
            var ops = new List<string> { "WZB (1,_VE,_V*1,_VA,_SD,0,'')", "Bohrung (1,2,8,-10,10,0,0,0,0,0,0,0)" };
            ops.AddRange(NcExport.NestingBlock(3, 0, 0, 0, true));
            var start = StartSection(NcExport.AssembleFile("p", 800, 400, 19, "", null, ops));
            Assert.That(start[2], Is.EqualTo("CALL Park_V7 ( VAL MODE:=3,POSX:=0,POSY:=0)"));
            Assert.That(start.FindAll(l => l.Contains("Park")).Count, Is.EqualTo(1));
        }

        [Test]
        public void NestingBlock_UsesMachineSpellingAndOrder()
        {
            // As in all 127 nesting machine files.
            Assert.That(NcExport.NestingBlock(2, 0, 0, 0, true), Is.EqualTo(new List<string>
            {
                "CALL Park_V7 ( VAL MODE:=2,POSX:=0,POSY:=0)",
                "CALL HH_MarkLabel ( VAL POSX:=_hhdata_LabelPosX,POSY:=_hhdata_LabelPosY,ANGLE:=0,LASER:=0)",
                "CALL BN_TrennerInnenAussen ()",
                "CALL BN_NestKontur ()",
            }));
            Assert.That(NcExport.NestingBlock(2, 0, 0, 0, false), Has.None.Contains("HH_MarkLabel"));
        }

        [Test]
        public void StartSection_HoistsNestBlockOnceAndDefaultsPark()
        {
            // Wired after a WZB block (and twice, from two parts): the block must
            // not stay between tool blocks, and must appear exactly once.
            var ops = new List<string> { "WZF (4,_VE,_V*1,_VA,_SD,0,'')", "G01 (1,2,0,0,0,2)" };
            ops.AddRange(NcExport.NestingBlock(2, 0, 0, 0, true));
            ops.Add("WZB (1,_VE,_V*1,_VA,_SD,0,'')");
            ops.Add("Bohrung (1,2,8,-10,10,0,0,0,0,0,0,0)");
            ops.AddRange(NcExport.NestingBlock(3, 0, 0, 0, true));

            var head = NcExport.StartSection(ops);

            var expected = new List<string> { "START", "Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)" };
            expected.AddRange(NcExport.NestingBlock(2, 0, 0, 0, true));
            Assert.That(head, Is.EqualTo(expected));
            Assert.That(ops, Is.EqualTo(new List<string>
            {
                "WZF (4,_VE,_V*1,_VA,_SD,0,'')", "G01 (1,2,0,0,0,2)",
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')", "Bohrung (1,2,8,-10,10,0,0,0,0,0,0,0)",
            }));

            var bare = NcExport.StartSection(new List<string> { "WZB (1,_VE,_V*1,_VA,_SD,0,'')" });
            Assert.That(bare, Is.EqualTo(new List<string>
                { "START", "Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)", NcExport.ParkLine() }));
        }
    }
}
