using System.Text;

namespace FileSync.Shared.Protocol;

// Leest kopregels en headers regel-voor-regel van een socket-stream, en laat daarna
// exact overschakelen naar het lezen van de ruwe binaire body op dezelfde stream.
//
// Een gewone StreamReader kan hier niet gebruikt worden: die buffert vooruit en zou
// zonder waarschuwing al een stuk van de (binaire) body inlezen zodra hij op zoek gaat
// naar de volgende regel. Deze klasse houdt daarom zelf bij welke bytes al binnen zijn
// maar nog niet "verbruikt", zodat ReadBody daar eerst uit put voordat hij verder leest
// van de onderliggende stream.
public sealed class ProtocolStreamReader
{
    private const int MaxLineBytes = 4096;

    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[MaxLineBytes];
    private int _bufferStart;
    private int _bufferLength;

    public ProtocolStreamReader(Stream stream)
    {
        _stream = stream;
    }

    public string ReadLine()
    {
        var lineBytes = new List<byte>();

        while (true)
        {
            byte b = ReadByte();

            if (b == (byte)'\n')
            {
                if (lineBytes.Count == 0 || lineBytes[^1] != (byte)'\r')
                {
                    throw new MalformedMessageException("Regel eindigt op LF zonder voorafgaande CR.");
                }

                lineBytes.RemoveAt(lineBytes.Count - 1);
                return Encoding.UTF8.GetString(lineBytes.ToArray());
            }

            lineBytes.Add(b);

            // Grens is inclusief de CRLF zelf; een regel die alleen al zonder terminator
            // de limiet haalt is per definitie te lang.
            if (lineBytes.Count > MaxLineBytes)
            {
                throw new LineTooLongException($"Regel overschrijdt de maximale lengte van {MaxLineBytes} bytes.");
            }
        }
    }

    // Vult `destination` met precies `count` bytes: eerst met wat toevallig nog in de
    // interne buffer zat na de laatste ReadLine (het begin van de body kan daar al in
    // terecht zijn gekomen), daarna rechtstreeks van de onderliggende stream.
    public void ReadBody(byte[] destination, int offset, int count)
    {
        int totalRead = 0;

        while (totalRead < count && _bufferStart < _bufferLength)
        {
            destination[offset + totalRead] = _buffer[_bufferStart++];
            totalRead++;
        }

        while (totalRead < count)
        {
            int read = _stream.Read(destination, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Verbinding gesloten tijdens het lezen van de body.");
            }

            totalRead += read;
        }
    }

    private byte ReadByte()
    {
        if (_bufferStart >= _bufferLength)
        {
            Refill();
        }

        return _buffer[_bufferStart++];
    }

    private void Refill()
    {
        _bufferStart = 0;
        _bufferLength = _stream.Read(_buffer, 0, _buffer.Length);
        if (_bufferLength == 0)
        {
            throw new EndOfStreamException("Verbinding is gesloten door de andere partij.");
        }
    }
}
