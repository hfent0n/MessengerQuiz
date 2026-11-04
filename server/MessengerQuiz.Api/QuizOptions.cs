namespace MessengerQuiz.Api;

public sealed class QuizOptions
{
    public const string SectionName = "Quiz";

    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;
    public long ChatMemoryCapBytes { get; set; } = 1024 * 1024 * 1024;
    public TimeSpan ChatExpiry { get; set; } = TimeSpan.FromHours(2);
    public int MaxConcurrentParses { get; set; } = 2;
    public int ParseQueueLimit { get; set; } = 4;
    public int UploadsPerMinutePerIp { get; set; } = 5;

    public int MinMessageLength { get; set; } = 30;

    // Questions come from this share of messages, ranked by reaction count.
    public double TopReactionShare { get; set; } = 0.05;

    // Small chats use this many top messages instead, so they still get a quiz.
    public int MinPoolSize { get; set; } = 50;

    // Up to this many earlier messages from the same sender are shown as context.
    public int MaxContextMessages { get; set; } = 3;

    // Each context message must be within this time of the next one.
    public TimeSpan ContextGap { get; set; } = TimeSpan.FromMinutes(2);

    // The most photo paths one upload can list as available.
    public int MaxAvailablePhotos { get; set; } = 50_000;

    // Below this many messages, the chat is rejected as too small to quiz on.
    public int MinQuizMessages { get; set; } = 10;
}
