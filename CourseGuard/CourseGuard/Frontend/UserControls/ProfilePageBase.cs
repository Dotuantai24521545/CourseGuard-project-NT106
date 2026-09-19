using System;
using System.Drawing;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls
{
    public class ProfilePageBase : UserControl
    {
        protected Control CreateInputGroup(
            string labelText,
            TextBox textBox,
            bool password = false,
            bool blendWithCard = true,
            int inputWidth = 280,
            bool clearInputTag = true)
        {
            Panel wrapper = CreateFieldWrapper(labelText, 74);
            RoundedPanel panel = CreateInputPanel(42, inputWidth);

            textBox.PasswordChar = password ? '*' : '\0';
            textBox.Multiline = false;
            textBox.Font = AppFonts.Body;
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = MetaTheme.Colors.InputBg;
            textBox.ForeColor = MetaTheme.Colors.TextPrimary;
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = Padding.Empty;
            if (clearInputTag)
                textBox.Tag = null;
            WireInputFocus(textBox, panel);

            panel.Controls.Add(textBox);
            wrapper.Controls.Add(panel);
            return wrapper;
        }

        protected Control CreateMultilineInputGroup(
            string labelText,
            TextBox textBox,
            bool blendWithCard = true,
            bool clearTextBoxMargin = false,
            bool clearInputTag = true)
        {
            Panel wrapper = CreateFieldWrapper(labelText, 118);
            RoundedPanel panel = CreateInputPanel(86, 280);
            panel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            wrapper.Resize += (_, _) =>
            {
                if (wrapper.ClientSize.Width > 0)
                    panel.Width = Math.Max(280, wrapper.ClientSize.Width);
            };

            textBox.Multiline = true;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.Font = AppFonts.Body;
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = MetaTheme.Colors.InputBg;
            textBox.ForeColor = MetaTheme.Colors.TextPrimary;
            textBox.Dock = DockStyle.Fill;
            if (clearTextBoxMargin)
                textBox.Margin = Padding.Empty;
            if (clearInputTag)
                textBox.Tag = null;
            WireInputFocus(textBox, panel);

            panel.Controls.Add(textBox);
            wrapper.Controls.Add(panel);
            return wrapper;
        }

        protected Control CreateComboGroup(string labelText, ComboBox comboBox, bool blendWithCard = true)
        {
            Panel wrapper = CreateFieldWrapper(labelText, 74);
            comboBox.Location = new Point(0, 25);
            comboBox.Size = new Size(280, 42);
            comboBox.MinimumSize = new Size(240, 42);
            comboBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            comboBox.Tag = null;
            StudentDropdownStyler.StyleComboBox(comboBox, true, false);
            wrapper.Controls.Add(comboBox);
            return wrapper;
        }

        protected Panel CreateFieldWrapper(string labelText, int height)
        {
            Panel wrapper = new()
            {
                Dock = DockStyle.Fill,
                Height = height,
                Margin = new Padding(0, 0, 15, 10),
                BackColor = AppColors.BgCard,
                Tag = "card"
            };
            wrapper.Controls.Add(new Label
            {
                Text = labelText,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Body,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            return wrapper;
        }

        protected RoundedPanel CreateInputPanel(int height, int width = 280)
        {
            return new RoundedPanel
            {
                CornerRadius = 8,
                BorderColor = MetaTheme.Colors.BorderSoft,
                FillColor = MetaTheme.Colors.InputBg,
                Size = new Size(width, height),
                MinimumSize = new Size(Math.Min(width, 240), height),
                Location = new Point(0, 25),
                Padding = new Padding(12, 9, 12, 9),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                BackColor = Color.Transparent
            };
        }

        protected void WireInputFocus(Control input, RoundedPanel panel)
        {
            input.GotFocus += (_, _) =>
            {
                panel.BorderColor = MetaTheme.Colors.BorderFocus;
                panel.Invalidate();
            };
            input.LostFocus += (_, _) =>
            {
                panel.BorderColor = MetaTheme.Colors.BorderSoft;
                panel.Invalidate();
            };
        }
    }
}
