using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShutdownServerApp
{
    public class GradientPanel : Panel
    {
        public Color ColorTop { get; set; } = Color.DarkSlateBlue;
        public Color ColorBottom { get; set; } = Color.MediumPurple;

        protected override void OnPaint(PaintEventArgs e)
        {
            using (
                LinearGradientBrush brush = new LinearGradientBrush(
                    ClientRectangle,
                    ColorTop,
                    ColorBottom,
                    90F
                )
            )
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            base.OnPaint(e);
        }
    }
}
