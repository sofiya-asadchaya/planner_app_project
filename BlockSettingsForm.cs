using System;
using System.Drawing;
using System.Windows.Forms;

namespace Planner_app
{
    public partial class BlockSettingsForm : Form
    {
        private TextBox textBox;
        private FlowLayoutPanel colorPalette;
        private Button okButton;
        private Panel selectedColorPanel;

        public string BlockText => textBox.Text;
        public Color BlockColor { get; private set; } = ColorTranslator.FromHtml("#A9B700");

        public BlockSettingsForm()
        {
            InitializeComponent();

            // TextBox for block text
            textBox = new TextBox
            {
                PlaceholderText = "Enter block text",
                Dock = DockStyle.Top,
                Margin = new Padding(10),
                Font = new Font("Arial", 10)
            };

            // Color palette as a flow layout
            colorPalette = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(10)
            };

            // OK button
            okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Top,
                Margin = new Padding(10)
            };

            okButton.Click += (s, e) => DialogResult = DialogResult.OK;

            // Add controls to the form
            Controls.Add(okButton);
            Controls.Add(colorPalette);
            Controls.Add(selectedColorPanel);
            Controls.Add(textBox);
        }

        
    }
}
