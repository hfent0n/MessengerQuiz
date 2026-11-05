using MessengerQuiz.Api.Multiplayer;

namespace MessengerQuiz.Tests;

public class ScoringAndNameTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(10, 750)]
    [InlineData(20, 500)]
    [InlineData(25, 500)]
    public void RightAnswersScoreMoreWhenFaster(int seconds, int points)
    {
        Assert.Equal(points, Scoring.Points(correct: true, TimeSpan.FromSeconds(seconds), Limit));
    }

    [Fact]
    public void WrongAnswersScoreNothing()
    {
        Assert.Equal(0, Scoring.Points(correct: false, TimeSpan.Zero, Limit));
    }

    [Theory]
    [InlineData("Ana", "Ana")]
    [InlineData("  Mary Ann  ", "Mary Ann")]
    [InlineData("José", "José")]
    [InlineData("Player 2", "Player 2")]
    [InlineData("Zoë Ångström", "Zoë Ångström")]
    public void AcceptsLettersDigitsAndSingleSpaces(string name, string cleaned)
    {
        Assert.Equal(cleaned, PlayerNames.Clean(name));
    }

    [Fact]
    public void JoinsAccentsTypedSeparately()
    {
        Assert.Equal("José", PlayerNames.Clean("José"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mary  Ann")]
    [InlineData("<script>")]
    [InlineData("O'Brien")]
    [InlineData("Ana​")]
    [InlineData("Ana\tBen")]
    [InlineData("A name that is far too long")]
    [InlineData(null)]
    public void RejectsEverythingElse(string? name)
    {
        Assert.Null(PlayerNames.Clean(name));
    }
}
