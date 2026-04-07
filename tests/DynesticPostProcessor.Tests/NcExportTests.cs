using NUnit.Framework;
using System.Collections.Generic;
using DynesticPostProcessor;

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
        public void BuildHeader_ContainsDimensions()
        {
            var lines = NcExport.BuildHeader("test", 2440.0, 1220.0, 18.0, "");
            string joined = string.Join("\n", lines);
            Assert.That(joined, Does.Contain(";DX=2440.000"));
            Assert.That(joined, Does.Contain(";DY=1220.000"));
            Assert.That(joined, Does.Contain(";DZ=18.000"));
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
    }
}
