using System.Text;

namespace CloudMusicPlaylistSearch.Core.Search;

public static class SearchTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var previousWhitespace = false;

        foreach (var character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                AppendWhitespace(builder, ref previousWhitespace);
                continue;
            }

            if (IsJoiner(character))
            {
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                AppendWhitespace(builder, ref previousWhitespace);
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            previousWhitespace = false;
        }

        return builder.ToString();
    }

    public static string[] Tokenize(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string Compose(params string?[] parts)
    {
        return string.Join(
            ' ',
            parts.Select(Normalize).Where(part => part.Length > 0));
    }

    private static bool IsJoiner(char character)
    {
        return character is '\'' or '’' or '‘' or '＇';
    }

    private static void AppendWhitespace(StringBuilder builder, ref bool previousWhitespace)
    {
        if (previousWhitespace || builder.Length == 0)
        {
            return;
        }

        builder.Append(' ');
        previousWhitespace = true;
    }
}