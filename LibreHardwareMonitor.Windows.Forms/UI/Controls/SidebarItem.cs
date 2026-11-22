using System.Drawing;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls
{
    /// <summary>A navigation entry of the <see cref="SidebarPanel" />; entries sharing a section are listed under one caption.</summary>
    public sealed class SidebarItem
    {
        public SidebarItem(string text, Image image, object tag, string section = null, bool footer = false, string shortcut = null)
        {
            Text = text;
            Image = image;
            Tag = tag;
            Section = section ?? string.Empty;
            Footer = footer;
            Shortcut = shortcut ?? string.Empty;
        }

        public string Text { get; }
        public Image Image { get; }
        public object Tag { get; }
        public string Section { get; }

        /// <summary>True for an entry pinned to the bottom of the pane, outside the scrolling list (it has no section).</summary>
        public bool Footer { get; }

        /// <summary>Keyboard shortcut shown in the tooltip, for example "Ctrl+7"; empty when there is none.</summary>
        public string Shortcut { get; }
    }
}
