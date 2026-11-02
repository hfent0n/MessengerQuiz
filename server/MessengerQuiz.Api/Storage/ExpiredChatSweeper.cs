namespace MessengerQuiz.Api.Storage;

public sealed class ExpiredChatSweeper(ChatStore store) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                store.RemoveExpired();
            }
        }
        catch (OperationCanceledException)
        {
            // The server is shutting down.
        }
    }
}
