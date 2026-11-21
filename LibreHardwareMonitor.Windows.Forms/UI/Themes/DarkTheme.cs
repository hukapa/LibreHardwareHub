using System.Drawing;
using System.Linq;

namespace LibreHardwareMonitor.Windows.Forms.UI.Themes
{
    /// <summary>Fluent dark: Mica-like base layer with slightly raised content surfaces.</summary>
    public class DarkTheme : LightTheme
    {
        private readonly Color[] _plotColorPalette;
        public override bool IsDark => true;
        public override Color ForegroundColor => Color.FromArgb(238, 240, 244);
        public override Color MutedForegroundColor => Color.FromArgb(138, 144, 158);
        public override Color BackgroundColor => Color.FromArgb(18, 19, 22);
        public override Color SidebarColor => Color.FromArgb(22, 24, 28);
        public override Color SurfaceColor => Color.FromArgb(27, 29, 35);
        public override Color SubtleSurfaceColor => Color.FromArgb(32, 35, 43);
        public override Color AccentColor => Color.FromArgb(76, 154, 255);
        public override Color HyperlinkColor => AccentColor;
        public override Color SelectedForegroundColor => ForegroundColor;
        public override Color SelectedBackgroundColor => Color.FromArgb(42, 45, 54);
        public override Color LineColor => Color.FromArgb(35, 38, 46);
        public override Color StrongLineColor => Color.FromArgb(40, 43, 52);
        public override Color SurfaceStrokeColor => Color.FromArgb(40, 43, 52);
        public override Color[] PlotColorPalette => _plotColorPalette;
        public override Color PlotGridMajorColor => Color.FromArgb(52, 56, 65);
        public override Color PlotGridMinorColor => Color.FromArgb(38, 42, 49);
        public override bool WindowTitlebarFallbackToImmersiveDarkMode => true;

        public DarkTheme() : base("dark", "Dark")
        {
            string[] colors = {
                "#60CDFF",
                "#FF99A4",
                "#6CCB5F",
                "#C5A3F5",
                "#FFB86B",
                "#4DD0C8",
                "#FCE100",
                "#F7A8DA",
                "#8EA6FF",
                "#A6E07A",
                "#E89CF2",
                "#7FD4E8"
            };

            _plotColorPalette = colors.Select(color => ColorTranslator.FromHtml(color)).ToArray();
        }
    }
}
