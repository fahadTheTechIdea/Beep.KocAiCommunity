using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Beep.KocAiCommunity.WinForms.Diagnostics;

/// <summary>
/// A rolling file log in the workspace.
/// <para>
/// The desktop had no durable log at all, which made "it crashed yesterday" unanswerable. This is
/// deliberately small — one file per day, a bounded number kept — rather than a logging framework: the
/// app needs somewhere to write, not a pipeline.
/// </para>
/// <para>
/// <b>What is never written:</b> the API token, the dev-persona override, and the contents of any
/// dataset. File names are fine; rows are not — an engineer's CSV may carry Restricted KOC data, and a
/// log is the one place it could leave the workspace unnoticed.
/// </para>
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly int _keepFiles;
    private readonly long _maxBytesPerFile;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Lock _writeGate = new();

    private string? _currentPath;
    private DateOnly _currentDay;

    public FileLoggerProvider(string directory, int keepFiles = 14, long maxBytesPerFile = 8L * 1024 * 1024)
    {
        _directory = directory;
        _keepFiles = keepFiles;
        _maxBytesPerFile = maxBytesPerFile;

        Directory.CreateDirectory(_directory);
        Prune();
    }

    /// <summary>The file currently being written, so Settings can offer to open it.</summary>
    public string CurrentFilePath => _currentPath ??= PathForToday();

    /// <summary>The minimum level written. Raised to Debug for a support session.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    /// <summary>Writes a line, rolling the file on a new day or an oversized one.</summary>
    internal void Write(string line)
    {
        lock (_writeGate)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_currentPath is null || today != _currentDay)
                {
                    _currentDay = today;
                    _currentPath = PathForToday();
                    Prune();
                }

                // A single runaway session must not fill the disk.
                if (File.Exists(_currentPath) && new FileInfo(_currentPath).Length > _maxBytesPerFile)
                {
                    _currentPath = Path.Combine(
                        _directory, $"studio-{_currentDay:yyyyMMdd}-{DateTime.Now:HHmmss}.log");
                }

                File.AppendAllText(_currentPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Logging must never be the reason the app fails.
            }
        }
    }

    private string PathForToday() =>
        Path.Combine(_directory, $"studio-{DateTime.Now:yyyyMMdd}.log");

    /// <summary>Keeps the newest <c>keepFiles</c> logs and deletes the rest.</summary>
    private void Prune()
    {
        try
        {
            var stale = new DirectoryInfo(_directory)
                .GetFiles("studio-*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(_keepFiles);

            foreach (var file in stale)
            {
                file.Delete();
            }
        }
        catch (Exception)
        {
            // A locked log file is not worth failing a launch over.
        }
    }
}

internal sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= provider.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var text = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(Short(logLevel)).Append("] ")
            .Append(category.Split('.')[^1])
            .Append(": ")
            .Append(formatter(state, exception));

        if (exception is not null)
        {
            text.AppendLine().Append(exception);
        }

        provider.Write(text.ToString());
    }

    private static string Short(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };
}
