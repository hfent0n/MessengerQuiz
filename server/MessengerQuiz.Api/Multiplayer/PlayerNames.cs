using System.Text;
using System.Text.RegularExpressions;

namespace MessengerQuiz.Api.Multiplayer;

public static partial class PlayerNames
{
    public const int MaxLength = 20;

    // Letters in any language, combining accents and digits, with single spaces between words.
    // Anything else, including punctuation, symbols, control and invisible characters, is rejected.
    [GeneratedRegex(@"^[\p{L}\p{M}\p{N}]+( [\p{L}\p{M}\p{N}]+)*$")]
    private static partial Regex Allowed();

    // Returns the cleaned name, or null if it isn't allowed.
    public static string? Clean(string? name)
    {
        if (name is null)
        {
            return null;
        }

        // NFC joins letters and accents typed separately, so "José" is the same however it was entered.
        var cleaned = name.Trim().Normalize(NormalizationForm.FormC);
        return cleaned.Length is > 0 and <= MaxLength && Allowed().IsMatch(cleaned) ? cleaned : null;
    }
}
