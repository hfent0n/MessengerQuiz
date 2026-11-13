using System.Net.Http.Headers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Uploads;

namespace MessengerQuiz.Tests;

// Chats as the server sees them: numbers, flags and sealed boxes. The server can't open real sealed boxes, so
// these fakes hold a readable label instead, which lets a test tell which message came back.
internal static class TestChats
{
    public static readonly DateTime Start = new(2023, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public static string Sealed(string label) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"sealed:{label}|".PadRight(32, '.')))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Label(string sealedBox)
    {
        var base64 = sealedBox.Replace('-', '+').Replace('_', '/');
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '=')));
        return text["sealed:".Length..text.IndexOf('|')];
    }

    public static Message Message(
        int sender = 0,
        int reactions = 0,
        int? minute = null,
        bool quizzable = true,
        bool system = false,
        int media = 0,
        string? label = null) =>
        new(0, sender, minute is { } m ? Start.AddMinutes(m) : null, reactions, media, quizzable, system, Sealed(label ?? "message"));

    // Numbers the messages by their place in the list, as an upload does.
    public static Chat Chat(int people, params IEnumerable<Message> messages) =>
        new(Sealed("title"), Sealed("people"), people, messages.Select((message, position) => message with { Position = position }).ToList());

    // What the browser uploads.
    public static UploadedMessage Uploaded(
        int sender, string label, int reactions = 1, int? minute = null, bool quizzable = true, bool system = false, int media = 0) =>
        new(sender, minute is { } m ? new DateTimeOffset(Start.AddMinutes(m)).ToUnixTimeSeconds() : null, reactions, media,
            quizzable, system, Sealed(label));

    public static ChatUpload Upload(int people, params IEnumerable<UploadedMessage> messages) =>
        new(Sealed("title"), Sealed("people"), people, messages.ToList());

    // Senders take turns, as in a real chat, so no message has same-sender context.
    public static ChatUpload QuizzableUpload(int people = 3, int messagesPerPerson = 6) =>
        Upload(people, Enumerable.Range(0, messagesPerPerson).SelectMany(index =>
            Enumerable.Range(0, people).Select(sender => Uploaded(sender, $"p{sender}-{index}"))));

    public static ByteArrayContent Content(ChatUpload upload, bool gzip = true)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(upload, JsonSerializerOptions.Web);
        if (gzip)
        {
            using var compressed = new MemoryStream();
            using (var stream = new GZipStream(compressed, CompressionLevel.Fastest))
            {
                stream.Write(json);
            }

            json = compressed.ToArray();
        }

        var content = new ByteArrayContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }
}
