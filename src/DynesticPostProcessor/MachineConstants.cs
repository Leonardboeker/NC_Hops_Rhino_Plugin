namespace WallabyHop
{
    /// <summary>
    /// Single source of truth for machine-side defaults and magic numbers.
    /// Anything that previously appeared as a literal scattered across
    /// components or logic modules — gather it here so a future change to
    /// the HOLZ-HER setup is one edit, not a search-and-replace.
    ///
    /// What belongs here:
    /// - Heights / clearances (preview safe-Z, spoilboard allowance)
    /// - Default tool diameters / depths used as fallbacks
    /// - Header field defaults written into every .hop file
    /// - Tool-call shape constants (feed factor, prefix tokens)
    ///
    /// What does NOT belong here:
    /// - Per-operation values (depth, stepdown, etc. are component inputs)
    /// - Per-rechner paths (those live in PluginConfig)
    /// - NC macro literal strings (those live in NcStrings)
    /// </summary>
    internal static class MachineConstants
    {
        // -------------------------------------------------------------
        // PREVIEW / SAFETY
        // -------------------------------------------------------------

        /// <summary>
        /// Vertical clearance in mm above the surface used to draw the
        /// preview "approach line" in Rhino. NOT a machine retreat
        /// height — purely cosmetic for the viewport.
        /// </summary>
        internal const double PreviewSafeZOffset = 20.0;

        /// <summary>
        /// Maximum tolerated overshoot below the plate's declared DZ
        /// thickness, in mm. The spoilboard typically tolerates this
        /// much penetration. HopAnalyzer flags any operation whose
        /// effective depth exceeds DZ + this value.
        /// </summary>
        internal const double SpoilboardAllowanceMm = 5.0;

        // -------------------------------------------------------------
        // OPERATION DEFAULTS (used as fallbacks when an input is 0)
        // -------------------------------------------------------------

        internal const double DefaultDrillDiameterMm = 8.0;
        internal const double DefaultDrillDepthMm = 1.0;
        internal const double DefaultMillingDepthMm = 1.0;
        internal const double DefaultSawKerfMm = 3.2;
        internal const double DefaultSawDepthMm = 19.0;
        internal const double DefaultSawTravelLengthMm = 600.0;

        internal const double DefaultBlumDistanceMm = 22.5;
        internal const double DefaultBlumCupDiameterMm = 35.0;
        internal const double DefaultBlumCupDepthMm = 12.8;

        internal const double DefaultGrooveWidthMm = 8.0;
        internal const double DefaultGrooveDepthMm = 8.0;

        internal const double DefaultDrillRowDepthMm = 13.0;
        internal const double DefaultDrillRowDiameterMm = 5.0;

        // -------------------------------------------------------------
        // .hop FILE HEADER DEFAULTS
        // -------------------------------------------------------------

        /// <summary>The fixed machine identifier the HOLZ-HER controller expects.</summary>
        internal const string HeaderMachineId = "HOLZHER";

        /// <summary>Dialog DLL name written into every .hop header.</summary>
        internal const string HeaderDialogDll = "Dialoge.Dll";

        /// <summary>Dialog procedure name written into every .hop header.</summary>
        internal const string HeaderDialogProc = "StandardFormAnzeigen";

        // -------------------------------------------------------------
        // TOOL CALL SHAPE
        // -------------------------------------------------------------

        /// <summary>Default feed factor multiplier used in WZB/WZF tool calls.</summary>
        internal const double DefaultDrillFeedFactor = 1.0;

        /// <summary>Default feed factor multiplier used in WZS (saw) tool calls.</summary>
        internal const double DefaultSawFeedFactor = 0.3;

        // -------------------------------------------------------------
        // FIXCHIP CLAMP FOOTPRINT
        // -------------------------------------------------------------

        /// <summary>
        /// Radius in mm around a Fixchip_K position considered "occupied"
        /// by the clamp body. HopAnalyzer flags any operation whose XY
        /// position lands within this radius — drilling/milling there
        /// would collide with the clamp. Conservative default; tighten
        /// only after measuring an actual clamp on the bed.
        /// </summary>
        internal const double FixchipClampRadiusMm = 25.0;
    }
}
