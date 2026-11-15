using MessengerQuiz.Api.Questions;

namespace MessengerQuiz.Api.Multiplayer;

public enum MediaUpload { Stored, NotWanted, TooLarge, RoomFull }

public enum MediaLookup { Found, NotYet, Unavailable }

// Photos, GIFs and videos relayed from the host's browser to the other players. The host seals each file with the
// game link's key, labelled with its message and slot, so the server holds them without being able to see them.
// Kept in memory only, and gone when the room is. Callers hold the room's lock.
public sealed class RoomMedia
{
    // How many files each message in the game has. Only these can be uploaded.
    private readonly Dictionary<int, int> _wanted = [];

    // Which question each message is shown with, so players can't fetch media for questions they haven't reached.
    private readonly Dictionary<int, int> _questionOf = [];

    // A null file means the host couldn't send it, such as a video over the size limit.
    private readonly Dictionary<(int Position, int Index), byte[]?> _files = [];

    public long Bytes { get; private set; }

    // Called when a game starts with media. Files already sent for an earlier game are kept.
    public void Want(IReadOnlyList<WhoSaidThisQuestion> questions)
    {
        _wanted.Clear();
        _questionOf.Clear();
        for (var number = 0; number < questions.Count; number++)
        {
            var question = questions[number];
            foreach (var (position, count) in question.Context.Select(item => (item.Position, item.MediaCount))
                .Append((question.Position, question.MediaCount))
                .Where(item => item.Item2 > 0))
            {
                _wanted[position] = count;
                _questionOf[position] = number;
            }
        }
    }

    // Messages the host still needs to send files for.
    public IEnumerable<(int Position, int Count)> Missing() => _wanted
        .Where(entry => Enumerable.Range(0, entry.Value).Any(index => !_files.ContainsKey((entry.Key, index))))
        .Select(entry => (entry.Key, entry.Value));

    public MediaUpload Store(int position, int index, byte[]? file, long maxFileBytes, long maxRoomBytes, Func<long, bool> reserveServerBytes)
    {
        if (!_wanted.TryGetValue(position, out var count) || index < 0 || index >= count || _files.ContainsKey((position, index)))
        {
            return MediaUpload.NotWanted;
        }

        if (file is not null)
        {
            if (file.Length > maxFileBytes)
            {
                return MediaUpload.TooLarge;
            }

            if (Bytes + file.Length > maxRoomBytes || !reserveServerBytes(file.Length))
            {
                return MediaUpload.RoomFull;
            }

            Bytes += file.Length;
        }

        _files[(position, index)] = file;
        return MediaUpload.Stored;
    }

    public MediaLookup Find(int position, int index, int currentQuestion, out byte[]? file)
    {
        file = null;
        if (!_questionOf.TryGetValue(position, out var number) || number > currentQuestion)
        {
            return MediaLookup.Unavailable;
        }

        if (!_files.TryGetValue((position, index), out file))
        {
            return index >= 0 && index < _wanted[position] ? MediaLookup.NotYet : MediaLookup.Unavailable;
        }

        return file is null ? MediaLookup.Unavailable : MediaLookup.Found;
    }
}
