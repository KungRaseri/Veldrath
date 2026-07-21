using MudBlazor;

namespace Veldrath.Web.Themes;

/// <summary>
/// Veldrath MudBlazor theme — Crimson Seal (blood red), Dark Black/Grey, and Gold.
/// Aligned with the Veldrath Design System (VDS) defined in docs/design-system.md.
/// <para>
/// ⚠️ Palette hex values are the single source of truth for VDS color tokens.
/// CSS rules in wwwroot/app.css reference these values via --mud-palette-* CSS
/// custom properties generated at runtime by MudThemeProvider. Each property is
/// annotated with its corresponding --vds-* token for cross-reference. Only
/// VDS-specific colors without a MudBlazor equivalent remain in app.css :root.
/// </para>
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
            Primary = "#C0392B", // VDS token: --vds-seal
            PrimaryDarken = "#8C2018", // VDS token: --vds-seal-dark
            PrimaryLighten = "#E05545", // VDS token: --vds-seal-light

            // ── Secondary: Gold ──
            Secondary = "#C9A84C", // VDS token: --vds-gold
            SecondaryDarken = "#A68A2E", // VDS token: --vds-gold-dark
            SecondaryLighten = "#D4B96A", // VDS token: --vds-gold-light

            // ── Tertiary: Emberfall (heat / energy accent) ──
            Tertiary = "#CC4125", // VDS token: --vds-ember
            TertiaryDarken = "#943018", // VDS token: --vds-ember-dark
            TertiaryLighten = "#E05E35", // VDS token: --vds-ember-light

            // ── Background layers ──
            Background = "#0C0D13", // VDS token: --vds-bg-0
            Surface = "#14151F", // VDS token: --vds-bg-1
            Dark = "#0C0D13", // VDS token: --vds-bg-0
            DarkLighten = "#1C1D2B", // VDS token: --vds-bg-2
            DarkDarken = "#080910", // VDS token: --vds-bg-deeper

            // ── Text ──
            TextPrimary = "#F0EDE8", // VDS token: --vds-text
            TextSecondary = "#A8A09A", // VDS token: --vds-text-muted
            TextDisabled = "#4A4540", // VDS token: --vds-text-disabled

            // ── AppBar ──
            AppbarBackground = "#14151F", // VDS token: --vds-bg-1
            AppbarText = "#F0EDE8", // VDS token: --vds-text

            // ── Drawer ──
            DrawerBackground = "#0C0D13", // VDS token: --vds-bg-0
            DrawerText = "#A8A09A", // VDS token: --vds-text-muted

            // ── Lines / Dividers / Borders ──
            LinesDefault = "#2A2B3D", // VDS token: --vds-border
            LinesInputs = "#3D3F58", // VDS token: --vds-border-strong
            Divider = "#2A2B3D", // VDS token: --vds-border
            DividerLight = "#1E1F2D", // VDS token: --vds-border-subtle

            // ── Table ──
            TableLines = "#2A2B3D", // VDS token: --vds-border
            TableStriped = "#14151F", // VDS token: --vds-bg-1
            TableHover = "#1C1D2B", // VDS token: --vds-bg-2

            // ── Actions ──
            ActionDefault = "#A8A09A", // VDS token: --vds-text-muted
            ActionDisabled = "#4A4540", // VDS token: --vds-text-disabled
            ActionDisabledBackground = "#1C1D2B", // VDS token: --vds-bg-2

            // ── Semantic ──
            Error = "#E05252", // VDS token: --vds-danger
            ErrorDarken = "#B03A3A", // No VDS token (MudBlazor-specific)
            ErrorLighten = "#E07070", // No VDS token (MudBlazor-specific)
            Warning = "#D4964A", // VDS token: --vds-warning
            WarningDarken = "#B07A2E", // No VDS token (MudBlazor-specific)
            WarningLighten = "#E0B070", // No VDS token (MudBlazor-specific)
            Info = "#5A9ED6", // VDS token: --vds-info
            InfoDarken = "#3A7AB0", // No VDS token (MudBlazor-specific)
            InfoLighten = "#7AB8E0", // No VDS token (MudBlazor-specific)
            Success = "#5CAB7D", // VDS token: --vds-success
            SuccessDarken = "#3A8A5A", // No VDS token (MudBlazor-specific)
            SuccessLighten = "#7AC89D", // No VDS token (MudBlazor-specific)

            // ── Overlays ──
            OverlayDark = "rgba(0, 0, 0, 0.65)", // No VDS token (MudBlazor-specific)
            OverlayLight = "rgba(255, 255, 255, 0.06)", // No VDS token (MudBlazor-specific)

            // ── Hover / Ripple ──
            HoverOpacity = 0.06, // No VDS token (MudBlazor-specific)
            RippleOpacity = 0.12, // No VDS token (MudBlazor-specific)

            // ── Gray scale ──
            GrayLight = "#2E2F42", // VDS token: --vds-bg-4
            GrayDark = "#1E1F2D", // Hex matches --vds-border-subtle (different semantic context)
            GrayDarker = "#0C0D13", // VDS token: --vds-bg-0
        },

        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" }, // VDS token: --vds-font-body
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.5",
            },
            H1 = new H1Typography()
            {
                FontFamily = new[] { "Cinzel", "serif" }, // VDS token: --vds-font-heading
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
            DefaultBorderRadius = "4px", // VDS token: --vds-radius-sm
        },
    };
}
