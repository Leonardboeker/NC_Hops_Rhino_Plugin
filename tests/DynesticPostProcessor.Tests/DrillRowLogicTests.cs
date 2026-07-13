using NUnit.Framework;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class DrillRowLogicTests
    {
        // Reference semantics (machine files):
        //   BIX/BIY = position of the FIRST hole along the row axis
        //   BII..BIIII = increments to holes 2..4 (INKREMENT:=1)
        //   USEn gates hole n — a 0 increment with USEn:=1 would drill
        //   hole n ON TOP of the previous one.

        [Test]
        public void XRow_FourHoles32mmSystem_Snapshot()
        {
            // Shelf-pin row: first hole at X=50, then 3 × 32 mm increments
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 50.0, StartY = 100.0, StartZ = 19.0,
                Spacings = new[] { 32.0, 32.0, 32.0 },
                Depth = 13.0, Diameter = 5.0,
                Mirror = false, ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);

            Assert.That(result.HoleCount, Is.EqualTo(4));
            Assert.That(result.Lines, Is.EquivalentTo(new[]
            {
                "WZB (1,_VE,_V*1,_VA,_SD,0,'')",
                "CALL _Bohgx_V5(VAL SPY:=100,BIX:=50,BIIX:=32,BIIIX:=32,BIIIIX:=32," +
                "SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1," +
                "USE2:=1,USE3:=1,USE4:=1)",
            }));
        }

        [Test]
        public void YRow_TwoHoles_StartCoordinateInBIY_UnusedHolesGatedOff()
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
                    "CALL _Bohgy_V5(VAL SPX:=50,BIY:=100,BIIY:=32,BIIIY:=0,BIIIIY:=0," +
                    "SPIEGELN:=0,T:=6,D:=5,TLF:=10,INKREMENT:=1,ESXY:=0,ESD:=1," +
                    "USE2:=1,USE3:=0,USE4:=0)"));
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
        public void ZeroSpacing_DisablesHoleViaUseFlag()
        {
            // 32, 0, 32 → 3 holes; USE3 MUST be 0 or the machine drills
            // hole 3 on top of hole 2.
            var input = new DrillRowLogic.DrillRowInput
            {
                IsXRow = true,
                StartX = 10, StartY = 0, StartZ = 19,
                Spacings = new[] { 32.0, 0.0, 32.0 },
                Depth = 13.0, Diameter = 5.0,
                ToolNr = 1,
            };
            var result = DrillRowLogic.Generate(input);
            Assert.That(result.HoleCount, Is.EqualTo(3));
            Assert.That(result.Lines[1], Does.Contain("USE2:=1,USE3:=0,USE4:=1"));
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
