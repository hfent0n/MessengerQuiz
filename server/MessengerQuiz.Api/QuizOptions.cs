namespace MessengerQuiz.Api;

public sealed class QuizOptions
{
    public const string SectionName = "Quiz";

    // Behind a proxy such as Cloud Run's, every request seems to come from the proxy. This reads the visitor's
    // address from the X-Forwarded-For header instead, so rate limits apply per visitor. Only turn it on behind
    // a proxy that sets that header, or visitors could claim any address.
    public bool TrustForwardedHeaders { get; set; }

    // Sized so a few uploads at once fit in a 512 MB Cloud Run instance alongside the stored chats.
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;

    // Uploads arrive gzipped, so this limits their size once decompressed.
    public long MaxChatBytes { get; set; } = 40 * 1024 * 1024;
    public long ChatMemoryCapBytes { get; set; } = 192 * 1024 * 1024;

    // Limits on what one upload can hold.
    public int MaxMessages { get; set; } = 300_000;
    public int MaxPeople { get; set; } = 1_000;

    // The longest sealed box, in characters. Generous, since one message can be long.
    public int MaxSealedLength { get; set; } = 100_000;
    public TimeSpan ChatExpiry { get; set; } = TimeSpan.FromHours(2);
    public int MaxConcurrentParses { get; set; } = 2;
    public int ParseQueueLimit { get; set; } = 4;
    public int UploadsPerMinutePerIp { get; set; } = 5;

    // Questions come from this share of messages, ranked by reaction count.
    public double TopReactionShare { get; set; } = 0.05;

    // Small chats use this many top messages instead, so they still get a quiz.
    public int MinPoolSize { get; set; } = 50;

    // Up to this many earlier messages from the same sender are shown as context.
    public int MaxContextMessages { get; set; } = 3;

    // Each context message must be within this time of the next one.
    public TimeSpan ContextGap { get; set; } = TimeSpan.FromMinutes(2);

    // Multiplayer.
    public int MaxPlayers { get; set; } = 8;
    public int QuestionsPerGame { get; set; } = 10;
    public int[] TimeLimitSeconds { get; set; } = [10, 20, 30, 60];
    public int MaxRoomsPerChat { get; set; } = 5;
    public int RoomRequestsPerMinutePerIp { get; set; } = 20;

    // Answers this long after the deadline still count, to allow for network delay.
    public TimeSpan AnswerGrace { get; set; } = TimeSpan.FromSeconds(1);

    // Below this many messages, the chat is rejected as too small to quiz on.
    public int MinQuizMessages { get; set; } = 10;
}
