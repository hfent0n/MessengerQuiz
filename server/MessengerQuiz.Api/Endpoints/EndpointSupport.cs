namespace MessengerQuiz.Api.Endpoints;

internal static class EndpointSupport
{
    private const string ChatIdHeader = "X-Chat-Id";

    public static string GetChatId(this HttpContext context) => context.Request.Headers[ChatIdHeader].ToString();

    public static IResult Problem(int statusCode, string detail) =>
        Results.Problem(detail: detail, statusCode: statusCode);
}
