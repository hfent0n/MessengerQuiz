using System.IO.Compression;

namespace MessengerQuiz.Api.Uploads;

// The client gzips uploads to keep them small. Plain uploads are still accepted. The size limit applies after
// decompression, so a small upload can't expand into a huge one.
public static class GzipReader
{
    private const int BufferSize = 81920;

    public static async Task<MemoryStream> ReadAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        var raw = new MemoryStream();
        await body.CopyToAsync(raw, cancellationToken);
        raw.Position = 0;

        if (!IsGzip(raw))
        {
            if (raw.Length > maxBytes)
            {
                throw new ChatTooLargeException();
            }

            return raw;
        }

        var decompressed = new MemoryStream();
        await using (var gzip = new GZipStream(raw, CompressionMode.Decompress))
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = await gzip.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (decompressed.Length + read > maxBytes)
                {
                    throw new ChatTooLargeException();
                }

                decompressed.Write(buffer, 0, read);
            }
        }

        decompressed.Position = 0;
        return decompressed;
    }

    // Gzip data starts with these two bytes. JSON never does.
    private static bool IsGzip(MemoryStream stream)
    {
        var bytes = stream.GetBuffer();
        return stream.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
    }
}

public sealed class ChatTooLargeException() : Exception("The chat is too large once decompressed.");
