using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Parsing;

public interface IChatParser
{
    Task<Chat> ParseAsync(IReadOnlyList<ChatPage> pages, CancellationToken cancellationToken);
}

public sealed record ChatPage(string FileName, Stream Content);

// The message is shown to the player, so it must never contain chat content.
public sealed class ChatParseException(string message) : Exception(message);
