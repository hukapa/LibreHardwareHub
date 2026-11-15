using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LibreHardwareMonitor.Windows.Forms.UI.Themes
{
    /// <summary>
    /// Shared Fluent (Windows 11) design primitives: typography, system accent color,
    /// color math, rounded geometry and DWM window effects.
    /// </summary>
    public static class FluentStyle
    {
        private static readonly Color DefaultAccent = Color.FromArgb(0, 103, 192);
        private static Font _uiFont;
        private static Font _uiFontSemibold;
        private static Font _uiFontSmall;
        private static Font _uiFontSmallBold;
        private static Font _uiFontMini;
        private static Font _monoFont;
        private static Font _monoFontSmall;
        private static Font _monoFontKpi;
        private static Color? _systemAccent;

        private static StringFormat _formatEllipsisNoWrap;
        private static StringFormat _formatCenter;
        private static StringFormat _formatFar;
        private static StringFormat _formatNearCenter;
        private static StringFormat _formatFarCenter;

        /// <summary>Corner radius shared by buttons, combo boxes and text fields (logical units; cards use the theme radius + 2).</summary>
        public const int ControlCornerLogical = 8;

        /// <summary>Default UI font: Segoe UI Variable Text when installed, otherwise the system message font.</summary>
        public static Font UiFont => _uiFont ??= CreateUiFont(FontStyle.Regular);

        /// <summary>Semibold variant used for headings and group titles.</summary>
        public static Font UiFontSemibold => _uiFontSemibold ??= CreateUiFont(FontStyle.Bold);

        /// <summary>8pt regular UI font for subtitles, metric labels and badges.</summary>
        public static Font UiFontSmall => _uiFontSmall ??= CreateUiFont(FontStyle.Regular, 8f);

        /// <summary>8pt bold UI font for group headers and category captions.</summary>
        public static Font UiFontSmallBold => _uiFontSmallBold ??= CreateUiFont(FontStyle.Bold, 8f);

        /// <summary>7pt regular UI font for compact core matrix labels.</summary>
        public static Font UiFontMini => _uiFontMini ??= CreateUiFont(FontStyle.Regular, 7f);

        /// <summary>9pt monospace font for telemetry numbers.</summary>
        public static Font MonoFont => _monoFont ??= CreateMonoFont(9f, FontStyle.Bold);

        /// <summary>7.5pt monospace font for compact core load badges.</summary>
        public static Font MonoFontSmall => _monoFontSmall ??= CreateMonoFont(7.5f, FontStyle.Bold);

        /// <summary>11.5pt monospace font for KPI metric values.</summary>
        public static Font MonoFontKpi => _monoFontKpi ??= CreateMonoFont(11.5f, FontStyle.Bold);

        public static StringFormat FormatEllipsisNoWrap => _formatEllipsisNoWrap ??= new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        public static StringFormat FormatCenter => _formatCenter ??= new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None };
        public static StringFormat FormatFar => _formatFar ??= new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None };
        public static StringFormat FormatNearCenter => _formatNearCenter ??= new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None };
        public static StringFormat FormatFarCenter => _formatFarCenter ??= new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None };

        /// <summary>The user's Windows accent color, read once from the DWM registry key.</summary>
        public static Color SystemAccentColor => _systemAccent ??= ReadSystemAccent();

        public static Font CreateUiFont(FontStyle style, float size = 9f, GraphicsUnit unit = GraphicsUnit.Point)
        {
            string family = style == FontStyle.Bold ? "Segoe UI Variable Text Semibold" : "Segoe UI Variable Text";
            if (HasFamily(family))
                return new Font(family, size, FontStyle.Regular, unit);

            if (style == FontStyle.Bold && HasFamily("Segoe UI Semibold"))
                return new Font("Segoe UI Semibold", size, FontStyle.Regular, unit);

            Font system = SystemFonts.MessageBoxFont;
            return new Font(system.FontFamily, size, style, unit);
        }

        public static Font CreateMonoFont(float size, FontStyle style = FontStyle.Bold, GraphicsUnit unit = GraphicsUnit.Point)
        {
            if (HasFamily("Cascadia Mono"))
                return new Font("Cascadia Mono", size, style, unit);
            if (HasFamily("Consolas"))
                return new Font("Consolas", size, style, unit);
            return new Font(FontFamily.GenericMonospace, size, style, unit);
        }

        /// <summary>Large display font for headline numbers (KPI values), semibold where available.</summary>
        public static Font CreateDisplayFont(float size)
        {
            if (HasFamily("Segoe UI Variable Display Semib"))
                return new Font("Segoe UI Variable Display Semib", size, FontStyle.Regular, GraphicsUnit.Point);

            return CreateUiFont(FontStyle.Bold, size);
        }

        /// <summary>Linear blend of <paramref name="top" /> over <paramref name="bottom" /> by <paramref name="amount" /> (0..1).</summary>
        public static Color Blend(Color bottom, Color top, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)Math.Round(bottom.R + ((top.R - bottom.R) * amount)),
                (int)Math.Round(bottom.G + ((top.G - bottom.G) * amount)),
                (int)Math.Round(bottom.B + ((top.B - bottom.B) * amount)));
        }

        /// <summary>Returns the accent adjusted so it keeps contrast against light or dark surfaces.</summary>
        public static Color AccentFor(bool darkSurface)
        {
            Color accent = SystemAccentColor;
            double luminance = RelativeLuminance(accent);
            if (darkSurface && luminance < 0.25)
                return Blend(accent, Color.White, 0.45);

            if (!darkSurface && luminance > 0.45)
                return Blend(accent, Color.Black, 0.35);

            return accent;
        }

        public static double RelativeLuminance(Color color)
        {
            static double Channel(int c)
            {
                double v = c / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
        }

        public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new();
            if (bounds.Width <= 0f || bounds.Height <= 0f)
                return path;

            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0.5f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            RectangleF arc = new(bounds.Location, new SizeF(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF bounds, float radius)
        {
            using GraphicsPath path = RoundedRectangle(bounds, radius);
            using SolidBrush brush = new(color);
            g.FillPath(brush, path);
        }

        public static void DrawRounded(Graphics g, Color color, RectangleF bounds, float radius, float width = 1f)
        {
            using GraphicsPath path = RoundedRectangle(bounds, radius);
            using Pen pen = new(color, width);
            g.DrawPath(pen, path);
        }

        public static void StylePrimaryButton(Button btn, Theme theme)
        {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = theme.AccentColor;
            btn.ForeColor = theme.AccentForegroundColor;
            btn.Font = UiFontSemibold;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Blend(theme.AccentColor, Color.White, 0.15);
            btn.FlatAppearance.MouseDownBackColor = Blend(theme.AccentColor, Color.Black, 0.15);
        }

        public static void StyleSecondaryButton(Button btn, Theme theme)
        {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = theme.SurfaceStrokeColor;
            btn.BackColor = theme.SurfaceColor;
            btn.ForeColor = theme.ForegroundColor;
            btn.Font = UiFont;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = theme.HoverBackgroundColor;
            btn.FlatAppearance.MouseDownBackColor = theme.SelectedBackgroundColor;
        }

        public static void StyleChipButton(Button btn, Theme theme, bool selected)
        {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            if (selected)
            {
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = theme.AccentColor;
                btn.ForeColor = theme.AccentForegroundColor;
                btn.Font = UiFontSemibold;
                btn.FlatAppearance.MouseOverBackColor = Blend(theme.AccentColor, Color.White, 0.15);
                btn.FlatAppearance.MouseDownBackColor = Blend(theme.AccentColor, Color.Black, 0.15);
            }
            else
            {
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = theme.SurfaceStrokeColor;
                btn.BackColor = theme.SurfaceColor;
                btn.ForeColor = theme.ForegroundColor;
                btn.Font = UiFont;
                btn.FlatAppearance.MouseOverBackColor = theme.HoverBackgroundColor;
                btn.FlatAppearance.MouseDownBackColor = theme.SelectedBackgroundColor;
            }
        }

        /// <summary>Asks DWM (Windows 11 build 22000+) to round the window corners and tint its border.</summary>
        public static void ApplyWindowChrome(IntPtr handle, Color borderColor, bool smallCorners = false)
        {
            if (handle == IntPtr.Zero || !IsWindows11())
                return;

            try
            {
                int corner = smallCorners ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

                int border = ColorTranslator.ToWin32(borderColor);
                DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch (DllNotFoundException)
            { }
            catch (EntryPointNotFoundException)
            { }
        }

        public static bool IsWindows11()
        {
            return !Software.OperatingSystem.IsUnix && Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        }

        // Installed font names, read once: FontFamily.Families builds a new array of every installed family on each call.
        private static System.Collections.Generic.HashSet<string> _families;

        private static bool HasFamily(string name)
        {
            _families ??= new System.Collections.Generic.HashSet<string>(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            return _families.Contains(name);
        }

        private static Color ReadSystemAccent()
        {
            if (Software.OperatingSystem.IsUnix)
                return DefaultAccent;

            try
            {
                // AccentColor is stored as 0xAABBGGRR.
                if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr)
                    return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
            catch (System.Security.SecurityException)
            { }

            return DefaultAccent;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_ROUNDSMALL = 3;
    }
}
