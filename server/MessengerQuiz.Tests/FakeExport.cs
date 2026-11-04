using System.Net;
using System.Text;

namespace MessengerQuiz.Tests;

// Hand-written copies of the export's markup. Real messages must never be used as fixtures.
internal static class FakeExport
{
    public const string DefaultTimestamp = "Dec 12, 2023 9:05:03pm";

    public static string Page(string title, params string[] blocks)
    {
        return $$"""
            <html><head><meta charset="utf-8" /><title>{{WebUtility.HtmlEncode(title)}}</title></head>
            <body class="_5vb_ _2yq _a7o5"><div class="_a706" role="main">
            <div class="_3-95 _a6-g"><div class="_2ph_ _a6-h">Participants: Ana, Ben and Cal</div></div>
            <div class="_3-95 _a6-g"><div class="_2ph_ _a6-h">Group invitation link: not available</div></div>
            {{string.Concat(blocks)}}
            </div></body></html>
            """;
    }

    public static string Message(
        string sender,
        string text,
        string timestamp = DefaultTimestamp,
        params string[] reactions)
    {
        var encoded = WebUtility.HtmlEncode(text).Replace("\n", "<br />");
        return Block(sender, $"<div></div><div>{encoded}</div><div></div><div></div>{Reactions(reactions)}", timestamp);
    }

    public const string ChatFolder = "your_facebook_activity/messages/inbox/chat_123";

    public static string Photo(string sender, params string[] reactions) =>
        Media(sender, $"{ChatFolder}/photos/p.jpg", DefaultTimestamp, reactions);

    public static string PhotoAt(string sender, string timestamp, string fileName = "p.jpg", string[]? reactions = null) =>
        Media(sender, $"{ChatFolder}/photos/{fileName}", timestamp, reactions ?? []);

    public static string Gif(string sender, string fileName = "g.gif") =>
        Media(sender, $"{ChatFolder}/gifs/{fileName}", DefaultTimestamp);

    public static string Video(string sender, string fileName = "v.mp4")
    {
        var video = $"""<div><div><video src="{ChatFolder}/videos/{fileName}" controls="1" class="_a6_o _3-96"><a href="{ChatFolder}/videos/{fileName}">Click for video</a></video><div></div></div></div>""";
        return Block(sender, $"<div></div><div></div><div></div><div></div>{video}", DefaultTimestamp);
    }

    // Any image the export can reference, such as a GIF or sticker.
    public static string Media(string sender, string source, string timestamp, params string[] reactions)
    {
        var image = $"""<div><div><a target="_blank" href="{source}"><img src="{source}" class="_a6_o _3-96" /></a><div></div></div></div>""";
        return Block(sender, $"<div></div><div></div><div></div><div></div>{image}{Reactions(reactions)}", timestamp);
    }

    // The export's group photo entries have an image and no timestamp.
    public static string GroupPhoto(string sender)
    {
        return $"""
            <div class="_3-95 _a6-g"><div class="_2ph_ _a6-h _a6-i">{sender}</div>
            <div class="_2ph_ _a6-p"><div><div></div><a href="g.jpg"><img src="g.jpg" class="_a6_o _3-96" /></a></div></div></div>
            """;
    }

    public static string QuizzablePage(string title = "Test chat", int messagesPerSender = 6)
    {
        // Senders take turns, as in a real chat, so no message has same-sender context.
        var messages = Enumerable.Range(0, messagesPerSender).SelectMany(index => new[] { "Ana", "Ben", "Cal" }
            .Select(sender => Message(sender, $"{sender} wrote a long enough message, number {index}.", reactions: "😆Ana")));

        return Page(title, messages.ToArray());
    }

    public static MemoryStream Stream(string html) => new(Encoding.UTF8.GetBytes(html));

    private static string Block(string sender, string content, string timestamp)
    {
        return $"""
            <div class="_3-95 _a6-g"><div class="_2ph_ _a6-h _a6-i">{WebUtility.HtmlEncode(sender)}</div>
            <div class="_2ph_ _a6-p"><div>{content}</div></div>
            <div class="_3-94 _a6-o"><div class="_a72d">{timestamp}</div></div></div>
            """;
    }

    private static string Reactions(string[] reactions)
    {
        if (reactions.Length == 0)
        {
            return "";
        }

        var items = string.Concat(reactions.Select(reaction => $"<li>{WebUtility.HtmlEncode(reaction)}</li>"));
        return $"""<div><ul class="_a6-q">{items}</ul></div>""";
    }
}
