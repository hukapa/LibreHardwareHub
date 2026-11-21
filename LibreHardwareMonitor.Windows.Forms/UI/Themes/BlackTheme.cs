using System.Drawing;
using System.Linq;

namespace LibreHardwareMonitor.Windows.Forms.UI.Themes
{
    /// <summary>True black (OLED) variant of the Fluent dark theme.</summary>
    public class BlackTheme : LightTheme
    {
        private readonly Color[] _plotColorPalette;
        public override bool IsDark => true;
        public override Color ForegroundColor => Color.FromArgb(230, 232, 236);
        public override Color MutedForegroundColor => Color.FromArgb(127, 133, 146);
        public override Color BackgroundColor => Color.FromArgb(8, 9, 11);
        public override Color SidebarColor => Color.FromArgb(11, 12, 14);
        public override Color SurfaceColor => Color.FromArgb(16, 18, 22);
        public override Color SubtleSurfaceColor => Color.FromArgb(22, 25, 32);
        public override Color AccentColor => Color.FromArgb(76, 154, 255);
        public override Color HyperlinkColor => AccentColor;
        public override Color SelectedForegroundColor => ForegroundColor;
        public override Color SelectedBackgroundColor => Color.FromArgb(27, 30, 37);
        public override Color LineColor => Color.FromArgb(24, 26, 32);
        public override Color StrongLineColor => Color.FromArgb(30, 33, 40);
        public override Color SurfaceStrokeColor => Color.FromArgb(30, 33, 40);
        public override Color[] PlotColorPalette => _plotColorPalette;
        public override Color PlotGridMajorColor => Color.FromArgb(44, 48, 56);
        public override Color PlotGridMinorColor => Color.FromArgb(24, 26, 32);
        public override bool WindowTitlebarFallbackToImmersiveDarkMode => true;

        public BlackTheme() : base("black", "Black")
        {
            string[] colors = {
                "#4CC2FF",
                "#FF6B7A",
                "#5EE36A",
                "#FFD43B",
                "#B98CFF",
                "#33E0D0",
                "#FF9F45",
                "#FF7AD9",
                "#7C9BFF",
                "#9BE564"
            };

            _plotColorPalette = colors.Select(color => ColorTranslator.FromHtml(color)).ToArray();
        }
    }
}
