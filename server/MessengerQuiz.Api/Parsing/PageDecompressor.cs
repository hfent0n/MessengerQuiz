using System.IO.Compression;

namespace MessengerQuiz.Api.Parsing;

// The client gzips pages to keep uploads small. Plain pages are still accepted.
// Every page counts towards one limit, so a small upload can't expand into a huge one.
public sealed class PageDecompressor(long maxBytes)
{
    private const int BufferSize = 81920;

    private long _remaining = maxBytes;

    public async Task<Stream> OpenAsync(Stream upload, CancellationToken cancellationToken)
    {
        if (!await IsGzipAsync(upload, cancellationToken))
        {
            Spend(upload.Length);
            return upload;
        }

        var decompressed = new MemoryStream();
        await using (var gzip = new GZipStream(upload, CompressionMode.Decompress, leaveOpen: true))
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = await gzip.ReadAsync(buffer, cancellationToken)) > 0)
            {
                Spend(read);
                decompressed.Write(buffer, 0, read);
            }
        }

        decompressed.Position = 0;
        return decompressed;
    }

    // Gzip data starts with these two bytes. HTML never does.
    private static async Task<bool> IsGzipAsync(Stream upload, CancellationToken cancellationToken)
    {
        var header = new byte[2];
        var read = await upload.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        upload.Position = 0;
        return read == 2 && header[0] == 0x1f && header[1] == 0x8b;
    }

    private void Spend(long bytes)
    {
        _remaining -= bytes;
        if (_remaining < 0)
        {
            throw new ChatTooLargeException();
        }
    }
}

public sealed class ChatTooLargeException() : Exception("The chat pages are too large once decompressed.");
