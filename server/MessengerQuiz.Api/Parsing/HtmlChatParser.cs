using System.Globalization;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Parsing;

public sealed class HtmlChatParser : IChatParser
{
    // Facebook's auto-generated class names, kept together because a future export could rename them.
    private const string MessageBlockSelector = "._a6-g";
    private const string SenderSelector = "._a6-i";
    private const string ContentSelector = "._a6-p";
    private const string TimestampSelector = "._a72d";
    private const string ReactionSelector = "ul._a6-q > li";

    // Inside the content wrapper, the second div holds the text. Later divs hold media and reactions.
    private const int TextDivIndex = 1;

    // Older exports write "9:05:03pm" and newer ones "9:05:03 pm".
    private static readonly string[] TimestampFormats = ["MMM d, yyyy h:mm:sstt", "MMM d, yyyy h:mm:ss tt"];

    public async Task<Chat> ParseAsync(IReadOnlyList<ChatPage> pages, CancellationToken cancellationToken)
    {
        if (pages.Count == 0)
        {
            throw new ChatParseException("No chat files were uploaded.");
        }

        EnsureSingleChatFolder(pages);

        var parser = new HtmlParser();
        var titles = new HashSet<string>();
        var messages = new List<ParsedMessage>();

        foreach (var page in pages)
        {
            using var reader = new StreamReader(page.Content, Encoding.UTF8);
            var html = await reader.ReadToEndAsync(cancellationToken);
            using var document = await parser.ParseDocumentAsync(html, cancellationToken);

            if (document.QuerySelector(MessageBlockSelector) is null)
            {
                throw new ChatParseException("A file isn't a recognisable Messenger chat page.");
            }

            titles.Add(document.Title?.Trim() ?? "");
            // Pages list messages newest first. Reversing keeps same-second messages in sending order after sorting.
            messages.AddRange(document.QuerySelectorAll(MessageBlockSelector).Select(ParseBlock).OfType<ParsedMessage>().Reverse());
        }

        if (titles.Count > 1)
        {
            throw new ChatParseException("The files come from different chats. Upload one chat at a time.");
        }

        var ordered = messages
            .OrderBy(message => message.SentAt ?? DateTime.MinValue)
            .Select((message, index) => new Message(
                $"m_{index}", message.Sender, message.Text, message.SentAt, message.Reactions, message.HasMedia))
            .ToList();

        var participants = ordered
            .GroupBy(message => message.Sender)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .ToList();

        return new Chat(titles.Single(), participants, ordered);
    }

    private static void EnsureSingleChatFolder(IReadOnlyList<ChatPage> pages)
    {
        var fileNames = pages.Select(page => page.FileName.Replace('\\', '/')).ToList();

        if (fileNames.Distinct().Count() != fileNames.Count)
        {
            throw new ChatParseException("The same file was uploaded twice.");
        }

        var folders = fileNames.Select(fileName => fileName.Contains('/') ? fileName[..fileName.LastIndexOf('/')] : "");
        if (folders.Distinct().Count() > 1)
        {
            throw new ChatParseException("The files come from different chats. Upload one chat at a time.");
        }
    }

    private static ParsedMessage? ParseBlock(IElement block)
    {
        var sender = block.QuerySelector(SenderSelector);
        var timestamp = block.QuerySelector(TimestampSelector);

        // Header blocks have no sender, and group photo blocks have no timestamp. Neither is a message.
        if (sender is null || timestamp is null)
        {
            return null;
        }

        var textDiv = block.QuerySelector(ContentSelector)?.FirstElementChild?.Children.ElementAtOrDefault(TextDivIndex);
        var reactions = block.QuerySelectorAll(ReactionSelector).Select(ParseReaction).OfType<Reaction>().ToList();
        var hasMedia = block.QuerySelector("img, video, audio") is not null;

        return new ParsedMessage(
            sender.TextContent.Trim(),
            textDiv is null ? "" : ExtractText(textDiv),
            ParseTimestamp(timestamp.TextContent),
            reactions,
            hasMedia);
    }

    private static string ExtractText(IElement element)
    {
        var text = new StringBuilder();
        AppendText(element, text);
        return text.ToString().Trim();
    }

    private static void AppendText(INode node, StringBuilder text)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText textNode)
            {
                text.Append(textNode.Data);
            }
            else if (child is IElement { TagName: "BR" })
            {
                text.Append('\n');
            }
            else
            {
                AppendText(child, text);
            }
        }
    }

    private static Reaction? ParseReaction(IElement item)
    {
        var text = item.TextContent.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        // One emoji can be several code points, so split on the first full text element.
        var emojiLength = StringInfo.GetNextTextElementLength(text);
        return new Reaction(text[..emojiLength], text[emojiLength..].Trim());
    }

    private static DateTime? ParseTimestamp(string value)
    {
        return DateTime.TryParseExact(
            value.Trim(), TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private sealed record ParsedMessage(
        string Sender,
        string Text,
        DateTime? SentAt,
        IReadOnlyList<Reaction> Reactions,
        bool HasMedia);
}
