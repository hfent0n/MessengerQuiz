using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Uploads;

// What the browser uploads. The server can't read the sealed parts, so it checks everything else instead.
public sealed record ChatUpload(
    string? Title,
    string? People,
    int PersonCount,
    IReadOnlyList<UploadedMessage>? Messages,
    // People the browser recognised as automated accounts, such as Meta AI. Optional.
    IReadOnlyList<int>? Automated = null);

public sealed record UploadedMessage(
    int Sender,
    // Seconds since 1970.
    long? SentAt,
    int Reactions,
    int Media,
    bool Quizzable,
    bool System,
    string? Sealed);

public static partial class ChatUploads
{
    // Below this, it can't be a sealed box: a 12-byte IV and a 16-byte tag, in base64url.
    private const int MinSealedLength = 38;
    private const int MaxReactions = 10_000;
    private const int MaxMedia = 1_000;

    // 1 January 2000 to 1 January 2100. Messenger didn't exist before, and later is a mistake.
    private const long EarliestTime = 946_684_800;
    private const long LatestTime = 4_102_444_800;

    // Returns the chat, or null with a reason that's safe to show the player.
    public static bool TryRead(ChatUpload? upload, QuizOptions limits, [NotNullWhen(true)] out Chat? chat, out string error)
    {
        chat = null;
        error = "The chat couldn't be read.";

        if (upload?.Messages is not { } messages
            || !IsSealed(upload.Title, limits.MaxSealedLength)
            || !IsSealed(upload.People, limits.MaxSealedLength)
            || upload.PersonCount < 1 || upload.PersonCount > limits.MaxPeople)
        {
            return false;
        }

        if (messages.Count == 0 || messages.Count > limits.MaxMessages)
        {
            error = messages.Count == 0 ? "The chat has no messages." : "This chat has too many messages.";
            return false;
        }

        var accepted = new List<Message>(messages.Count);
        foreach (var message in messages)
        {
            if (message is null
                || message.Sender < 0 || message.Sender >= upload.PersonCount
                || message.Reactions is < 0 or > MaxReactions
                || message.Media is < 0 or > MaxMedia
                || message.SentAt is < EarliestTime or > LatestTime
                || !IsSealed(message.Sealed, limits.MaxSealedLength))
            {
                return false;
            }

            accepted.Add(new Message(
                accepted.Count,
                message.Sender,
                message.SentAt is { } seconds ? DateTime.UnixEpoch.AddSeconds(seconds) : null,
                message.Reactions,
                message.Media,
                message.Quizzable,
                message.System,
                message.Sealed!));
        }

        var automated = upload.Automated ?? [];
        if (automated.Any(person => person < 0 || person >= upload.PersonCount))
        {
            return false;
        }

        chat = new Chat(upload.Title!, upload.People!, upload.PersonCount, accepted) { Automated = automated.ToHashSet() };
        return true;
    }

    private static bool IsSealed([NotNullWhen(true)] string? value, int maxLength) =>
        value is not null && value.Length >= MinSealedLength && value.Length <= maxLength && Base64Url().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex Base64Url();
}
