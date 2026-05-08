using CloudMusicPlaylistSearch.Core.Models;

namespace CloudMusicPlaylistSearch.Core.Search;

public sealed class PlaylistSearchEngine
{
    public IReadOnlyList<PlaylistTrack> Search(
        PlaylistSnapshot snapshot,
        string? query,
        int maxResults = 200)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (maxResults <= 0)
        {
            return Array.Empty<PlaylistTrack>();
        }

        var searchQuery = SearchQuery.Create(query);
        if (searchQuery.IsEmpty)
        {
            return snapshot.Tracks
                .OrderBy(track => track.DisplayIndex)
                .Take(maxResults)
                .ToArray();
        }

        return snapshot.Tracks
            .Select(track => new SearchCandidate(track, Score(track, searchQuery)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Track.DisplayIndex)
            .Take(maxResults)
            .Select(candidate => candidate.Track)
            .ToArray();
    }

    private static int Score(PlaylistTrack track, SearchQuery query)
    {
        var normalizedName = SearchTextNormalizer.Normalize(track.Name);
        var normalizedArtist = SearchTextNormalizer.Normalize(track.Artist);
        var normalizedSearchText = track.SearchText.Length == 0
            ? SearchTextNormalizer.Compose(track.Name, track.Artist, track.Album)
            : SearchTextNormalizer.Normalize(track.SearchText);

        var combinedTokens = SplitTokens(normalizedSearchText);
        if (!TryMatchTokens(combinedTokens, query.Tokens, requireOrder: false, out _))
        {
            return 0;
        }

        var nameScore = ScoreField(
            normalizedName,
            query,
            exactBase: 1000,
            orderedTokenBase: 900,
            unorderedTokenBase: 800,
            startsWithBonus: 80,
            containsBonus: 40);

        var artistScore = ScoreField(
            normalizedArtist,
            query,
            exactBase: 650,
            orderedTokenBase: 550,
            unorderedTokenBase: 450,
            startsWithBonus: 60,
            containsBonus: 30);

        var combinedScore = ScoreField(
            normalizedSearchText,
            query,
            exactBase: 0,
            orderedTokenBase: 260,
            unorderedTokenBase: 180,
            startsWithBonus: 0,
            containsBonus: 0);

        return Math.Max(nameScore, Math.Max(artistScore, combinedScore));
    }

    private sealed record SearchCandidate(PlaylistTrack Track, int Score);

    private sealed record SearchQuery(string NormalizedText, string[] Tokens)
    {
        public bool IsEmpty => Tokens.Length == 0;

        public static SearchQuery Create(string? query)
        {
            var normalized = SearchTextNormalizer.Normalize(query);
            return new SearchQuery(normalized, SplitTokens(normalized));
        }
    }

    private static int ScoreField(
        string normalizedField,
        SearchQuery query,
        int exactBase,
        int orderedTokenBase,
        int unorderedTokenBase,
        int startsWithBonus,
        int containsBonus)
    {
        if (normalizedField.Length == 0)
        {
            return 0;
        }

        var fieldTokens = SplitTokens(normalizedField);
        if (!TryMatchTokens(fieldTokens, query.Tokens, requireOrder: false, out var unorderedQuality))
        {
            return 0;
        }

        var score = unorderedTokenBase + unorderedQuality * 10;
        if (TryMatchTokens(fieldTokens, query.Tokens, requireOrder: true, out var orderedQuality))
        {
            score = Math.Max(score, orderedTokenBase + orderedQuality * 10);
        }

        if (exactBase > 0 && normalizedField.Equals(query.NormalizedText, StringComparison.Ordinal))
        {
            score = Math.Max(score, exactBase);
        }
        else if (startsWithBonus > 0 && normalizedField.StartsWith(query.NormalizedText, StringComparison.Ordinal))
        {
            score += startsWithBonus;
        }
        else if (containsBonus > 0 && normalizedField.Contains(query.NormalizedText, StringComparison.Ordinal))
        {
            score += containsBonus;
        }

        return score;
    }

    private static bool TryMatchTokens(
        string[] fieldTokens,
        string[] queryTokens,
        bool requireOrder,
        out int quality)
    {
        quality = 0;
        if (fieldTokens.Length == 0 || queryTokens.Length == 0)
        {
            return false;
        }

        if (requireOrder)
        {
            var searchStart = 0;
            foreach (var queryToken in queryTokens)
            {
                var bestQuality = 0;
                var matchedIndex = -1;

                for (var index = searchStart; index < fieldTokens.Length; index++)
                {
                    var currentQuality = MatchQuality(fieldTokens[index], queryToken);
                    if (currentQuality <= bestQuality)
                    {
                        continue;
                    }

                    bestQuality = currentQuality;
                    matchedIndex = index;
                    if (currentQuality == 3)
                    {
                        break;
                    }
                }

                if (matchedIndex < 0)
                {
                    quality = 0;
                    return false;
                }

                quality += bestQuality;
                searchStart = matchedIndex + 1;
            }

            return true;
        }

        foreach (var queryToken in queryTokens)
        {
            var bestQuality = 0;
            foreach (var fieldToken in fieldTokens)
            {
                bestQuality = Math.Max(bestQuality, MatchQuality(fieldToken, queryToken));
                if (bestQuality == 3)
                {
                    break;
                }
            }

            if (bestQuality == 0)
            {
                quality = 0;
                return false;
            }

            quality += bestQuality;
        }

        return true;
    }

    private static int MatchQuality(string fieldToken, string queryToken)
    {
        if (fieldToken.Equals(queryToken, StringComparison.Ordinal))
        {
            return 3;
        }

        if (fieldToken.StartsWith(queryToken, StringComparison.Ordinal))
        {
            return 2;
        }

        if (fieldToken.Contains(queryToken, StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }

    private static string[] SplitTokens(string normalizedText)
    {
        if (normalizedText.Length == 0)
        {
            return [];
        }

        return normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}