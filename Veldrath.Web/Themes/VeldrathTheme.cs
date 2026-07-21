using MudBlazor;

namespace Veldrath.Web.Themes;

/// <summary>
/// Veldrath MudBlazor theme — Crimson Seal (blood red), Dark Black/Grey, and Gold.
/// Aligned with the Veldrath Design System (VDS) defined in docs/design-system.md.
/// </summary>
public static class VeldrathTheme
{
    /// <summary>
    /// Gets the default dark-first MudBlazor theme for Veldrath.Web.
    /// </summary>
    public static MudTheme Default { get; } = new()
    {
        PaletteDark = new PaletteDark()
        {
            // ── Primary: Crimson Blood Red (Crimson Seal) ──
            Primary = "#C0392B",
            PrimaryDarken = "#8C2018",
            PrimaryLighten = "#E05545",

            // ── Secondary: Gold ──
            Secondary = "#C9A84C",
            SecondaryDarken = "#A68A2E",
            SecondaryLighten = "#D4B96A",

            // ── Tertiary: Emberfall (heat / energy accent) ──
            Tertiary = "#CC4125",
            TertiaryDarken = "#943018",
            TertiaryLighten = "#E05E35",

            // ── Background layers ──
            Background = "#0C0D13",
            Surface = "#14151F",
            Dark = "#0C0D13",
            DarkLighten = "#1C1D2B",
            DarkDarken = "#080910",

            // ── Text ──
            TextPrimary = "#F0EDE8",
            TextSecondary = "#A8A09A",
            TextDisabled = "#4A4540",

            // ── AppBar ──
            AppbarBackground = "#14151F",
            AppbarText = "#F0EDE8",

            // ── Drawer ──
            DrawerBackground = "#0C0D13",
            DrawerText = "#A8A09A",

            // ── Lines / Dividers / Borders ──
            LinesDefault = "#2A2B3D",
            LinesInputs = "#3D3F58",
            Divider = "#2A2B3D",
            DividerLight = "#1E1F2D",

            // ── Table ──
            TableLines = "#2A2B3D",
            TableStriped = "#14151F",
            TableHover = "#1C1D2B",

            // ── Actions ──
            ActionDefault = "#A8A09A",
            ActionDisabled = "#4A4540",
            ActionDisabledBackground = "#1C1D2B",

            // ── Semantic ──
            Error = "#E05252",
            ErrorDarken = "#B03A3A",
            ErrorLighten = "#E07070",
            Warning = "#D4964A",
            WarningDarken = "#B07A2E",
            WarningLighten = "#E0B070",
            Info = "#5A9ED6",
            InfoDarken = "#3A7AB0",
            InfoLighten = "#7AB8E0",
            Success = "#5CAB7D",
            SuccessDarken = "#3A8A5A",
            SuccessLighten = "#7AC89D",

            // ── Overlays ──
            OverlayDark = "rgba(0, 0, 0, 0.65)",
            OverlayLight = "rgba(255, 255, 255, 0.06)",

            // ── Hover / Ripple ──
            HoverOpacity = 0.06,
            RippleOpacity = 0.12,

            // ── Gray scale ──
            GrayLight = "#2E2F42",
            GrayDark = "#1E1F2D",
            GrayDarker = "#0C0D13",
        },

        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.5",
            },
            H1 = new H1Typography()
            {
                FontFamily = new[] { "Cinzel", "serif" },
                FontWeight = "600",
                FontSize = "2rem",
                LineHeight = "1.3",
            },
            H2 = new H2Typography()
            {
                FontFamily = new[] { "Cinzel", "serif" },
                FontWeight = "600",
                FontSize = "1.5rem",
                LineHeight = "1.3",
            },
            H3 = new H3Typography()
            {
                FontFamily = new[] { "Cinzel", "serif" },
                FontWeight = "600",
                FontSize = "1.25rem",
                LineHeight = "1.3",
            },
            H4 = new H4Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "600",
                FontSize = "1.125rem",
                LineHeight = "1.4",
            },
            H5 = new H5Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "600",
                FontSize = "1rem",
                LineHeight = "1.4",
            },
            H6 = new H6Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "600",
                FontSize = "0.875rem",
                LineHeight = "1.4",
            },
            Subtitle1 = new Subtitle1Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "500",
                FontSize = "1rem",
                LineHeight = "1.5",
            },
            Subtitle2 = new Subtitle2Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "500",
                FontSize = "0.875rem",
                LineHeight = "1.5",
            },
            Body1 = new Body1Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "400",
                FontSize = "0.875rem",
                LineHeight = "1.5",
            },
            Body2 = new Body2Typography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "400",
                FontSize = "0.75rem",
                LineHeight = "1.5",
            },
            Caption = new CaptionTypography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "400",
                FontSize = "0.6875rem",
                LineHeight = "1.4",
            },
            Button = new ButtonTypography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "500",
                FontSize = "0.875rem",
            },
            Overline = new OverlineTypography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "500",
                FontSize = "0.625rem",
                LetterSpacing = "0.1em",
            },
        },

        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "4px",
        },
    };
}
