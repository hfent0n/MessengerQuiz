using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Storage;
using static MessengerQuiz.Api.Endpoints.EndpointSupport;

namespace MessengerQuiz.Api.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/questions/who-said-this", GetWhoSaidThis);
    }

    // ?selection=random asks for any message that ends its run, rather than the most reacted.
    private static IResult GetWhoSaidThis(
        HttpContext context,
        ChatStore store,
        IQuestionGenerator<WhoSaidThisQuestion> generator,
        string? selection)
    {
        if (!TryParseSelection(selection, out var chosen))
        {
            return Problem(StatusCodes.Status400BadRequest, "Choose reacted or random messages.");
        }

        if (!store.TryGet(context.GetChatId(), out var session) || session is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "This chat has expired. Upload it again to keep playing.");
        }

        var question = generator.Next(session, chosen);
        return question is null ? Results.NoContent() : Results.Ok(question);
    }

    public static bool TryParseSelection(string? value, out QuestionSelection selection)
    {
        selection = QuestionSelection.Reacted;
        return string.IsNullOrEmpty(value)
            || (Enum.TryParse(value, ignoreCase: true, out selection) && Enum.IsDefined(selection));
    }
}
