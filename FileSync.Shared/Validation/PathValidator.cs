using System.Text;

namespace FileSync.Shared.Validation;

// Zuivere functie zonder I/O: valideert en canoniseert een pad volgens PROTOCOL.md §2.
// Wordt door zowel de server (elk commando met een pad) als de client (vóór elke
// verzending) gebruikt, zodat de padregels op precies één plek gedefinieerd staan.
public static class PathValidator
{
    private const int MaxSegmentBytes = 240;
    private const int MaxPathBytes = 2048;

    private static readonly char[] ForbiddenChars = { '\\', ':', '*', '?', '"', '<', '>', '|' };

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool TryValidate(string? raw, out string canonical, out PathValidationError? error)
    {
        canonical = string.Empty;
        error = null;

        if (string.IsNullOrEmpty(raw))
        {
            error = PathValidationError.Empty;
            return false;
        }

        // NFC: verschillende Unicode-representaties van hetzelfde teken (bv. een letter
        // met los toegevoegd accent versus het samengestelde teken) moeten als hetzelfde
        // pad gelden, ongeacht op welk besturingssysteem de client draait.
        string normalized = raw.Normalize(NormalizationForm.FormC);

        if (normalized.StartsWith('/'))
        {
            error = PathValidationError.LeadingSlash;
            return false;
        }

        foreach (string segment in normalized.Split('/'))
        {
            if (!TryValidateSegment(segment, out error))
            {
                return false;
            }
        }

        if (Encoding.UTF8.GetByteCount(normalized) > MaxPathBytes)
        {
            error = PathValidationError.PathTooLong;
            return false;
        }

        canonical = normalized;
        return true;
    }

    private static bool TryValidateSegment(string segment, out PathValidationError? error)
    {
        error = null;

        if (segment is "." or "..")
        {
            error = PathValidationError.TraversalSegment;
            return false;
        }

        if (segment.Length == 0)
        {
            // Lege segmenten ontstaan door "//" of een pad dat op "/" eindigt.
            error = PathValidationError.ForbiddenChar;
            return false;
        }

        foreach (char c in segment)
        {
            if (c < 0x20)
            {
                error = PathValidationError.ControlChar;
                return false;
            }

            if (Array.IndexOf(ForbiddenChars, c) >= 0)
            {
                error = PathValidationError.ForbiddenChar;
                return false;
            }
        }

        if (segment[^1] is ' ' or '.')
        {
            error = PathValidationError.TrailingSpaceOrDot;
            return false;
        }

        // "CON.txt" is op Windows evengoed het gereserveerde apparaat CON, niet alleen
        // de kale naam "CON" zelf.
        string nameBeforeExtension = segment.Split('.')[0];
        if (ReservedNames.Contains(nameBeforeExtension))
        {
            error = PathValidationError.ReservedName;
            return false;
        }

        if (Encoding.UTF8.GetByteCount(segment) > MaxSegmentBytes)
        {
            error = PathValidationError.SegmentTooLong;
            return false;
        }

        return true;
    }
}
