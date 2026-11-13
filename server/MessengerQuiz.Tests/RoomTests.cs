using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Tests;

public class RoomTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(1);
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Options are people's numbers.
    private const int Ana = 0;
    private const int Ben = 1;
    private const int Cal = 2;
    private const int NotAnOption = 9;

    private readonly Room _room = NewRoom();
    private readonly Player _host = NewPlayer("Host", isHost: true);
    private readonly Player _guest = NewPlayer("Guest");

    public RoomTests()
    {
        _room.AddPlayer(_host);
        _room.AddPlayer(_guest);
        _room.SetConnection(_host, "host-connection", Start);
        _room.SetConnection(_guest, "guest-connection", Start);
    }

    [Fact]
    public void NamesMustBeUniqueIgnoringCase()
    {
        Assert.Equal(JoinOutcome.NameTaken, _room.AddPlayer(NewPlayer("GUEST")));
    }

    [Fact]
    public void RoomsHoldUpToTheirLimit()
    {
        var room = NewRoom(maxPlayers: 2);
        room.AddPlayer(NewPlayer("A", isHost: true));
        room.AddPlayer(NewPlayer("B"));

        Assert.Equal(JoinOutcome.Full, room.AddPlayer(NewPlayer("C")));
    }

    [Fact]
    public void NobodyCanJoinAfterTheStart()
    {
        StartGame(questionCount: 1);

        Assert.Equal(JoinOutcome.AlreadyStarted, _room.AddPlayer(NewPlayer("Late")));
    }

    [Fact]
    public void OnlyTheHostCanStartAndOnlyWithSomeoneElse()
    {
        var room = NewRoom();
        var host = NewPlayer("Host", isHost: true);
        room.AddPlayer(host);

        Assert.False(room.CanStart(host));
        Assert.False(_room.CanStart(_guest));
        Assert.True(_room.CanStart(_host));
    }

    [Fact]
    public void FasterRightAnswersScoreMore()
    {
        StartGame(questionCount: 1);

        Assert.True(_room.Answer(_host, Ana, Start, Grace));
        Assert.True(_room.Answer(_guest, Ana, Start + TimeSpan.FromSeconds(10), Grace));

        Assert.Equal(1000, _host.Score);
        Assert.Equal(750, _guest.Score);
    }

    [Fact]
    public void RejectsLateRepeatedAndUnknownAnswers()
    {
        StartGame(questionCount: 1);

        Assert.False(_room.Answer(_host, NotAnOption, Start, Grace));
        Assert.True(_room.Answer(_host, Ben, Start, Grace));
        Assert.False(_room.Answer(_host, Ana, Start, Grace));
        Assert.False(_room.Answer(_guest, Ana, Start + Limit + Grace + TimeSpan.FromMilliseconds(1), Grace));
    }

    [Fact]
    public void AnswersWithinTheGraceStillCount()
    {
        StartGame(questionCount: 1);

        Assert.True(_room.Answer(_host, Ana, Start + Limit + Grace, Grace));
        Assert.Equal(500, _host.Score);
    }

    [Fact]
    public void RevealsOnceEveryoneHasAnswered()
    {
        StartGame(questionCount: 1);

        _room.Answer(_host, Ana, Start, Grace);
        Assert.Equal(RoomStage.Question, _room.Stage);

        _room.Answer(_guest, Ben, Start, Grace);
        Assert.Equal(RoomStage.Reveal, _room.Stage);
    }

    [Fact]
    public void TimingOutRevealsAndCountsTheWholeLimit()
    {
        StartGame(questionCount: 1);
        _room.Answer(_host, Ana, Start + TimeSpan.FromSeconds(4), Grace);

        Assert.False(_room.TimeOut(_room.Round - 1));
        Assert.True(_room.TimeOut(_room.Round));

        Assert.Equal(RoomStage.Reveal, _room.Stage);
        Assert.Equal(0, _guest.Score);
        Assert.Equal(Limit, _guest.AnswerTime);
        Assert.Equal(TimeSpan.FromSeconds(4), _host.AnswerTime);
    }

    [Fact]
    public void MovesOnWhenEveryoneIsReady()
    {
        StartGame(questionCount: 2);
        _room.TimeOut(_room.Round);

        _room.MarkReady(_host, Start + Limit);
        Assert.Equal(RoomStage.Reveal, _room.Stage);

        _room.MarkReady(_guest, Start + Limit);
        Assert.Equal(RoomStage.Question, _room.Stage);
        Assert.Equal(1, _room.QuestionIndex);
        Assert.Equal(Start + Limit + Limit, _room.Deadline);
    }

    [Fact]
    public void FinishesAfterTheLastQuestion()
    {
        StartGame(questionCount: 1);
        _room.TimeOut(_room.Round);
        _room.MarkReady(_host, Start);
        _room.MarkReady(_guest, Start);

        Assert.Equal(RoomStage.Finished, _room.Stage);
    }

    [Fact]
    public void DisconnectedPlayersDontHoldUpTheGame()
    {
        StartGame(questionCount: 2);
        _room.Answer(_host, Ana, Start, Grace);

        _room.SetConnection(_guest, null, Start);
        Assert.Equal(RoomStage.Reveal, _room.Stage);

        _room.MarkReady(_host, Start);
        Assert.Equal(RoomStage.Question, _room.Stage);
    }

    [Fact]
    public void WithNobodyConnectedTheGameWaits()
    {
        StartGame(questionCount: 3);
        _room.SetConnection(_host, null, Start);
        _room.SetConnection(_guest, null, Start);

        Assert.Equal(RoomStage.Question, _room.Stage);
        _room.TimeOut(_room.Round);
        Assert.Equal(RoomStage.Reveal, _room.Stage);
        Assert.Equal(0, _room.QuestionIndex);
    }

    [Fact]
    public void RematchesResetScoresAndRememberAskedMessages()
    {
        StartGame(questionCount: 1);
        _room.Answer(_host, Ana, Start, Grace);
        _room.TimeOut(_room.Round);
        _room.MarkReady(_host, Start);
        _room.MarkReady(_guest, Start);

        Assert.True(_room.CanStart(_host));
        _room.Start([Question(9)], [9], Start);

        Assert.Equal(0, _host.Score);
        Assert.Equal([0, 9], _room.AskedPositions.Order());
    }

    [Fact]
    public void TheAnswerIsHiddenUntilTheReveal()
    {
        StartGame(questionCount: 1);
        _room.Answer(_host, Ben, Start, Grace);

        var guestView = RoomView.For(_room, _guest, Start);
        Assert.Null(guestView.Reveal);
        Assert.Null(guestView.Question!.YourAnswer);
        var hostSeenByGuest = guestView.Players.Single(player => player.Id == _host.Id);
        Assert.True(hostSeenByGuest.Answered);
        Assert.Equal(0, hostSeenByGuest.Score);
        Assert.Equal(20_000, guestView.Question.RemainingMilliseconds);

        _room.TimeOut(_room.Round);
        var reveal = RoomView.For(_room, _guest, Start).Reveal!;
        Assert.Equal(Ana, reveal.Answer);
        Assert.Equal(Ben, reveal.Picks.Single(pick => pick.PlayerId == _host.Id).Option);
        Assert.Null(reveal.Picks.Single(pick => pick.PlayerId == _guest.Id).Option);
    }

    [Fact]
    public void PlayersAreRankedByScoreThenSpeed()
    {
        StartGame(questionCount: 1);
        _room.Answer(_guest, Ana, Start + TimeSpan.FromSeconds(2), Grace);
        _room.Answer(_host, Ana, Start + TimeSpan.FromSeconds(2.001), Grace);

        // Both score 950 once rounded, so the faster answer ranks first.
        _room.TimeOut(_room.Round);
        Assert.Equal(_host.Score, _guest.Score);
        Assert.Equal([_guest.Id, _host.Id], RoomView.For(_room, _host, Start).Players.Select(player => player.Id));
    }

    [Fact]
    public void ChatKeepsTheLatestMessages()
    {
        for (var index = 1; index <= Room.MaxChatLines + 10; index++)
        {
            _room.AddChat(_host, TestChats.Sealed($"message {index}"));
        }

        Assert.Equal(Room.MaxChatLines, _room.Chat.Count);
        Assert.Equal("message 11", TestChats.Label(_room.Chat[0].Sealed));
        Assert.Equal(Room.MaxChatLines + 10, _room.Chat[^1].Id);
    }

    [Fact]
    public void ChatWorksDuringQuestionsButNotAfterTheEnd()
    {
        StartGame(questionCount: 1);

        Assert.True(_room.AddChat(_guest, TestChats.Sealed("during a question")));

        _room.End();
        Assert.False(_room.AddChat(_guest, TestChats.Sealed("too late")));
    }

    [Fact]
    public void ActionLimiterAllowsBurstsThenWaits()
    {
        var limiter = new ActionLimiter(2, TimeSpan.FromSeconds(5));

        Assert.True(limiter.TryTake(Start));
        Assert.True(limiter.TryTake(Start + TimeSpan.FromSeconds(1)));
        Assert.False(limiter.TryTake(Start + TimeSpan.FromSeconds(2)));
        Assert.True(limiter.TryTake(Start + TimeSpan.FromSeconds(5)));
    }

    private void StartGame(int questionCount)
    {
        var questions = Enumerable.Range(0, questionCount).Select(Question).ToList();
        _room.Start(questions, questions.Select(question => question.Position), Start);
    }

    private static WhoSaidThisQuestion Question(int position) =>
        new(position, TestChats.Sealed($"message {position}"), 0, [], [Ana, Ben, Cal], Ana, 2);

    private static Room NewRoom(int maxPlayers = 8)
    {
        var chat = TestChats.Chat(2, TestChats.Message());
        return new Room("code", "chat", new ChatSession(chat, chat.Messages), Limit, maxPlayers);
    }

    private static Player NewPlayer(string name, bool isHost = false) =>
        new(RoomStore.NewPlayerId(), RoomStore.NewToken(), name, isHost);
}
