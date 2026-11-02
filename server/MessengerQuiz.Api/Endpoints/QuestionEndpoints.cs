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

    private static IResult GetWhoSaidThis(
        HttpContext context,
        ChatStore store,
        IQuestionGenerator<WhoSaidThisQuestion> generator)
    {
        if (!store.TryGet(context.GetChatId(), out var session) || session is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "This chat has expired. Upload it again to keep playing.");
        }

        var question = generator.Next(session);
        return question is null ? Results.NoContent() : Results.Ok(question);
    }
}
