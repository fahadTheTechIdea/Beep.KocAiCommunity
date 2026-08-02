using System.Text;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// What a CSV file appears to be. A guess, and presented as one.
/// </summary>
/// <param name="Encoding">How to decode the bytes.</param>
/// <param name="Delimiter">What separates fields.</param>
/// <param name="Confident">
/// False when the delimiter could not be told apart — a single-column file, or one where nothing wins.
/// The UI asks rather than assumes when this is false.
/// </param>
public sealed record CsvFormat(Encoding Encoding, char Delimiter, bool Confident)
{
    /// <summary>The delimiters offered as an override, in the order they are worth trying.</summary>
    public static readonly char[] Candidates = [',', ';', '\t', '|'];

    /// <summary>A name for the delimiter that reads as words rather than punctuation.</summary>
    public static string DelimiterName(char delimiter) => delimiter switch
    {
        ',' => "comma",
        ';' => "semicolon",
        '\t' => "tab",
        '|' => "pipe",
        _ => delimiter.ToString(),
    };
}

/// <summary>
/// Works out a CSV's encoding and delimiter from its first pages.
/// <para>
/// The import path used to assume UTF-8 and commas. KOC data is often neither: Excel in an Arabic
/// locale exports <b>semicolon</b>-separated, and older systems produce <b>Windows-1256</b>. Both used
/// to import to something that looked fine in the list and then failed strangely in the designer — one
/// column named after the entire header row, or column names in mojibake. This is the most likely
/// first-contact failure for a KOC engineer, and it was silent.
/// </para>
/// </summary>
public static class CsvFormatDetector
{
    /// <summary>How much of the file is read to decide. Enough for a header and a few hundred rows.</summary>
    public const int SampleBytes = 64 * 1024;

    private const int SampleLines = 20;

    private static readonly object ProviderGate = new();
    private static bool _providerRegistered;

    /// <summary>
    /// The system's ANSI code page — 1256 on an Arabic-locale Windows, 1252 on a Western one.
    /// <para>
    /// .NET Core dropped the legacy code pages from the default set, so this has to be registered
    /// before <see cref="Encoding.GetEncoding(int)"/> will return one. Falls back to UTF-8 where the
    /// page genuinely is not available rather than throwing during an import.
    /// </para>
    /// </summary>
    public static Encoding SystemAnsi()
    {
        lock (ProviderGate)
        {
            if (!_providerRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _providerRegistered = true;
            }
        }

        try
        {
            return Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>Arabic Windows, offered explicitly because guessing it from bytes alone is unreliable.</summary>
    public static Encoding Arabic()
    {
        SystemAnsi();
        try
        {
            return Encoding.GetEncoding(1256);
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>Reads the head of a seekable stream and rewinds it.</summary>
    public static async Task<CsvFormat> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[SampleBytes];
        var read = await stream.ReadAsync(buffer.AsMemory(0, SampleBytes), ct);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Detect(buffer.AsSpan(0, read));
    }

    public static CsvFormat Detect(ReadOnlySpan<byte> sample)
    {
        var encoding = DetectEncoding(sample);
        var text = Decode(encoding, sample);
        var (delimiter, confident) = DetectDelimiter(text);
        return new CsvFormat(encoding, delimiter, confident);
    }

    private static Encoding DetectEncoding(ReadOnlySpan<byte> sample)
    {
        // A BOM is the only encoding statement a file can actually make. Take it at its word.
        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        // No BOM: valid UTF-8 is overwhelmingly likely to *be* UTF-8, because the multi-byte sequences
        // are too structured to arise by accident in legacy text. If it does not decode, it is not.
        return IsValidUtf8(sample) ? new UTF8Encoding(false) : LegacyEncodingFor(sample);
    }

    /// <summary>
    /// Which legacy code page these bytes are most likely to be.
    /// <para>
    /// Falling back to the machine's own ANSI page is the obvious move and it is wrong here: a KOC
    /// laptop set to English has 1252, so a genuine Windows-1256 export from an Arabic system decodes
    /// to <c>ÇáÈÆÑ</c> — mojibake column names, on the exact file this whole detector exists for.
    /// </para>
    /// <para>
    /// So the bytes get a vote. Arabic text is <em>mostly</em> non-ASCII, and under 1256 those bytes
    /// land in the Unicode Arabic block; Western text in 1252 is mostly ASCII with the odd accent. The
    /// two are not perfectly separable — a 1252 file heavy with accented Latin would read as Arabic
    /// here — which is one more reason the guess is shown to the user before anything is kept.
    /// </para>
    /// </summary>
    private static Encoding LegacyEncodingFor(ReadOnlySpan<byte> sample)
    {
        var arabic = Arabic();
        string decoded;
        try
        {
            decoded = arabic.GetString(sample);
        }
        catch (Exception)
        {
            return SystemAnsi();
        }

        if (decoded.Length == 0)
        {
            return SystemAnsi();
        }

        var nonAscii = 0;
        var arabicLetters = 0;
        foreach (var ch in decoded)
        {
            if (ch <= 127)
            {
                continue;
            }

            nonAscii++;
            if (ch is >= '؀' and <= 'ۿ')
            {
                arabicLetters++;
            }
        }

        var mostlyNonAscii = nonAscii >= decoded.Length * 0.15;
        var almostAllArabic = nonAscii > 0 && arabicLetters >= nonAscii * 0.8;

        return mostlyNonAscii && almostAllArabic ? arabic : SystemAnsi();
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> sample)
    {
        try
        {
            // The sample almost certainly cuts a character in half at the end. Stopping a few bytes
            // short avoids reporting the whole file as non-UTF-8 because of where the buffer ended.
            var length = Math.Max(0, sample.Length - 4);
            new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(sample[..length]);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string Decode(Encoding encoding, ReadOnlySpan<byte> sample)
    {
        try
        {
            return encoding.GetString(sample);
        }
        catch (Exception)
        {
            return Encoding.UTF8.GetString(sample);
        }
    }

    /// <summary>
    /// The delimiter whose count is most consistent across lines.
    /// <para>
    /// Frequency alone picks the wrong one on prose: a notes column full of commas beats the semicolons
    /// that actually separate the fields. Consistency is the real signal — a delimiter appears the same
    /// number of times on every line, because every line has the same number of columns.
    /// </para>
    /// </summary>
    private static (char Delimiter, bool Confident) DetectDelimiter(string text)
    {
        var lines = text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .Take(SampleLines)
            .ToList();

        // The last line of the sample is probably truncated mid-row, so it would look inconsistent.
        if (lines.Count > 2)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return (',', false);
        }

        var best = ',';
        var bestScore = 0;

        foreach (var candidate in CsvFormat.Candidates)
        {
            var counts = lines.Select(l => CountOutsideQuotes(l, candidate)).ToList();
            if (counts[0] == 0 || counts.Distinct().Count() != 1)
            {
                continue; // absent from the header, or the count varies — not the delimiter
            }

            // Among consistent candidates the one splitting into most columns wins: a semicolon file
            // whose text happens to hold one comma per line would otherwise be a coin toss.
            if (counts[0] > bestScore)
            {
                best = candidate;
                bestScore = counts[0];
            }
        }

        return bestScore > 0 ? (best, true) : (',', false);
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var quoted = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (ch == delimiter && !quoted)
            {
                count++;
            }
        }

        return count;
    }
}
