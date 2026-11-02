using System.Text.RegularExpressions;

namespace MessengerQuiz.Api.Selection;

public static partial class SystemMessages
{
    // Real messages can match these phrases, but losing the odd one from the pool is harmless.
    private const int MaxLength = 120;

    public static bool IsSystemLine(string text) => text.Length <= MaxLength && SystemLine().IsMatch(text);

    public static bool IsLinkOnly(string text) => LinkOnly().IsMatch(text);

    [GeneratedRegex(
        @"reacted .{1,12} to|joined the (video )?call|started a (video )?call|set the nickname|set (his|her|their|your) own|cleared the nickname|named the group|sent an attachment|sent a live|missed a|called you|changed the|left the group|added .+ to the group|removed .+ from|unsent a message|pinned|voted for|created (a|the) poll|set the quick reaction|see when .+ open this chat",
        RegexOptions.IgnoreCase)]
    private static partial Regex SystemLine();

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex LinkOnly();
}
