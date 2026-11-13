using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

// Options and the answer are people's numbers. The browser opens the sealed boxes and shows names.
public sealed record WhoSaidThisQuestion(
    int Position,
    string Sealed,
    int MediaCount,
    IReadOnlyList<ContextMessage> Context,
    IReadOnlyList<int> Options,
    int Answer,
    int Reactions);

// An earlier message from the same sender, which helps the question make sense.
public sealed record ContextMessage(int Position, string Sealed, int MediaCount);

public sealed class WhoSaidThisGenerator(MessageSelector selector) : IQuestionGenerator<WhoSaidThisQuestion>
{
    private const int MaxOptions = 4;

    public WhoSaidThisQuestion? Next(ChatSession session)
    {
        var item = session.TakeNext(
            candidates => MessageSelector.PickBalanced(candidates, Random.Shared),
            message => selector.PrecedingContext(session.Chat, message.Position));
        return item is null ? null : Create(session, item.Message, item.Context);
    }

    // Builds the question for a message that has already been chosen, with up to 4 shuffled options. The other
    // options are people who sent top messages, with automated accounts much less likely to be picked.
    public static WhoSaidThisQuestion Create(ChatSession session, Message message, IReadOnlyList<Message> context)
    {
        var others = PickWeighted(
            session.OptionPeople.Where(person => person != message.Sender).ToList(),
            person => session.Chat.Automated.Contains(person) ? AutomatedWeight : 1,
            MaxOptions - 1);

        var options = others.Append(message.Sender).OrderBy(_ => Random.Shared.Next()).ToList();
        var earlier = context.Select(item => new ContextMessage(item.Position, item.Sealed, item.MediaCount)).ToList();

        return new WhoSaidThisQuestion(
            message.Position, message.Sealed, message.MediaCount, earlier, options, message.Sender, message.Reactions);
    }

    // How likely an automated account is to be picked as a wrong answer, compared with a person.
    private const double AutomatedWeight = 0.05;

    // Picks up to count people without repeats, each in proportion to its weight.
    private static List<int> PickWeighted(List<int> people, Func<int, double> weight, int count)
    {
        var picked = new List<int>();
        while (picked.Count < count && people.Count > 0)
        {
            var roll = Random.Shared.NextDouble() * people.Sum(weight);
            var index = 0;
            while (index < people.Count - 1 && (roll -= weight(people[index])) >= 0)
            {
                index++;
            }

            picked.Add(people[index]);
            people.RemoveAt(index);
        }

        return picked;
    }
}
