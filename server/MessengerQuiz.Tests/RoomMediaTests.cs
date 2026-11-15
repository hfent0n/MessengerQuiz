using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Questions;

namespace MessengerQuiz.Tests;

public class RoomMediaTests
{
    private const long FileLimit = 100;
    private const long RoomLimit = 250;

    private readonly RoomMedia _media = new();

    public RoomMediaTests()
    {
        // Question 0 has two photos and a photo in its context. Question 1 has one.
        _media.Want([
            new WhoSaidThisQuestion(5, TestChats.Sealed("q0"), 2, [new ContextMessage(4, TestChats.Sealed("c"), 1)], [0, 1], 0, 0),
            new WhoSaidThisQuestion(9, TestChats.Sealed("q1"), 1, [], [0, 1], 1, 0),
        ]);
    }

    [Fact]
    public void ListsWhatTheHostStillNeedsToSend()
    {
        Store(5, 0, 10);

        Assert.Equal([(4, 1), (5, 2), (9, 1)], _media.Missing().OrderBy(item => item.Position));

        Store(5, 1, 10);
        Assert.Equal([4, 9], _media.Missing().Select(item => item.Position).Order());
    }

    [Fact]
    public void OnlyFilesTheGameNeedsAreAccepted()
    {
        Assert.Equal(MediaUpload.NotWanted, Store(6, 0, 10));
        Assert.Equal(MediaUpload.NotWanted, Store(5, 2, 10));
        Assert.Equal(MediaUpload.Stored, Store(5, 0, 10));
        Assert.Equal(MediaUpload.NotWanted, Store(5, 0, 10));
    }

    [Fact]
    public void EnforcesTheFileAndRoomLimits()
    {
        Assert.Equal(MediaUpload.TooLarge, Store(5, 0, 101));
        Assert.Equal(MediaUpload.Stored, Store(5, 0, 100));
        Assert.Equal(MediaUpload.Stored, Store(5, 1, 100));
        Assert.Equal(MediaUpload.RoomFull, Store(4, 0, 100));
        Assert.Equal(200, _media.Bytes);
    }

    [Fact]
    public void EnforcesTheServerLimit()
    {
        Assert.Equal(MediaUpload.RoomFull, _media.Store(5, 0, new byte[10], FileLimit, RoomLimit, _ => false));
        Assert.Equal(0, _media.Bytes);
    }

    [Fact]
    public void PlayersOnlyGetMediaForQuestionsTheyveReached()
    {
        Store(5, 0, 10);
        Store(9, 0, 10);

        Assert.Equal(MediaLookup.Found, _media.Find(5, 0, currentQuestion: 0, out var file));
        Assert.Equal(10, file!.Length);
        Assert.Equal(MediaLookup.Unavailable, _media.Find(9, 0, currentQuestion: 0, out _));
        Assert.Equal(MediaLookup.Found, _media.Find(9, 0, currentQuestion: 1, out _));
    }

    [Fact]
    public void TellsPlayersToWaitOrGiveUp()
    {
        _media.Store(4, 0, null, FileLimit, RoomLimit, _ => true);

        Assert.Equal(MediaLookup.NotYet, _media.Find(5, 1, currentQuestion: 0, out _));
        // The host couldn't send this one.
        Assert.Equal(MediaLookup.Unavailable, _media.Find(4, 0, currentQuestion: 0, out _));
        Assert.Equal(MediaLookup.Unavailable, _media.Find(5, 7, currentQuestion: 0, out _));
    }

    private MediaUpload Store(int position, int index, int bytes) =>
        _media.Store(position, index, new byte[bytes], FileLimit, RoomLimit, _ => true);
}
