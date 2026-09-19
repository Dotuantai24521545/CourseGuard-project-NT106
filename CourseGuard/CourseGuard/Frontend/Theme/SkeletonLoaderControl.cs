using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public enum SkeletonType
    {
        DashboardOverview,
        StudentOverviewDashboard,
        TableWithToolbar,
        TableWithToolbarAndDetailPanel,
        CourseGrid,
        CourseCardGrid,
        ExamListWithToolbar,
        ResultTable,
        FormWithTable,
        DetailPanel,
        CalendarView,
        ChatLayout,
        NotificationList,
        ProfileForm
    }

    public enum SkeletonShapeType
    {
        RoundedRectangle,
        Circle
    }

    public sealed class SkeletonShape
    {
        public Rectangle Bounds { get; }
        public int Radius { get; }
        public SkeletonShapeType ShapeType { get; }

        public SkeletonShape(Rectangle bounds, int radius = 8, SkeletonShapeType shapeType = SkeletonShapeType.RoundedRectangle)
        {
            Bounds = bounds;
            Radius = radius;
            ShapeType = shapeType;
        }
    }

    public sealed class SkeletonTemplate
    {
        public IReadOnlyList<SkeletonShape> Shapes { get; }

        public SkeletonTemplate(IReadOnlyList<SkeletonShape> shapes)
        {
            Shapes = shapes;
        }
    }

    public static class SkeletonTemplateFactory
    {
        private const int Padding = 24;
        private const int Gap = 16;
        private const int RadiusSm = 6;
        private const int RadiusMd = 10;
        private const int RadiusLg = 16;

        public static SkeletonTemplate Create(SkeletonType type, Rectangle viewport)
        {
            Rectangle content = Deflate(viewport, Padding);
            List<SkeletonShape> shapes = new();

            switch (type)
            {
                case SkeletonType.StudentOverviewDashboard:
                case SkeletonType.DashboardOverview:
                    AddDashboardOverview(shapes, content);
                    break;
                case SkeletonType.CourseCardGrid:
                case SkeletonType.CourseGrid:
                    AddCourseGrid(shapes, content);
                    break;
                case SkeletonType.ExamListWithToolbar:
                    AddExamListWithToolbar(shapes, content);
                    break;
                case SkeletonType.ResultTable:
                    AddResultTable(shapes, content);
                    break;
                case SkeletonType.TableWithToolbar:
                    AddTableWithToolbar(shapes, content);
                    break;
                case SkeletonType.TableWithToolbarAndDetailPanel:
                    AddTableWithToolbarAndDetailPanel(shapes, content);
                    break;
                case SkeletonType.FormWithTable:
                    AddFormWithTable(shapes, content);
                    break;
                case SkeletonType.DetailPanel:
                    AddDetailPanel(shapes, content);
                    break;
                case SkeletonType.CalendarView:
                    AddCalendarView(shapes, content);
                    break;
                case SkeletonType.ChatLayout:
                    AddChatLayout(shapes, content);
                    break;
                case SkeletonType.NotificationList:
                    AddNotificationList(shapes, content);
                    break;
                case SkeletonType.ProfileForm:
                    AddProfileForm(shapes, content);
                    break;
                default:
                    AddTableWithToolbar(shapes, content);
                    break;
            }

            return new SkeletonTemplate(shapes);
        }

        private static void AddDashboardOverview(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(280, c.Width), 30);

            int statTop = c.Top + 58;
            int columns = c.Width < 900 ? 2 : 4;
            int rows = columns == 2 ? 2 : 1;
            int statHeight = 130;
            int statWidth = (c.Width - Gap * (columns - 1)) / columns;

            for (int i = 0; i < columns * rows && i < 4; i++)
            {
                int col = i % columns;
                int row = i / columns;
                Rectangle card = new Rectangle(c.Left + col * (statWidth + Gap), statTop + row * (statHeight + Gap), statWidth, statHeight);
                AddStatCard(shapes, card);
            }

            int lowerTop = statTop + rows * statHeight + (rows - 1) * Gap + 24;
            int lowerHeight = Math.Max(120, c.Bottom - lowerTop);
            int leftWidth = c.Width < 760 ? c.Width : (int)(c.Width * 0.66f) - Gap / 2;
            Rectangle notice = new Rectangle(c.Left, lowerTop, Math.Max(80, leftWidth), c.Width < 760 ? Math.Max(120, lowerHeight * 58 / 100) : lowerHeight);
            Rectangle activity = c.Width < 760
                ? new Rectangle(c.Left, notice.Bottom + Gap, c.Width, Math.Max(100, c.Bottom - notice.Bottom - Gap))
                : new Rectangle(c.Left + leftWidth + Gap, lowerTop, Math.Max(80, c.Width - leftWidth - Gap), lowerHeight);
            shapes.Add(new SkeletonShape(notice, RadiusLg));
            shapes.Add(new SkeletonShape(activity, RadiusLg));

            AddLine(shapes, notice.Left + 20, notice.Top + 18, Math.Min(220, notice.Width - 40), 20);
            AddDataTable(shapes, new Rectangle(notice.Left + 20, notice.Top + 56, notice.Width - 40, notice.Height - 76), new[] { 20, 48, 32 }, 5);

            AddLine(shapes, activity.Left + 20, activity.Top + 18, Math.Min(180, activity.Width - 40), 20);
            int y = activity.Top + 60;
            for (int i = 0; i < 4 && y + 42 <= activity.Bottom - 12; i++)
            {
                shapes.Add(new SkeletonShape(new Rectangle(activity.Left + 20, y + 6, 12, 12), 6, SkeletonShapeType.Circle));
                AddLine(shapes, activity.Left + 46, y, Math.Min(190, activity.Width - 68), 14);
                AddLine(shapes, activity.Left + 46, y + 24, Math.Min(120, activity.Width - 68), 10);
                y += 58;
            }
        }

        private static void AddTableWithToolbar(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(280, c.Width), 28);
            shapes.Add(new SkeletonShape(new Rectangle(c.Left, c.Top + 48, Math.Min(360, c.Width - 116), 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Right - 100, c.Top + 48, 100, 36), RadiusMd));
            AddDataTable(shapes, new Rectangle(c.Left, c.Top + 108, c.Width, c.Height - 108), new[] { 28, 22, 18, 16, 16 }, 8);
        }

        private static void AddExamListWithToolbar(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(260, c.Width - 170), 30);
            shapes.Add(new SkeletonShape(new Rectangle(Math.Max(c.Left, c.Right - 150), c.Top + 2, Math.Min(150, c.Width), 36), RadiusMd));
            AddDataTable(shapes, new Rectangle(c.Left, c.Top + 56, c.Width, c.Height - 56), new[] { 28, 24, 18, 12, 18 }, 9);
        }

        private static void AddResultTable(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(240, c.Width - 170), 30);
            shapes.Add(new SkeletonShape(new Rectangle(Math.Max(c.Left, c.Right - 150), c.Top + 2, Math.Min(150, c.Width), 36), RadiusMd));
            AddDataTable(shapes, new Rectangle(c.Left, c.Top + 56, c.Width, c.Height - 56), new[] { 28, 24, 16, 12, 20 }, 9);
        }

        private static void AddTableWithToolbarAndDetailPanel(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(330, c.Width), 30);

            int toolbarTop = c.Top + 48;
            int buttonWidth = 100;
            int joinWidth = 140;
            int detailWidth = 120;
            int searchWidth = Math.Min(350, Math.Max(180, c.Width - buttonWidth * 2 - detailWidth - joinWidth - Gap * 5));

            shapes.Add(new SkeletonShape(new Rectangle(c.Left, toolbarTop, searchWidth, 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Left + searchWidth + 10, toolbarTop, buttonWidth, 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Left + searchWidth + buttonWidth + 20, toolbarTop, buttonWidth, 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Right - detailWidth - joinWidth - 10, toolbarTop, detailWidth, 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Right - joinWidth, toolbarTop, joinWidth, 36), RadiusMd));

            int detailHeight = Math.Min(155, Math.Max(120, c.Height / 4));
            int tableTop = c.Top + 96;
            int detailTop = c.Bottom - detailHeight;
            Rectangle tableArea = new Rectangle(c.Left, tableTop, c.Width, Math.Max(120, detailTop - tableTop - 12));
            AddCourseTable(shapes, tableArea);

            Rectangle detail = new Rectangle(c.Left, detailTop, c.Width, detailHeight);
            shapes.Add(new SkeletonShape(detail, RadiusMd));
            AddLine(shapes, detail.Left + 18, detail.Top + 18, Math.Min(300, detail.Width - 36), 24);
            AddLine(shapes, detail.Left + 18, detail.Top + 56, Math.Min(240, detail.Width - 36), 14);
            AddLine(shapes, detail.Left + 18, detail.Top + 84, Math.Min(320, detail.Width - 36), 14);
            AddLine(shapes, detail.Left + 18, detail.Top + 112, Math.Min(180, detail.Width - 36), 14);
        }

        private static void AddCourseTable(List<SkeletonShape> shapes, Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0)
                return;

            int[] weights = { 30, 22, 16, 16, 16 };
            int x = area.Left;
            int headerHeight = 38;
            for (int i = 0; i < weights.Length; i++)
            {
                int width = i == weights.Length - 1
                    ? area.Right - x
                    : area.Width * weights[i] / 100;
                shapes.Add(new SkeletonShape(new Rectangle(x, area.Top, Math.Max(24, width - 4), headerHeight), RadiusSm));
                x += width;
            }

            int rowTop = area.Top + headerHeight + 12;
            int rowHeight = 30;
            int rowGap = 12;
            int rows = Math.Min(10, Math.Max(4, (area.Bottom - rowTop) / (rowHeight + rowGap)));
            for (int row = 0; row < rows; row++)
            {
                x = area.Left;
                int y = rowTop + row * (rowHeight + rowGap);
                for (int col = 0; col < weights.Length; col++)
                {
                    int width = col == weights.Length - 1
                        ? area.Right - x
                        : area.Width * weights[col] / 100;
                    int cellWidth = Math.Max(24, width - 18);
                    if (col == 2)
                        cellWidth = Math.Min(cellWidth, 96);
                    shapes.Add(new SkeletonShape(new Rectangle(x, y, cellWidth, rowHeight), RadiusSm));
                    x += width;
                }
            }
        }

        private static void AddCourseGrid(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(320, c.Width), 30);
            shapes.Add(new SkeletonShape(new Rectangle(c.Left, c.Top + 52, Math.Min(390, c.Width - 116), 38), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(c.Left + Math.Min(406, c.Width - 104), c.Top + 52, 104, 38), RadiusMd));

            int top = c.Top + 118;
            int columns = c.Width >= 1080 ? 4 : c.Width >= 760 ? 3 : 2;
            int cardWidth = (c.Width - Gap * (columns - 1)) / columns;
            int rows = columns == 2 ? 2 : 1;
            int availableHeight = Math.Max(230, c.Bottom - top);
            int cardHeight = Math.Min(286, (availableHeight - Gap * (rows - 1)) / rows);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= 4)
                        return;

                    Rectangle card = new Rectangle(c.Left + col * (cardWidth + Gap), top + row * (cardHeight + Gap), cardWidth, cardHeight);
                    AddCourseCard(shapes, card);
                }
            }
        }

        private static void AddFormWithTable(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(260, c.Width), 28);
            int formWidth = Math.Min(360, Math.Max(260, c.Width / 3));
            Rectangle form = new Rectangle(c.Left, c.Top + 54, formWidth, c.Height - 54);
            Rectangle table = new Rectangle(form.Right + Gap, c.Top + 54, c.Width - formWidth - Gap, c.Height - 54);

            shapes.Add(new SkeletonShape(form, RadiusLg));
            int y = form.Top + 24;
            for (int i = 0; i < 5; i++)
            {
                AddLine(shapes, form.Left + 20, y, 120, 14);
                shapes.Add(new SkeletonShape(new Rectangle(form.Left + 20, y + 24, form.Width - 40, 36), RadiusMd));
                y += 76;
            }
            shapes.Add(new SkeletonShape(new Rectangle(form.Left + 20, form.Bottom - 54, 130, 36), RadiusMd));

            AddTableRows(shapes, table, 8);
        }

        private static void AddDetailPanel(List<SkeletonShape> shapes, Rectangle c)
        {
            shapes.Add(new SkeletonShape(new Rectangle(c.Left, c.Top, c.Width, c.Height), RadiusLg));
            AddLine(shapes, c.Left + 24, c.Top + 24, Math.Min(280, c.Width - 48), 28);
            AddLine(shapes, c.Left + 24, c.Top + 70, Math.Min(180, c.Width - 48), 16);
            int y = c.Top + 112;
            for (int i = 0; i < 6; i++)
            {
                AddLine(shapes, c.Left + 24, y, Math.Min(c.Width - 48, i == 5 ? 360 : c.Width - 80), 14);
                y += 28;
            }
            shapes.Add(new SkeletonShape(new Rectangle(c.Left + 24, c.Bottom - 58, 132, 36), RadiusMd));
        }

        private static void AddCalendarView(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(160, c.Width), 30);
            shapes.Add(new SkeletonShape(new Rectangle(c.Left, c.Top + 50, Math.Min(200, c.Width), 36), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(Math.Max(c.Left, c.Right - 150), c.Top + 50, Math.Min(150, c.Width), 36), RadiusMd));
            AddDataTable(shapes, new Rectangle(c.Left, c.Top + 104, c.Width, c.Height - 104), new[] { 28, 22, 18, 18, 14 }, 8);
        }

        private static void AddChatLayout(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(160, c.Width), 30);

            Rectangle body = new Rectangle(c.Left, c.Top + 56, c.Width, Math.Max(0, c.Height - 56));
            int listWidth = Math.Min(300, Math.Max(220, c.Width / 3));
            Rectangle list = new Rectangle(body.Left, body.Top, listWidth, body.Height);
            Rectangle chat = new Rectangle(list.Right + Gap, body.Top, body.Width - listWidth - Gap, body.Height);

            shapes.Add(new SkeletonShape(list, RadiusLg));
            if (chat.Width <= 120)
                return;

            shapes.Add(new SkeletonShape(new Rectangle(chat.Left, chat.Top, chat.Width, Math.Max(0, chat.Height - 56)), RadiusLg));

            int y = list.Top + 20;
            for (int i = 0; i < 7; i++)
            {
                shapes.Add(new SkeletonShape(new Rectangle(list.Left + 18, y, 36, 36), 18, SkeletonShapeType.Circle));
                AddLine(shapes, list.Left + 68, y + 4, list.Width - 92, 12);
                AddLine(shapes, list.Left + 68, y + 24, list.Width - 132, 10);
                y += 62;
            }

            AddLine(shapes, chat.Left + 24, chat.Top + 20, Math.Min(260, chat.Width - 48), 18);

            y = chat.Top + 64;
            for (int i = 0; i < 5; i++)
            {
                int bubbleWidth = (int)(chat.Width * (i % 2 == 0 ? 0.56f : 0.42f));
                int x = i % 2 == 0 ? chat.Left + 24 : chat.Right - bubbleWidth - 24;
                shapes.Add(new SkeletonShape(new Rectangle(x, y, bubbleWidth, 42), RadiusLg));
                y += 72;
            }
            shapes.Add(new SkeletonShape(new Rectangle(chat.Left, chat.Bottom - 36, Math.Max(0, chat.Width - 112), 34), RadiusMd));
            shapes.Add(new SkeletonShape(new Rectangle(chat.Right - 100, chat.Bottom - 37, 100, 36), RadiusMd));
        }

        private static void AddNotificationList(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(180, c.Width - 220), 30);
            shapes.Add(new SkeletonShape(new Rectangle(Math.Max(c.Left, c.Right - 200), c.Top + 2, Math.Min(200, c.Width), 36), RadiusMd));
            AddDataTable(shapes, new Rectangle(c.Left, c.Top + 56, c.Width, c.Height - 56), new[] { 18, 24, 42, 16 }, 9);
        }

        private static void AddProfileForm(List<SkeletonShape> shapes, Rectangle c)
        {
            AddLine(shapes, c.Left, c.Top, Math.Min(220, c.Width), 30);
            AddLine(shapes, c.Left, c.Top + 42, Math.Min(420, c.Width), 14);

            int top = c.Top + 90;
            if (top >= c.Bottom)
                return;

            bool twoColumns = c.Width >= 760;
            int colWidth = twoColumns ? (c.Width - Gap) / 2 : c.Width;
            int left = c.Left;
            int right = twoColumns ? c.Left + colWidth + Gap : c.Left;

            if (twoColumns)
            {
                Rectangle personal = new Rectangle(left, top, colWidth, Math.Min(420, Math.Max(240, c.Bottom - top)));
                Rectangle academic = new Rectangle(right, top, colWidth, Math.Min(260, Math.Max(210, c.Bottom - top)));
                AddProfilePersonalCard(shapes, personal);
                AddProfileAcademicCard(shapes, academic);

                int lowerTop = top + Math.Max(personal.Height, academic.Height) + Gap;
                if (lowerTop < c.Bottom - 80)
                {
                    int lowerHeight = Math.Max(160, c.Bottom - lowerTop);
                    Rectangle activity = new Rectangle(left, lowerTop, colWidth, lowerHeight);
                    Rectangle security = new Rectangle(right, lowerTop, colWidth, lowerHeight);
                    AddProfileActivityCard(shapes, activity);
                    AddProfileSecurityCard(shapes, security);
                }
                return;
            }

            int y = top;
            Rectangle personalCard = new Rectangle(left, y, colWidth, 390);
            AddProfilePersonalCard(shapes, personalCard);
            y = personalCard.Bottom + Gap;

            if (y < c.Bottom - 120)
            {
                Rectangle academicCard = new Rectangle(left, y, colWidth, 250);
                AddProfileAcademicCard(shapes, academicCard);
                y = academicCard.Bottom + Gap;
            }

            if (y < c.Bottom - 100)
                AddProfileSecurityCard(shapes, new Rectangle(left, y, colWidth, Math.Max(160, c.Bottom - y)));
        }

        private static void AddProfilePersonalCard(List<SkeletonShape> shapes, Rectangle card)
        {
            if (card.Width <= 0 || card.Height <= 0)
                return;

            shapes.Add(new SkeletonShape(card, RadiusLg));
            int pad = 22;
            Rectangle avatar = new Rectangle(card.Left + pad, card.Top + pad, 78, 78);
            shapes.Add(new SkeletonShape(avatar, 39, SkeletonShapeType.Circle));
            AddLine(shapes, avatar.Right + 18, card.Top + pad + 12, Math.Min(220, card.Right - avatar.Right - 40), 20);
            AddLine(shapes, avatar.Right + 18, card.Top + pad + 46, Math.Min(140, card.Right - avatar.Right - 40), 14);
            shapes.Add(new SkeletonShape(new Rectangle(avatar.Right + 18, card.Top + pad + 72, Math.Min(150, card.Right - avatar.Right - 40), 34), RadiusMd));

            int y = card.Top + pad + 122;
            for (int i = 0; i < 7 && y + 24 <= card.Bottom - 60; i++)
            {
                AddLine(shapes, card.Left + pad, y + 5, 110, 12);
                AddLine(shapes, card.Left + pad + 140, y, Math.Min(230, card.Right - card.Left - pad * 2 - 140), 18);
                y += 34;
            }

            if (card.Bottom - y > 48)
                shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, card.Bottom - pad - 40, 170, 40), RadiusMd));
        }

        private static void AddProfileAcademicCard(List<SkeletonShape> shapes, Rectangle card)
        {
            if (card.Width <= 0 || card.Height <= 0)
                return;

            shapes.Add(new SkeletonShape(card, RadiusLg));
            int pad = 22;
            AddLine(shapes, card.Left + pad, card.Top + pad, Math.Min(190, card.Width - pad * 2), 20);

            int metricTop = card.Top + pad + 52;
            int metricGap = 12;
            int metricWidth = Math.Max(80, (card.Width - pad * 2 - metricGap) / 2);
            int metricHeight = 82;
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2;
                int row = i / 2;
                Rectangle metric = new Rectangle(card.Left + pad + col * (metricWidth + metricGap), metricTop + row * (metricHeight + metricGap), metricWidth, metricHeight);
                if (metric.Bottom > card.Bottom - pad)
                    continue;

                shapes.Add(new SkeletonShape(metric, RadiusMd));
                shapes.Add(new SkeletonShape(new Rectangle(metric.Left + 12, metric.Top + 14, 32, 32), 16, SkeletonShapeType.Circle));
                AddLine(shapes, metric.Left + 54, metric.Top + 12, Math.Min(90, metric.Width - 68), 12);
                AddLine(shapes, metric.Left + 54, metric.Top + 34, Math.Min(60, metric.Width - 68), 22);
                AddLine(shapes, metric.Left + 54, metric.Top + 62, Math.Min(100, metric.Width - 68), 10);
            }
        }

        private static void AddProfileActivityCard(List<SkeletonShape> shapes, Rectangle card)
        {
            if (card.Width <= 0 || card.Height <= 0)
                return;

            shapes.Add(new SkeletonShape(card, RadiusLg));
            int pad = 22;
            AddLine(shapes, card.Left + pad, card.Top + pad, Math.Min(190, card.Width - pad * 2), 20);

            int y = card.Top + pad + 50;
            for (int i = 0; i < 4 && y + 42 <= card.Bottom - pad; i++)
            {
                shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, y + 6, 10, 10), 5, SkeletonShapeType.Circle));
                AddLine(shapes, card.Left + pad + 24, y, Math.Min(260, card.Width - pad * 2 - 24), 14);
                AddLine(shapes, card.Left + pad + 24, y + 24, Math.Min(130, card.Width - pad * 2 - 24), 10);
                y += 52;
            }
        }

        private static void AddProfileSecurityCard(List<SkeletonShape> shapes, Rectangle card)
        {
            if (card.Width <= 0 || card.Height <= 0)
                return;

            shapes.Add(new SkeletonShape(card, RadiusLg));
            int pad = 22;
            AddLine(shapes, card.Left + pad, card.Top + pad, Math.Min(210, card.Width - pad * 2), 20);

            int inputWidth = Math.Min(320, card.Width - pad * 2);
            int y = card.Top + pad + 52;
            for (int i = 0; i < 3 && y + 58 <= card.Bottom - pad; i++)
            {
                AddLine(shapes, card.Left + pad, y, Math.Min(170, inputWidth), 12);
                shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, y + 24, inputWidth, 38), RadiusMd));
                y += 76;
            }

            if (y + 40 <= card.Bottom - pad)
                shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, y, Math.Min(150, inputWidth), 38), RadiusMd));
        }

        private static void AddCourseCard(List<SkeletonShape> shapes, Rectangle card)
        {
            shapes.Add(new SkeletonShape(card, RadiusLg));
            int pad = 18;
            int innerWidth = card.Width - pad * 2;
            shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, card.Top + pad, innerWidth, 88), 12));
            int y = card.Top + pad + 110;
            AddLine(shapes, card.Left + pad, y, (int)(innerWidth * 0.78f), 18);
            y += 34;
            AddLine(shapes, card.Left + pad, y, innerWidth, 12);
            y += 22;
            AddLine(shapes, card.Left + pad, y, (int)(innerWidth * 0.62f), 12);
            y += 34;
            shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, y, innerWidth, 10), RadiusSm));
            shapes.Add(new SkeletonShape(new Rectangle(card.Left + pad, card.Bottom - pad - 36, Math.Min(124, innerWidth), 36), RadiusMd));
        }

        private static void AddStatCard(List<SkeletonShape> shapes, Rectangle card)
        {
            shapes.Add(new SkeletonShape(card, 14));
            shapes.Add(new SkeletonShape(new Rectangle(card.Left + 22, card.Top + 22, 42, 42), 21, SkeletonShapeType.Circle));
            AddLine(shapes, card.Left + 78, card.Top + 26, card.Width - 160, 14);
            AddLine(shapes, card.Left + 78, card.Top + 56, Math.Min(120, card.Width - 170), 28);
            AddLine(shapes, card.Left + 78, card.Bottom - 40, Math.Min(150, card.Width - 170), 14);
            int chartLeft = card.Right - 82;
            for (int i = 0; i < 6; i++)
                shapes.Add(new SkeletonShape(new Rectangle(chartLeft + i * 10, card.Top + 46 - i % 3 * 6, 6, 52 + i % 3 * 6), 3));
        }

        private static void AddTableRows(List<SkeletonShape> shapes, Rectangle area, int rows)
        {
            if (area.Width <= 0 || area.Height <= 0)
                return;

            shapes.Add(new SkeletonShape(new Rectangle(area.Left, area.Top, area.Width, 42), RadiusMd));
            int y = area.Top + 58;
            for (int i = 0; i < rows && y + 34 <= area.Bottom; i++)
            {
                shapes.Add(new SkeletonShape(new Rectangle(area.Left, y, area.Width, 34), RadiusMd));
                y += 48;
            }
        }

        private static void AddDataTable(List<SkeletonShape> shapes, Rectangle area, int[] columnWeights, int rows)
        {
            if (area.Width <= 0 || area.Height <= 0)
                return;

            int headerHeight = 38;
            AddWeightedRow(shapes, area.Left, area.Top, area.Width, headerHeight, columnWeights, true);

            int y = area.Top + headerHeight + 12;
            int rowHeight = 30;
            int rowGap = 12;
            int maxRows = Math.Min(rows, Math.Max(3, (area.Bottom - y) / (rowHeight + rowGap)));
            for (int row = 0; row < maxRows; row++)
            {
                AddWeightedRow(shapes, area.Left, y, area.Width, rowHeight, columnWeights, false);
                y += rowHeight + rowGap;
            }
        }

        private static void AddWeightedRow(List<SkeletonShape> shapes, int left, int top, int totalWidth, int height, int[] weights, bool header)
        {
            int x = left;
            int weightSum = Math.Max(1, weights.Sum());
            for (int i = 0; i < weights.Length; i++)
            {
                int columnWidth = i == weights.Length - 1
                    ? left + totalWidth - x
                    : totalWidth * weights[i] / weightSum;

                int cellWidth = header
                    ? Math.Max(24, columnWidth - 4)
                    : Math.Max(24, columnWidth - 18);

                if (!header && i >= 2)
                    cellWidth = Math.Min(cellWidth, Math.Max(56, columnWidth - 28));

                shapes.Add(new SkeletonShape(new Rectangle(x, top, cellWidth, height), header ? RadiusSm : RadiusMd));
                x += columnWidth;
            }
        }

        private static void AddListRows(List<SkeletonShape> shapes, Rectangle area, int rows)
        {
            int y = area.Top;
            for (int i = 0; i < rows && y + 58 <= area.Bottom; i++)
            {
                shapes.Add(new SkeletonShape(new Rectangle(area.Left, y, area.Width, 58), RadiusMd));
                shapes.Add(new SkeletonShape(new Rectangle(area.Left + 16, y + 16, 28, 28), 14, SkeletonShapeType.Circle));
                AddLine(shapes, area.Left + 58, y + 12, Math.Min(260, area.Width - 90), 12);
                AddLine(shapes, area.Left + 58, y + 34, Math.Min(380, area.Width - 100), 10);
                y += 72;
            }
        }

        private static void AddLine(List<SkeletonShape> shapes, int x, int y, int width, int height)
        {
            shapes.Add(new SkeletonShape(new Rectangle(x, y, Math.Max(8, width), height), Math.Min(RadiusSm, height / 2)));
        }

        private static Rectangle Deflate(Rectangle rectangle, int padding)
        {
            return new Rectangle(
                rectangle.Left + padding,
                rectangle.Top + padding,
                Math.Max(0, rectangle.Width - padding * 2),
                Math.Max(0, rectangle.Height - padding * 2));
        }
    }

    public sealed class SkeletonLoaderControl : UserControl
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private int _shimmerX;
        private SkeletonType _skeletonType = SkeletonType.TableWithToolbar;

        [DefaultValue(SkeletonType.TableWithToolbar)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkeletonType SkeletonType
        {
            get => _skeletonType;
            set
            {
                _skeletonType = value;
                ResetShimmer();
                Invalidate();
            }
        }

        public SkeletonLoaderControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = GetSkeletonPageBackground();

            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                Start();
            else
                Stop();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ResetShimmer();
            Invalidate();
        }

        public void Start()
        {
            BackColor = GetSkeletonPageBackground();
            ResetShimmer();
            if (!_animationTimer.Enabled)
                _animationTimer.Start();
            Invalidate();
        }

        public void Stop()
        {
            if (_animationTimer.Enabled)
                _animationTimer.Stop();
        }

        private void ResetShimmer()
        {
            _shimmerX = -GetShimmerWidth();
        }

        private int GetShimmerWidth()
        {
            return Math.Max(160, Width / 4);
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            int shimmerWidth = GetShimmerWidth();
            _shimmerX += Math.Max(10, Width / 80);
            if (_shimmerX > Width + shimmerWidth)
                _shimmerX = -shimmerWidth;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(GetSkeletonPageBackground());

            SkeletonTemplate template = SkeletonTemplateFactory.Create(_skeletonType, ClientRectangle);
            foreach (SkeletonShape shape in template.Shapes)
                DrawShape(e.Graphics, shape);
        }

        private void DrawShape(Graphics g, SkeletonShape shape)
        {
            Rectangle bounds = shape.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            Color baseColor = GetSkeletonBaseColor();
            Color highlightColor = GetSkeletonHighlightColor();
            Color borderColor = GetSkeletonBorderColor();

            using GraphicsPath path = CreateShapePath(shape);
            using SolidBrush baseBrush = new SolidBrush(baseColor);
            g.FillPath(baseBrush, path);
            using Pen borderPen = new Pen(borderColor);
            g.DrawPath(borderPen, path);

            int shimmerWidth = GetShimmerWidth();
            Rectangle shimmer = new Rectangle(_shimmerX, bounds.Top - 10, shimmerWidth, bounds.Height + 20);
            if (!bounds.IntersectsWith(shimmer))
                return;

            using Region previousClip = g.Clip.Clone();
            g.SetClip(path, CombineMode.Intersect);
            using LinearGradientBrush shimmerBrush = new LinearGradientBrush(
                shimmer,
                Color.FromArgb(0, highlightColor),
                Color.FromArgb(170, highlightColor),
                LinearGradientMode.Horizontal);
            shimmerBrush.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f, 1f },
                Colors = new[]
                {
                    Color.FromArgb(0, highlightColor),
                    Color.FromArgb(170, highlightColor),
                    Color.FromArgb(0, highlightColor)
                }
            };
            g.FillRectangle(shimmerBrush, shimmer);
            g.Clip = previousClip;
        }

        private static Color GetSkeletonPageBackground()
        {
            return AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#111318")
                : ColorTranslator.FromHtml("#F9FAFB");
        }

        private static Color GetSkeletonBaseColor()
        {
            return AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#2A303B")
                : ColorTranslator.FromHtml("#E5E7EB");
        }

        private static Color GetSkeletonHighlightColor()
        {
            return AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#3A4352")
                : ColorTranslator.FromHtml("#F3F4F6");
        }

        private static Color GetSkeletonBorderColor()
        {
            return AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#374151")
                : ColorTranslator.FromHtml("#E5E7EB");
        }

        private static GraphicsPath CreateShapePath(SkeletonShape shape)
        {
            if (shape.ShapeType == SkeletonShapeType.Circle)
            {
                GraphicsPath circle = new GraphicsPath();
                circle.AddEllipse(shape.Bounds);
                return circle;
            }

            return CreateRoundedRect(shape.Bounds, shape.Radius);
        }

        private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);

            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
