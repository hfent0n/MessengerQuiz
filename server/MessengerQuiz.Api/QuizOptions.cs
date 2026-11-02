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
    public int MinReactions { get; set; } = 1;

    // Below this many messages, the reaction requirement is dropped.
    public int MinPoolSize { get; set; } = 50;

    // Below this many messages, the chat is rejected as too small to quiz on.
    public int MinQuizMessages { get; set; } = 10;
}
