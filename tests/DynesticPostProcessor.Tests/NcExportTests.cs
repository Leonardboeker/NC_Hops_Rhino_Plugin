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
        public void SortOperationLines_WzbBeforeWzs()
        {
            var lines = new List<string>
            {
                "WZS (2,...)", "CALL _nuten...",
                "WZB (1,...)", "Bohrung (...)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            Assert.That(sorted[0], Does.StartWith("WZB"));
            Assert.That(sorted[2], Does.StartWith("WZS"));
        }

        [Test]
        public void SortOperationLines_KeepsPairsIntact()
        {
            var lines = new List<string>
            {
                "WZS (2,...)", "CALL _nuten_saw",
                "WZB (1,...)", "Bohrung (drill)",
            };
            var sorted = NcExport.SortOperationLines(lines);
            // WZB pair first
            Assert.That(sorted[0], Does.StartWith("WZB"));
            Assert.That(sorted[1], Is.EqualTo("Bohrung (drill)"));
            // WZS pair second
            Assert.That(sorted[2], Does.StartWith("WZS"));
            Assert.That(sorted[3], Is.EqualTo("CALL _nuten_saw"));
        }
    }
}
