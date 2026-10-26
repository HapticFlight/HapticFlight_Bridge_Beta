using System.Collections.Generic;

namespace DcsExtraTelemetry.Launcher
{
            // Dark navy palette for the HapticFlight desktop workspace.  These values
            // deliberately match the product's reference screens: rich navy surfaces,
            // an electric-blue action colour and restrained violet depth.
    //
    // One fixed theme: frosted, semi-transparent panels over a dark, softly lit backdrop,
    // an orange accent for actions and green / amber / red for good / highlight / problem.
    // Every key that earlier palettes defined is kept so DynamicResource and
    // SetResourceReference lookups in MainWindow and the analysis charts keep resolving.
    // Keys ending in "Color" are written as Color resources; everything else as brushes.
    internal static class GlassThemePalette
    {
        public static IReadOnlyDictionary<string, string> Active { get; private set; }

        public static readonly IReadOnlyDictionary<string, string> DarkGlass = new Dictionary<string, string>
        {
            // Window and backdrop. The three backdrop colours are the soft light pools that
            // stand in for a blurred photograph behind the panels.
            { "BackdropBase", "#FF050818" },
            { "BackdropWarmColor", "#D023286B" },
            { "BackdropTealColor", "#301A3470" },
            { "BackdropPurpleColor", "#D039328A" },
            { "BackdropMidColor", "#00090E35" },
            { "BackdropTransparentColor", "#00090E35" },
            { "WindowBackground", "#FF090E35" },
            { "HeaderBackground", "#00000000" },
            { "SidebarBackground", "#00000000" },

            // Panels
            { "ShellSurface", "#000A1038" },
            { "GlassSurface", "#E80B123C" },
            { "GlassSurfaceStrong", "#EC111B52" },
            { "GlassSurfaceSubtle", "#241A2B66" },
            { "GlassSidebarSurface", "#ED080D2D" },
            { "GlassCoolSurface", "#E80B123C" },
            { "GlassUpdateSurface", "#E80B123C" },
            { "StepStripSurface", "#E80B123C" },
            { "StepActiveSurface", "#E8152054" },
            { "GlassStroke", "#284875B4" },
            { "GlassHighlight", "#1FFFFFFF" },
            { "GlassShadow", "#75000020" },
            { "GlassSurfaceTopColor", "#F5080D28" },
            { "GlassSurfaceBottomColor", "#E014143B" },
            { "GlassStrokeTopColor", "#284875B4" },
            { "GlassStrokeBottomColor", "#284875B4" },
            { "PanelBackground", "#E80B123C" },
            { "PanelSubtleBackground", "#171A2B66" },
            { "SurfaceRaised", "#FF111946" },
            { "AnalyzerCardBackground", "#F00B123C" },
            { "AnalyzerPillBackground", "#FF111946" },
            { "AnalyzerToolbarBackground", "#FF0E1744" },
            { "InputBackground", "#FF101942" },
            { "LogBackground", "#FF060A25" },
            { "PanelBorder", "#182B51AA" },
            { "ControlBorder", "#40516A99" },

            // Navigation
            { "ActiveNavBackground", "#FF1B2038" },
            { "NavActiveBackground", "#FF147BFF" },
            { "NavActiveShadow", "#00000000" },

            // Actions
            { "Primary", "#FF147BFF" },
            { "PrimaryHover", "#FF3B91FF" },
            { "PrimaryPressed", "#FF0862D4" },
            { "PrimaryText", "#FFFFFFFF" },
            { "SecondaryButtonBackground", "#14FFFFFF" },
            { "SecondaryButtonHover", "#24FFFFFF" },
            { "SecondaryButtonPressed", "#33FFFFFF" },
            { "DisabledBackground", "#0DFFFFFF" },
            { "DisabledForeground", "#66FFFFFF" },
            { "FocusRing", "#FF51A6FF" },
            { "SelectedAccent", "#FF57A8FF" },

            // Text
            { "TextPrimary", "#FFFFFFFF" },
            { "TextSecondary", "#FFB9B9BE" },
            { "TextMuted", "#FF8E8E94" },

            // Status
            { "Success", "#FF6EE7A0" },
            { "Warning", "#FFF5B83D" },
            { "Danger", "#FFFF7C86" },
            { "DangerHover", "#FFFF9AA1" },
            { "DangerText", "#FF1C1C1E" },
            { "StatusError", "#FFFF7C86" },
            { "NeutralStatus", "#FF8E8E94" },
            { "GoodBadgeBackground", "#2E4ADE80" },
            { "BadBadgeBackground", "#2EF87171" },
            { "WarnBadgeBackground", "#2EF5B83D" },

            // Charts
            { "ChartBackground", "#00000000" },
            { "ChartAxisBackground", "#00000000" },
            { "ChartLane", "#0FFFFFFF" },
            { "ChartPhaseBand", "#0AFFFFFF" },
            { "ChartGridMajor", "#1AFFFFFF" },
            { "ChartGridMinor", "#0FFFFFFF" },
            { "ChartCursor", "#FFFFFFFF" },
            { "ChartBar", "#FF5A5A60" },
            { "ChartBarHighlight", "#FF34C759" },
            { "ChartBarPeak", "#FFF5B83D" },
            { "ChartCategoryRotor", "#FF34C759" },
            { "ChartCategoryWeapons", "#FFE07A75" },
            { "ChartCategoryGround", "#FFF5B83D" },
            { "ChartCategoryAirframe", "#FFC9C9CE" },

            // Scrollbars
            { "ScrollTrack", "#0DFFFFFF" },
            { "ScrollThumb", "#2EFFFFFF" },
            { "ScrollThumbHover", "#52FFFFFF" }
        };

        public static IReadOnlyDictionary<string, string> SetActive(bool dark)
        {
            if (dark)
            {
                Active = DarkGlass;
                return Active;
            }

            var light = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> token in DarkGlass)
                light[token.Key] = token.Value;
            string[,] overrides =
            {
                { "BackdropBase", "#FFF2F5F8" }, { "BackdropWarmColor", "#FFD9E7F0" },
                { "BackdropTealColor", "#FFD9E8EE" }, { "BackdropPurpleColor", "#FFE0E5F2" },
                { "BackdropMidColor", "#00F2F3F5" }, { "BackdropTransparentColor", "#00F2F3F5" },
                { "WindowBackground", "#FFF2F3F5" }, { "HeaderBackground", "#FFF8F8FA" },
                { "SidebarBackground", "#FFF8F8FA" }, { "ShellSurface", "#F7FFFFFF" },
                { "GlassSurface", "#F2FFFFFF" }, { "GlassSurfaceStrong", "#FFFFFFFF" },
                { "GlassSurfaceSubtle", "#08000000" }, { "GlassSidebarSurface", "#F2FFFFFF" },
                { "GlassCoolSurface", "#F2FFFFFF" }, { "GlassUpdateSurface", "#F2FFFFFF" },
                { "StepStripSurface", "#F2FFFFFF" }, { "StepActiveSurface", "#FFFFFFFF" },
                { "GlassStroke", "#1A000000" }, { "GlassHighlight", "#80FFFFFF" },
                { "GlassShadow", "#18000000" }, { "GlassSurfaceTopColor", "#FFFFFFFF" },
                { "GlassSurfaceBottomColor", "#F2FFFFFF" }, { "GlassStrokeTopColor", "#33000000" },
                { "GlassStrokeBottomColor", "#1A000000" }, { "PanelBackground", "#F2FFFFFF" },
                { "PanelSubtleBackground", "#08000000" }, { "SurfaceRaised", "#FFFFFFFF" },
                { "AnalyzerCardBackground", "#F2FFFFFF" }, { "AnalyzerPillBackground", "#F7FFFFFF" },
                { "AnalyzerToolbarBackground", "#F2FFFFFF" }, { "InputBackground", "#FFFFFFFF" },
                { "LogBackground", "#FFF9FAFB" }, { "PanelBorder", "#1A000000" },
                { "ControlBorder", "#33000000" }, { "ActiveNavBackground", "#12000000" },
                { "NavActiveBackground", "#FF176B9D" }, { "Primary", "#FF176B9D" },
                { "PrimaryHover", "#FF2F83B8" }, { "PrimaryPressed", "#FF0E547E" },
                { "FocusRing", "#FF176B9D" }, { "SelectedAccent", "#FF2F83B8" },
                { "SecondaryButtonBackground", "#10000000" }, { "SecondaryButtonHover", "#19000000" },
                { "SecondaryButtonPressed", "#24000000" }, { "DisabledBackground", "#0A000000" },
                { "DisabledForeground", "#66000000" }, { "TextPrimary", "#FF202124" },
                { "TextSecondary", "#FF5F6368" }, { "TextMuted", "#FF777B80" },
                { "Success", "#FF168447" }, { "Warning", "#FF946200" },
                { "Danger", "#FFC62828" }, { "DangerHover", "#FFB71C1C" },
                { "DangerText", "#FFFFFFFF" }, { "StatusError", "#FFC62828" },
                { "NeutralStatus", "#FF777B80" }, { "GoodBadgeBackground", "#26168447" },
                { "BadBadgeBackground", "#26C62828" }, { "WarnBadgeBackground", "#26946200" },
                { "ChartLane", "#0A000000" }, { "ChartPhaseBand", "#07000000" },
                { "ChartGridMajor", "#24000000" }, { "ChartGridMinor", "#12000000" },
                { "ChartCursor", "#FF303238" }, { "ChartBar", "#FF8E9298" },
                { "ChartCategoryAirframe", "#FF777B80" }, { "ChartCategoryWeapons", "#FFC85F5C" }, { "ScrollTrack", "#0A000000" },
                { "ScrollThumb", "#33000000" }, { "ScrollThumbHover", "#52000000" }
            };
            for (int i = 0; i < overrides.GetLength(0); i++)
                light[overrides[i, 0]] = overrides[i, 1];
            Active = light;
            return Active;
        }
    }
}
