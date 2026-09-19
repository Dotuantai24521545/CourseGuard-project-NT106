using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public static class SearchFocusManager
    {
        private const int WmLButtonDown = 0x0201;
        private const int WmRButtonDown = 0x0204;
        private const string SearchMarker = "__CourseGuardSearchInput";
        private static readonly Dictionary<Form, SearchClickFilter> Filters = new();

        public static void Install(Form form)
        {
            if (Filters.ContainsKey(form))
                return;

            var filter = new SearchClickFilter(form);
            Filters[form] = filter;
            Application.AddMessageFilter(filter);
            form.FormClosed += (_, _) => Uninstall(form);
        }

        public static void Uninstall(Form form)
        {
            if (!Filters.TryGetValue(form, out var filter))
                return;

            Application.RemoveMessageFilter(filter);
            Filters.Remove(form);
        }

        public static void MarkSearchInput(TextBox input)
        {
            input.Tag = SearchMarker;
            input.TabStop = false;
        }

        public static bool BlurFocusedSearchInput(Form? form)
        {
            if (form == null || form.IsDisposed)
                return false;

            Control? focused = GetDeepActiveControl(form);
            if (!IsSearchInput(focused))
                return false;

            ClearActiveControlChain(form, focused!);
            return true;
        }

        private static bool IsSearchInput(Control? control)
        {
            if (control is not TextBox textBox)
                return false;

            if (Equals(textBox.Tag, SearchMarker))
                return true;

            string name = textBox.Name ?? string.Empty;
            if (name.Contains("search", StringComparison.OrdinalIgnoreCase))
                return true;

            string placeholder = textBox.PlaceholderText ?? string.Empty;
            return placeholder.Contains("Tìm", StringComparison.OrdinalIgnoreCase)
                || placeholder.Contains("Tim", StringComparison.OrdinalIgnoreCase)
                || placeholder.Contains("search", StringComparison.OrdinalIgnoreCase);
        }

        private static Control? GetDeepActiveControl(ContainerControl container)
        {
            Control? active = container.ActiveControl;
            while (active is ContainerControl nested && nested.ActiveControl != null)
                active = nested.ActiveControl;

            return active;
        }

        private static void ClearActiveControlChain(Form form, Control focused)
        {
            Control? current = focused.Parent;
            while (current != null)
            {
                if (current is ContainerControl container)
                    container.ActiveControl = null;

                current = current.Parent;
            }

            form.ActiveControl = null;
        }

        private static bool IsSelfOrDescendant(Control root, Control? candidate)
        {
            Control? current = candidate;
            while (current != null)
            {
                if (current == root)
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private sealed class SearchClickFilter : IMessageFilter
        {
            private readonly Form _form;

            public SearchClickFilter(Form form)
            {
                _form = form;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WmLButtonDown && m.Msg != WmRButtonDown)
                    return false;

                if (_form.IsDisposed || !_form.Visible)
                    return false;

                Control? focused = GetDeepActiveControl(_form);
                if (!IsSearchInput(focused))
                    return false;

                Control? target = Control.FromHandle(m.HWnd);
                if (IsSelfOrDescendant(focused!, target))
                    return false;

                ClearActiveControlChain(_form, focused!);
                return false;
            }
        }
    }
}
