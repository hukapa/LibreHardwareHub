using System.Drawing;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls
{
    /// <summary>A navigation entry of the <see cref="SidebarPanel" />; entries sharing a section are listed under one caption.</summary>
    public sealed class SidebarItem
    {
        public SidebarItem(string text, Image image, object tag, string section = null)
        {
            Text = text;
            Image = image;
            Tag = tag;
            Section = section ?? string.Empty;
        }

        public string Text { get; }
        public Image Image { get; }
        public object Tag { get; }
        public string Section { get; }
    }
}
