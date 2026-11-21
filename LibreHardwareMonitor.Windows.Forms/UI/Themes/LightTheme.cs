using System.Drawing;

namespace LibreHardwareMonitor.Windows.Forms.UI.Themes
{
    /// <summary>Fluent light: quiet neutral canvas with white content surfaces and hairline borders.</summary>
    public class LightTheme : Theme
    {
        private readonly Color[] _plotColorPalette = new Color[] {
            ColorTranslator.FromHtml("#0067C0"),
            ColorTranslator.FromHtml("#C42B1C"),
            ColorTranslator.FromHtml("#0F7B0F"),
            ColorTranslator.FromHtml("#8764B8"),
            ColorTranslator.FromHtml("#CA5010"),
            ColorTranslator.FromHtml("#038387"),
            ColorTranslator.FromHtml("#9D5D00"),
            ColorTranslator.FromHtml("#E3008C"),
            ColorTranslator.FromHtml("#4F6BED"),
            ColorTranslator.FromHtml("#498205"),
            ColorTranslator.FromHtml("#B146C2"),
            ColorTranslator.FromHtml("#005B70"),
            ColorTranslator.FromHtml("#A4262C")
        };

        public LightTheme(string id, string displayName) : base(id, displayName) { }
        public override Color ForegroundColor => Color.FromArgb(22, 24, 29);
        public override Color MutedForegroundColor => Color.FromArgb(102, 112, 133);
        public override Color BackgroundColor => Color.FromArgb(245, 246, 248);
        public override Color SidebarColor => Color.FromArgb(240, 242, 245);
        public override Color SurfaceColor => Color.FromArgb(255, 255, 255);
        public override Color SubtleSurfaceColor => Color.FromArgb(246, 247, 249);
        public override Color HyperlinkColor => AccentColor;
        public override Color SelectedForegroundColor => ForegroundColor;
        public override Color SelectedBackgroundColor => Color.FromArgb(236, 238, 242);
        public override Color LineColor => Color.FromArgb(238, 240, 243);
        public override Color StrongLineColor => Color.FromArgb(228, 231, 236);
        public override Color SurfaceStrokeColor => Color.FromArgb(228, 231, 236);
        public override Color[] PlotColorPalette => _plotColorPalette;
        public override Color PlotGridMajorColor => Color.FromArgb(222, 225, 230);
        public override Color PlotGridMinorColor => Color.FromArgb(238, 240, 243);
        public override bool WindowTitlebarFallbackToImmersiveDarkMode => false;

        public LightTheme() : base("light", "Light") { }
    }
}
