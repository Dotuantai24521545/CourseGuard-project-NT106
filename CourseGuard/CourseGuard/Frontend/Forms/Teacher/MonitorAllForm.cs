using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CourseGuard.Backend.Services.Monitoring;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class MonitorAllForm : Form
    {
        private readonly FlowLayoutPanel _flowLayoutPanel = new();
        private readonly ConcurrentDictionary<int, StudentMonitorCard> _studentCards = new();

        public MonitorAllForm()
        {
            Text = "Giám sát tất cả sinh viên";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = MetaTheme.Colors.FormBg;

            _flowLayoutPanel.Dock = DockStyle.Fill;
            _flowLayoutPanel.AutoScroll = true;
            _flowLayoutPanel.Padding = new Padding(16);
            _flowLayoutPanel.BackColor = MetaTheme.Colors.FormBg;
            Controls.Add(_flowLayoutPanel);

            TcpScreenMonitorService.Instance.FrameReceived += Service_FrameReceived;
            FormClosing += MonitorAllForm_FormClosing;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            TcpScreenMonitorService.Instance.StartListening();
        }

        private void MonitorAllForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            TcpScreenMonitorService.Instance.FrameReceived -= Service_FrameReceived;
            foreach (var card in _studentCards.Values)
            {
                card.DisposeImage();
            }
        }

        private void Service_FrameReceived(object? sender, ScreenFrameReceivedEventArgs e)
        {
            try
            {
                using var ms = new MemoryStream(e.JpegBytes);
                using var source = Image.FromStream(ms);
                var frame = new Bitmap(source); // Create a clone

                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() => { if (!IsDisposed) UpdateStudentCard(e.StudentId, e.ExamId, e.AttemptId, frame, e.FrameType); }));
                }
                else
                {
                    UpdateStudentCard(e.StudentId, e.ExamId, e.AttemptId, frame, e.FrameType);
                }
            }
            catch
            {
                // Bỏ qua nếu lỗi
            }
        }

        private void UpdateStudentCard(int studentId, int examId, int attemptId, Image frame, byte frameType)
        {
            if (!_studentCards.TryGetValue(studentId, out var card))
            {
                card = new StudentMonitorCard(studentId, examId, attemptId, this);
                _studentCards[studentId] = card;
                _flowLayoutPanel.Controls.Add(card);
            }
            card.UpdateFrame(frame, frameType);
        }

        private class StudentMonitorCard : Panel
        {
            private readonly PictureBox _pictureBox;
            private readonly Label _lblInfo;
            private readonly int _studentId;
            private readonly int _examId;
            private readonly int _attemptId;

            public StudentMonitorCard(int studentId, int examId, int attemptId, Form parentForm)
            {
                _studentId = studentId;
                _examId = examId;
                _attemptId = attemptId;
                
                Width = 240;
                Height = 180;
                Margin = new Padding(10);
                BackColor = MetaTheme.Colors.CardBg;
                BorderStyle = BorderStyle.FixedSingle;

                _pictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black,
                    Cursor = Cursors.Hand
                };
                _pictureBox.Click += (_, _) => 
                {
                    var popup = new MonitorPopupForm(0, _examId, _studentId, _attemptId);
                    popup.Show(parentForm);
                };

                _lblInfo = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    Text = $"Sinh viên: {studentId}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = MetaTheme.Colors.TextPrimary,
                    BackColor = MetaTheme.Colors.CardBg
                };

                Controls.Add(_pictureBox);
                Controls.Add(_lblInfo);
            }

            public void UpdateFrame(Image frame, byte frameType)
            {
                if (IsDisposed) return;
                Image? old = _pictureBox.Image;
                _pictureBox.Image = frame;
                old?.Dispose();
                
                _pictureBox.Refresh(); // Force redraw

                if (frameType == 1) // Cheating detected
                {
                    _lblInfo.BackColor = Color.Red;
                    _lblInfo.ForeColor = Color.White;
                    _lblInfo.Text = $"🚨 VI PHẠM (HS: {_studentId}) 🚨";
                    BackColor = Color.Red;
                }
                else
                {
                    _lblInfo.BackColor = MetaTheme.Colors.CardBg;
                    _lblInfo.ForeColor = MetaTheme.Colors.TextPrimary;
                    _lblInfo.Text = $"Sinh viên: {_studentId}";
                    BackColor = MetaTheme.Colors.CardBg;
                }
            }

            public void DisposeImage()
            {
                _pictureBox.Image?.Dispose();
            }
        }
    }
}
