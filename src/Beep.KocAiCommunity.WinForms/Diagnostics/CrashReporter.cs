using System.Text;

namespace Beep.KocAiCommunity.WinForms.Diagnostics;

/// <summary>
/// The last line of defence. Before this existed, an unhandled exception closed the window with no
/// message and no log — the user saw the app vanish and had nothing to report.
/// <para>
/// Everything here is deliberately primitive: no dependency injection, no logging framework, no Blazor.
/// This code runs when something has already gone wrong, and a crash handler that itself throws leaves
/// the user exactly where they started.
/// </para>
/// </summary>
public static class CrashReporter
{
    private static string? _logDirectory;
    private static bool _reporting;

    /// <summary>
    /// Installs the handlers. Must run before anything else in <c>Main</c> — an exception thrown while
    /// composing the app is precisely the one nobody can currently diagnose.
    /// </summary>
    public static void Install(string logDirectory)
    {
        _logDirectory = logDirectory;

        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => Report(e.Exception, terminating: false);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report(e.ExceptionObject as Exception, terminating: e.IsTerminating);

        // An unobserved task exception is not fatal, but it is worth a line in the log — it is usually
        // the first sign of a swallowed failure somewhere.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashFile(e.Exception, "unobserved-task");
            e.SetObserved();
        };
    }

    /// <summary>Writes the failure to disk and tells the user where it went.</summary>
    public static void Report(Exception? exception, bool terminating)
    {
        if (exception is null)
        {
            return;
        }

        // A failure raised while reporting a failure would loop. Report the first one only.
        if (_reporting)
        {
            return;
        }

        _reporting = true;
        try
        {
            var path = WriteCrashFile(exception, "crash");
            Show(exception, path, terminating);
        }
        catch (Exception)
        {
            // Absolute last resort — no file, no formatting, just tell the user something broke.
            TryPlainMessage(exception);
        }
        finally
        {
            _reporting = false;
        }
    }

    private static string? WriteCrashFile(Exception exception, string kind)
    {
        if (_logDirectory is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            var text = new StringBuilder()
                .AppendLine($"KOC Studio {Version}")
                .AppendLine($"When:    {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}")
                .AppendLine($"OS:      {Environment.OSVersion}")
                .AppendLine($"64-bit:  {Environment.Is64BitProcess}")
                .AppendLine($"CLR:     {Environment.Version}")
                .AppendLine()
                .AppendLine(exception.ToString());

            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Show(Exception exception, string? crashFile, bool terminating)
    {
        var body = new StringBuilder()
            .AppendLine(terminating
                ? "KOC Studio has hit a problem it can't recover from and needs to close."
                : "KOC Studio has hit a problem. You can usually carry on, but save your work.")
            .AppendLine()
            .AppendLine(exception.Message);

        if (crashFile is not null)
        {
            body.AppendLine().AppendLine($"Details were written to:{Environment.NewLine}{crashFile}");
        }

        body.AppendLine().AppendLine("Choose Yes to copy the full details to the clipboard.");

        var choice = MessageBox.Show(
            body.ToString(),
            terminating ? "KOC Studio — unrecoverable error" : "KOC Studio — error",
            MessageBoxButtons.YesNo,
            terminating ? MessageBoxIcon.Error : MessageBoxIcon.Warning);

        if (choice == DialogResult.Yes)
        {
            TryCopy(exception.ToString());
        }
    }

    private static void TryCopy(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception)
        {
            // The clipboard can be locked by another process. The crash file is still on disk.
        }
    }

    private static void TryPlainMessage(Exception exception)
    {
        try
        {
            MessageBox.Show(exception.Message, "KOC Studio — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception)
        {
            // Nothing further is possible.
        }
    }

    /// <summary>The running version, for the crash header and the log banner.</summary>
    public static string Version =>
        typeof(CrashReporter).Assembly.GetName().Version?.ToString() ?? "unknown";
}
