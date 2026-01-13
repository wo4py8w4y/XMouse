namespace XMouse
{
    public class DistanceForm : Form
    {
        private readonly Label lblTotal;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private double total = 0.0;

        public DistanceForm()
        {
            Text = "XMouse - Distance";
            Size = new Size(300, 200);
            StartPosition = FormStartPosition.CenterScreen;

            lblTotal = new Label()
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            Controls.Add(lblTotal);

            // Placeholder for a simple graph area
            PictureBox graph = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(graph);

            refreshTimer = new System.Windows.Forms.Timer() { Interval = 250 };
            refreshTimer.Tick += (s, e) => UpdateDisplay();
            refreshTimer.Start();
        }

        public void SetTotal(double value)
        {
            total = value;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (lblTotal.InvokeRequired)
            {
                _ = lblTotal.BeginInvoke(() => lblTotal.Text = FormatTotal(total));
            }
            else
            {
                lblTotal.Text = FormatTotal(total);
            }
        }

        private static string FormatTotal(double pixels)
        {
            return Math.Round(pixels).ToString() + " px";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
