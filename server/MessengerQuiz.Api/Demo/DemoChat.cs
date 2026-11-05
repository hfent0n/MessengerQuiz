using System.Text.Json;
using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Demo;

// A made-up chat, so people without a Facebook export can try the game. Its photos are served by the client.
public sealed class DemoChat
{
    private const string ResourceName = "MessengerQuiz.Api.Demo.demo-chat.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Chat Chat { get; } = Load();

    private static Chat Load()
    {
        using var stream = typeof(DemoChat).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The {ResourceName} resource is missing.");
        var file = JsonSerializer.Deserialize<DemoFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The demo chat is empty.");

        var messages = new List<Message>();
        DateTime? sentAt = null;
        foreach (var (entry, index) in file.Messages.Select((entry, index) => (entry, index)))
        {
            // Each message is timed from the one before, unless it starts a new conversation.
            sentAt = entry.At ?? sentAt?.AddMinutes(entry.After)
                ?? throw new InvalidOperationException("The first demo message needs a time.");

            var reactions = entry.Reactions.Select(reaction =>
            {
                var space = reaction.IndexOf(' ');
                return new Reaction(reaction[..space], reaction[(space + 1)..]);
            }).ToList();

            messages.Add(new Message($"m_{index}", entry.Sender, entry.Text, sentAt, reactions, entry.Media.Count > 0)
            {
                Media = entry.Media,
            });
        }

        var participants = messages
            .GroupBy(message => message.Sender)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .ToList();

        return new Chat(file.Title, participants, messages);
    }

    private sealed record DemoFile(string Title, IReadOnlyList<DemoMessage> Messages);

    private sealed record DemoMessage(
        string Sender,
        DateTime? At,
        double After,
        string Text = "",
        IReadOnlyList<string>? Reactions = null,
        IReadOnlyList<string>? Media = null)
    {
        public IReadOnlyList<string> Reactions { get; init; } = Reactions ?? [];
        public IReadOnlyList<string> Media { get; init; } = Media ?? [];
    }
}
