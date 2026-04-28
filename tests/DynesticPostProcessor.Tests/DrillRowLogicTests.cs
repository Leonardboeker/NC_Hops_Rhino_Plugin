using NUnit.Framework;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class DrillRowLogicTests
    {
        [Test]
        public void XRow_FivePoint32mmHoles_Snapshot()
        {
            // Cabinet shelf-pin row: 32 mm system, 5 holes (start + 4 spacings of 32)
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 50.0, StartY = 100.0, StartZ = 19.0,
                Spacings = new[] { 32.0, 32.0, 32.0, 32.0 },
                Depth = 13.0, Diameter = 5.0,
                Mirror = false, ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);

            Assert.That(result.HoleCount, Is.EqualTo(5));
            Assert.That(result.Lines, Is.EquivalentTo(new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Bohgx_V5(VAL SPY:=100,BIX:=32,BIIX:=32,BIIIX:=32,BIIIIX:=32," +
                "SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1," +
                "USE2:=1,USE3:=1,USE4:=1)",
            }));
        }

        [Test]
        public void YRow_TwoHoles_Snapshot()
        {
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = false,
                StartX = 50.0, StartY = 100.0, StartZ = 19.0,
                Spacings = new[] { 32.0 },  // only 1 spacing → 2 holes
                Depth = 13.0, Diameter = 5.0,
                ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);

            Assert.That(result.HoleCount, Is.EqualTo(2));
            Assert.That(result.Lines[1],
                Is.EqualTo(
                    "CALL _Bohgy_V5(VAL SPX:=50,BIY:=32,BIIY:=0,BIIIY:=0,BIIIIY:=0," +
                    "SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1," +
                    "USE2:=1,USE3:=1,USE4:=1)"));
        }

        [Test]
        public void Mirror_SetsSpiegelnTo1()
        {
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 0, StartY = 0, StartZ = 19,
                Spacings = new[] { 32.0 },
                Depth = 13.0, Diameter = 5.0,
                Mirror = true, ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);
            Assert.That(result.Lines[1], Does.Contain("SPIEGELN:=1"));
        }

        [Test]
        public void HoleCount_IgnoresZeroSpacings()
        {
            // 32, 0, 32, 32 → 4 holes (zero is "disabled", not a hole)
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 0, StartY = 0, StartZ = 19,
                Spacings = new[] { 32.0, 0.0, 32.0, 32.0 },
                Depth = 13.0, Diameter = 5.0,
                ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);
            Assert.That(result.HoleCount, Is.EqualTo(4));
        }

        [Test]
        public void Defaults_AppliedWhenZeroOrNegative()
        {
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 0, StartY = 0, StartZ = 19,
                Spacings = new[] { 32.0 },
                Depth = 0.0,       // → 13
                Diameter = -1.0,   // → 5
                ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);
            Assert.That(result.Lines[1], Does.Contain("T:=6"));     // 19 - 13 = 6
            Assert.That(result.Lines[1], Does.Contain("D:=5"));
        }
    }
}
