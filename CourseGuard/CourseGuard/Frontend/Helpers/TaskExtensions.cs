using CourseGuard.Frontend.Extensions;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Helpers
{
    public static class TaskExtensions
    {
        internal static Action<Control?, string, string, Exception>? ErrorReporter { get; set; }
        internal static Action<Exception?>? ObservationCompleted { get; set; }

        public static void FireAndForgetSafe(this Task task, Control? owner = null, string errorTitle = "Loi nen")
        {
            if (task == null)
            {
                return;
            }

            SynchronizationContext? context = SynchronizationContext.Current;
            var work = new ObservationWork(task, owner, errorTitle, context);
            ThreadPool.QueueUserWorkItem(static state =>
            {
                var item = (ObservationWork)state!;
                Task ignored = ObserveAsync(item.Task, item.Owner, item.ErrorTitle, item.Context);
                _ = ignored;
            }, work);
        }

        private static async Task ObserveAsync(Task task, Control? owner, string errorTitle, SynchronizationContext? context)
        {
            Exception? observedException = null;

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is an observed completion, not a background failure.
            }
            catch (Exception ex)
            {
                observedException = ex;
                try
                {
                    ReportException(owner, errorTitle, ex, context);
                }
                catch
                {
                    // The original task exception has been observed; never let reporting crash the app.
                }
            }
            finally
            {
                NotifyObservationCompleted(observedException);
            }
        }

        private static void ReportException(Control? owner, string errorTitle, Exception exception, SynchronizationContext? context)
        {
            string message = $"A background task failed: {exception.Message}";

            if (IsValidOwner(owner))
            {
                owner!.InvokeIfRequired(() => SafeShowDialog(owner, errorTitle, message, exception));
                return;
            }

            if (context != null)
            {
                context.Post(_ => SafeShowDialog(null, errorTitle, message, exception), null);
            }
        }

        private static bool IsValidOwner(Control? owner)
        {
            return owner != null && !owner.IsDisposed && owner.IsHandleCreated;
        }

        private static void NotifyObservationCompleted(Exception? exception)
        {
            try
            {
                ObservationCompleted?.Invoke(exception);
            }
            catch
            {
                // Test hooks must not affect production behavior.
            }
        }

        private static void SafeShowDialog(Control? owner, string errorTitle, string message, Exception exception)
        {
            try
            {
                ShowDialog(owner, errorTitle, message, exception);
            }
            catch
            {
                // The original task exception has been observed; reporting must never crash the app.
            }
        }

        private static void ShowDialog(Control? owner, string errorTitle, string message, Exception exception)
        {
            Action<Control?, string, string, Exception>? reporter = ErrorReporter;
            if (reporter != null)
            {
                reporter(owner, errorTitle, message, exception);
                return;
            }

            if (IsValidOwner(owner))
            {
                MetaTheme.ShowModernDialog(owner, message, errorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MetaTheme.ShowModernDialog(message, errorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private sealed class ObservationWork
        {
            public ObservationWork(Task task, Control? owner, string errorTitle, SynchronizationContext? context)
            {
                Task = task;
                Owner = owner;
                ErrorTitle = errorTitle;
                Context = context;
            }

            public Task Task { get; }
            public Control? Owner { get; }
            public string ErrorTitle { get; }
            public SynchronizationContext? Context { get; }
        }
    }
}
