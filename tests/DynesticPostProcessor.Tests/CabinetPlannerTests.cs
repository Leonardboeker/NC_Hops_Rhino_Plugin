using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using WallabyHop.Logic;

namespace DynesticPostProcessor.Tests
{
    [TestFixture]
    public class CabinetPlannerTests
    {
        // -------------------------------------------------------------
        // VALIDATION
        // -------------------------------------------------------------

        [Test]
        public void Validate_StandardCabinet_Passes()
        {
            // 600x720x560 with 19mm thickness — typical base cabinet
            Assert.That(CabinetPlanner.ValidateDimensions(600, 720, 560, 19), Is.Null);
        }

        [Test]
        public void Validate_ZeroOrNegativeDimensions_Fails()
        {
            Assert.That(CabinetPlanner.ValidateDimensions(0, 720, 560, 19),
                Does.StartWith("Dimensions"));
            Assert.That(CabinetPlanner.ValidateDimensions(-100, 720, 560, 19),
                Does.StartWith("Dimensions"));
        }

        [Test]
        public void Validate_ThicknessTooLarge_Fails()
        {
            // thickness >= half of smallest dim → invalid (panels would overlap)
            Assert.That(CabinetPlanner.ValidateDimensions(100, 100, 100, 50),
                Does.StartWith("Thickness"));
        }

        // -------------------------------------------------------------
        // INNER DIMENSIONS
        // -------------------------------------------------------------

        [Test]
        public void InnerDimensions_StandardButtJoint()
        {
            // 600x720x560 with 19mm → inner 562 x 682 x 560
            var inner = CabinetPlanner.ComputeInnerDimensions(600, 720, 560, 19);
            Assert.That(inner.InnerWidth, Is.EqualTo(562));
            Assert.That(inner.InnerHeight, Is.EqualTo(682));
            Assert.That(inner.InnerDepth, Is.EqualTo(560)); // depth unchanged
        }

        // -------------------------------------------------------------
        // CONNECTORS
        // -------------------------------------------------------------

        [Test]
        public void AutoConnectorCount_DependsOnDepth()
        {
            Assert.That(CabinetPlanner.AutoConnectorCount(280), Is.EqualTo(2));
            Assert.That(CabinetPlanner.AutoConnectorCount(300), Is.EqualTo(2));
            Assert.That(CabinetPlanner.AutoConnectorCount(400), Is.EqualTo(3));
            Assert.That(CabinetPlanner.AutoConnectorCount(500), Is.EqualTo(3));
            Assert.That(CabinetPlanner.AutoConnectorCount(560), Is.EqualTo(4));
            Assert.That(CabinetPlanner.AutoConnectorCount(800), Is.EqualTo(4));
        }

        [Test]
        public void ConnectorPositions_ThreeAcross560mm()
        {
            // Depth 560, 3 connectors, edge 37 → positions at 37, 280, 523
            var p = CabinetPlanner.ConnectorPositions(560, 3, 37);
            Assert.That(p.Count, Is.EqualTo(3));
            Assert.That(p[0], Is.EqualTo(37).Within(1e-9));
            Assert.That(p[1], Is.EqualTo(280).Within(1e-9));
            Assert.That(p[2], Is.EqualTo(523).Within(1e-9));
        }

        [Test]
        public void ConnectorPositions_OneCenters()
        {
            var p = CabinetPlanner.ConnectorPositions(560, 1, 37);
            Assert.That(p, Is.EquivalentTo(new[] { 280.0 }));
        }

        // -------------------------------------------------------------
        // SYSTEM-32
        // -------------------------------------------------------------

        [Test]
        public void System32YPositions_720mmCabinet_StepsOf32Edge37()
        {
            // First at 37, last <= 720 - 37 = 683 → 37, 69, 101, ..., 677
            var ys = CabinetPlanner.System32YPositions(720, 37, 32);
            Assert.That(ys.First(), Is.EqualTo(37));
            Assert.That(ys.Last(), Is.LessThanOrEqualTo(683.1));
            // Each step = 32
            for (int i = 1; i < ys.Count; i++)
                Assert.That(ys[i] - ys[i - 1], Is.EqualTo(32).Within(1e-9));
        }

        [Test]
        public void System32ColumnsX_FrontAtEdge_BackInsetByEdge()
        {
            var cols = CabinetPlanner.System32ColumnsX(560, 37, 32);
            Assert.That(cols.FrontX, Is.EqualTo(37));
            Assert.That(cols.BackX, Is.EqualTo(560 - 37));  // 523
        }

        [Test]
        public void System32ColumnsX_TightDepth_BackClampedToFrontPlusRaster()
        {
            // depth 100, edge 37 → backX would be 63, but we want at least front+raster=69
            var cols = CabinetPlanner.System32ColumnsX(100, 37, 32);
            Assert.That(cols.BackX, Is.EqualTo(37 + 32));   // 69, clamped
        }

        // -------------------------------------------------------------
        // FOOT CENTERS
        // -------------------------------------------------------------

        [Test]
        public void FootCenters_StandardCabinet_FourCorners()
        {
            // 600 wide → only 4 corners (no middle row)
            var feet = CabinetPlanner.FootCenters(600, 562, 560, 50);
            Assert.That(feet.Count, Is.EqualTo(4));
        }

        [Test]
        public void FootCenters_WideCabinet_AddsMiddleRow()
        {
            // 1000 wide (>800) → 4 corners + 2 middle = 6
            var feet = CabinetPlanner.FootCenters(1000, 962, 560, 50);
            Assert.That(feet.Count, Is.EqualTo(6));
        }

        // -------------------------------------------------------------
        // DOORS
        // -------------------------------------------------------------

        [Test]
        public void DoorDimensions_TwoDoorsFullOverlay()
        {
            // 600x720 cabinet, 2 doors, 3mm gap, FullOverlay
            // 3 gaps total → doorW = (600 - 3*3) / 2 = 295.5
            // doorH = 720 - 2*3 = 714
            var d = CabinetPlanner.ComputeDoorDimensions(600, 720, 562, 682, 19,
                doorCount: 2, gap: 3, overlay: CabinetPlanner.DoorOverlay.FullOverlay);
            Assert.That(d.Width, Is.EqualTo(295.5).Within(1e-9));
            Assert.That(d.Height, Is.EqualTo(714).Within(1e-9));
        }

        [Test]
        public void DoorDimensions_OneDoorInset()
        {
            // 1 door inset: 1 + 1 = 2 gaps → doorW = (innerW - 2*gap) / 1 = 562 - 6 = 556
            var d = CabinetPlanner.ComputeDoorDimensions(600, 720, 562, 682, 19,
                doorCount: 1, gap: 3, overlay: CabinetPlanner.DoorOverlay.Inset);
            Assert.That(d.Width, Is.EqualTo(556).Within(1e-9));
            Assert.That(d.Height, Is.EqualTo(682 - 6).Within(1e-9));
        }

        // -------------------------------------------------------------
        // HINGE COUNT + POSITIONS
        // -------------------------------------------------------------

        [Test]
        public void HingeCount_ScalesWithDoorHeight()
        {
            Assert.That(CabinetPlanner.HingeCount(700),  Is.EqualTo(2));
            Assert.That(CabinetPlanner.HingeCount(1200), Is.EqualTo(3));
            Assert.That(CabinetPlanner.HingeCount(1700), Is.EqualTo(4));
            Assert.That(CabinetPlanner.HingeCount(2000), Is.EqualTo(5));
        }

        [Test]
        public void HingeYPositions_TwoHinges_AtTopAndBottomFirstPos()
        {
            var ys = CabinetPlanner.HingeYPositions(720, 2, 128);
            Assert.That(ys, Is.EquivalentTo(new[] { 128.0, 720.0 - 128.0 }));
        }

        [Test]
        public void HingeYPositions_ThreeHinges_MiddleEvenlyDistributed()
        {
            var ys = CabinetPlanner.HingeYPositions(1500, 3, 128);
            // top=128, bottom=1500-128=1372, middle = 128 + (1372-128)/2 = 750
            Assert.That(ys[0], Is.EqualTo(128).Within(1e-9));
            Assert.That(ys[1], Is.EqualTo(750).Within(1e-9));
            Assert.That(ys[2], Is.EqualTo(1372).Within(1e-9));
        }

        // -------------------------------------------------------------
        // EFFECTIVE DEPTH (back panel)
        // -------------------------------------------------------------

        [Test]
        public void EffectiveDepth_Inset_SubtractsBackThicknessAndSetback()
        {
            // depth 560, MS 19, back 8mm thick, setback 10 → effective = 560 - 8 - 10 = 542
            double e = CabinetPlanner.EffectiveDepth(560, 19,
                CabinetPlanner.BackType.Inset, 8, setback: 10, falzWidth: 0, setbackDist: 0);
            Assert.That(e, Is.EqualTo(542).Within(1e-9));
        }

        [Test]
        public void EffectiveDepth_Rabbeted_SubtractsFalzWidth()
        {
            // depth 560, falzWidth 8.5 → effective = 560 - 8.5 = 551.5
            double e = CabinetPlanner.EffectiveDepth(560, 19,
                CabinetPlanner.BackType.Rabbeted, 8, setback: 0, falzWidth: 8.5, setbackDist: 0);
            Assert.That(e, Is.EqualTo(551.5).Within(1e-9));
        }

        [Test]
        public void EffectiveDepth_Grooved_SubtractsSetbackDistAndThickness()
        {
            double e = CabinetPlanner.EffectiveDepth(560, 19,
                CabinetPlanner.BackType.Grooved, 8, setback: 0, falzWidth: 0, setbackDist: 10);
            Assert.That(e, Is.EqualTo(560 - 10 - 8).Within(1e-9));
        }

        [Test]
        public void EffectiveDepth_NeverBelowMinimumSanity()
        {
            // Pathological: setback enormous → would yield negative → must clamp to MS+10
            double e = CabinetPlanner.EffectiveDepth(560, 19,
                CabinetPlanner.BackType.Inset, 8, setback: 9999, falzWidth: 0, setbackDist: 0);
            Assert.That(e, Is.EqualTo(29).Within(1e-9));   // 19 + 10
        }
    }
}
