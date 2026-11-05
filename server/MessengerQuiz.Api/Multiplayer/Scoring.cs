namespace MessengerQuiz.Api.Multiplayer;

public static class Scoring
{
    public const int CorrectPoints = 500;
    public const int SpeedPoints = 500;
    public const int MaxPointsPerQuestion = CorrectPoints + SpeedPoints;

    // The speed bonus falls in a straight line from the moment the question appears to the deadline.
    public static int Points(bool correct, TimeSpan elapsed, TimeSpan timeLimit)
    {
        if (!correct)
        {
            return 0;
        }

        var share = Math.Clamp(elapsed / timeLimit, 0, 1);
        return CorrectPoints + (int)Math.Round(SpeedPoints * (1 - share));
    }
}
