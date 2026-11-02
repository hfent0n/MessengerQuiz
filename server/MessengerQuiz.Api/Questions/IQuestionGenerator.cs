using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

public interface IQuestionGenerator<out TQuestion> where TQuestion : class
{
    // Returns null when the chat has no questions left.
    TQuestion? Next(ChatSession session);
}
