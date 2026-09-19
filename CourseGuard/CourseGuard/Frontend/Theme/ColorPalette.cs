/*
 * ColorPalette.cs
 * 
 * Layer: Presentation (Theme)
 * Redirects to MetaTheme dark tokens. Kept for backward compatibility.
 */
using System.Drawing;

namespace CourseGuard.Frontend.Theme
{
    public static class ColorPalette
    {
        public struct LightMode
        {
            // "LightMode" now returns dark-mode values for consistency
            public static readonly Color Base          = MetaTheme.Colors.FormBg;
            public static readonly Color Secondary     = MetaTheme.Colors.CardBg;
            public static readonly Color Accent        = MetaTheme.Colors.Accent;

            public static readonly Color TextPrimary   = MetaTheme.Colors.TextPrimary;
            public static readonly Color TextSecondary = MetaTheme.Colors.TextSecondary;

            public static readonly Color Border        = MetaTheme.Colors.Border;

            public static readonly Color Hover         = MetaTheme.Colors.AccentHover;
            public static readonly Color Active        = MetaTheme.Colors.AccentPressed;
        }

        public struct DarkMode
        {
            public static readonly Color Base          = MetaTheme.Colors.FormBg;
            public static readonly Color Secondary     = MetaTheme.Colors.CardBg;
            public static readonly Color Accent        = MetaTheme.Colors.Accent;

            public static readonly Color TextPrimary   = MetaTheme.Colors.TextPrimary;
            public static readonly Color TextSecondary = MetaTheme.Colors.TextSecondary;

            public static readonly Color Border        = MetaTheme.Colors.Border;

            public static readonly Color Hover         = MetaTheme.Colors.AccentHover;
            public static readonly Color Active        = MetaTheme.Colors.AccentPressed;
        }

        public struct Status
        {
            public static readonly Color SuccessLight  = MetaTheme.Colors.Success;
            public static readonly Color WarningLight  = MetaTheme.Colors.Warning;
            public static readonly Color ErrorLight    = MetaTheme.Colors.Critical;
            public static readonly Color InfoLight     = MetaTheme.Colors.Info;

            public static readonly Color SuccessDark   = MetaTheme.Colors.Success;
            public static readonly Color WarningDark   = MetaTheme.Colors.Warning;
            public static readonly Color ErrorDark     = MetaTheme.Colors.Critical;
            public static readonly Color InfoDark      = MetaTheme.Colors.Info;
        }
    }
}
