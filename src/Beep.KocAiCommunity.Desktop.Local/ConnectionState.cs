namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>What the app currently believes about the KOC network.</summary>
public enum Connectivity
{
    /// <summary>Nothing has been checked yet.</summary>
    Unknown,

    Online,

    Offline,
}

/// <summary>
/// One shared answer to "am I connected", so every page agrees.
/// <para>
/// It used to be discovered per page, by a call failing. That gives three pages three opinions and
/// tells a person only after they have tried to do something.
/// </para>
/// </summary>
public sealed class ConnectionState
{
    private Connectivity _status = Connectivity.Unknown;
    private int _queued;

    /// <summary>Raised when the status or the queue depth changes. Fires off the UI thread.</summary>
    public event Action? Changed;

    public Connectivity Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            Changed?.Invoke();
        }
    }

    /// <summary>How many submissions are waiting. Shown next to the status, because it is the reason to care.</summary>
    public int Queued
    {
        get => _queued;
        set
        {
            if (_queued == value)
            {
                return;
            }

            _queued = value;
            Changed?.Invoke();
        }
    }

    /// <summary>True while there is queued work and a network to send it on.</summary>
    public bool IsSyncing => Status == Connectivity.Online && Queued > 0;

    /// <summary>When the connectivity check last ran, so the indicator can say how fresh it is.</summary>
    public DateTime? LastCheckedUtc { get; set; }
}
