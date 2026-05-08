using CloudMusicPlaylistSearch.Core.Models;
using CloudMusicPlaylistSearch.Core.Search;

namespace CloudMusicPlaylistSearch.Tests.Core;

public sealed class PlaylistSearchEngineTests
{
    private readonly PlaylistSearchEngine _searchEngine = new();

    [Fact]
    public void Search_WhenQueryIsEmpty_ReturnsOriginalOrder()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(2, "High Hopes", "Pink Floyd"),
            CreateTrack(1, "Take It Back", "Pink Floyd"),
            CreateTrack(3, "Islands", "King Crimson"));

        var results = _searchEngine.Search(snapshot, string.Empty, maxResults: 10);

        Assert.Collection(
            results,
            track => Assert.Equal("Take It Back", track.Name),
            track => Assert.Equal("High Hopes", track.Name),
            track => Assert.Equal("Islands", track.Name));
    }

    [Fact]
    public void Search_PrioritizesSongNameMatchesOverArtistMatches()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Stone In Love", "Journey"),
            CreateTrack(2, "Wild Horses", "The Rolling Stones"),
            CreateTrack(3, "Stoner", "Amoeba"));

        var results = _searchEngine.Search(snapshot, "stone", maxResults: 10);

        Assert.Collection(
            results,
            track => Assert.Equal("Stone In Love", track.Name),
            track => Assert.Equal("Stoner", track.Name),
            track => Assert.Equal("Wild Horses", track.Name));
    }

    [Fact]
    public void Search_FindsArtistSubstringMatches()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Take It Back", "Pink Floyd"),
            CreateTrack(2, "Somewhere I Belong", "Linkin Park"),
            CreateTrack(3, "Let Down", "Radiohead"));

        var results = _searchEngine.Search(snapshot, "park", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Somewhere I Belong", match.Name);
    }

    [Fact]
    public void Search_MatchesSeparatedNameTokens()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Take It Back", "Pink Floyd"),
            CreateTrack(2, "Take Me Out", "Franz Ferdinand"),
            CreateTrack(3, "Back To Black", "Amy Winehouse"));

        var results = _searchEngine.Search(snapshot, "take back", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Take It Back", match.Name);
    }

    [Fact]
    public void Search_MatchesTokensAcrossNameAndArtist()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "High Hopes", "Pink Floyd"),
            CreateTrack(2, "Pink Moon", "Nick Drake"),
            CreateTrack(3, "Hopes And Fears", "Keane"));

        var results = _searchEngine.Search(snapshot, "pink hopes", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("High Hopes", match.Name);
    }

    [Fact]
    public void Search_NormalizesPunctuationAndExtraWhitespace()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Don't Stop", "Fleetwood Mac"),
            CreateTrack(2, "Stop Crying Your Heart Out", "Oasis"),
            CreateTrack(3, "Go Your Own Way", "Fleetwood Mac"));

        var results = _searchEngine.Search(snapshot, "  dont   stop  ", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Don't Stop", match.Name);
    }

    [Fact]
    public void Search_ReturnsAllTracksSharingTheSameKeyword()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Never Stop", "Brand New Heavies"),
            CreateTrack(2, "Don't Stop", "Fleetwood Mac"),
            CreateTrack(3, "Stop Crying Your Heart Out", "Oasis"));

        var results = _searchEngine.Search(snapshot, "stop", maxResults: 10);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, track => track.Name == "Never Stop");
        Assert.Contains(results, track => track.Name == "Don't Stop");
        Assert.Contains(results, track => track.Name == "Stop Crying Your Heart Out");
    }

    [Fact]
    public void Search_FindsTrackByPartialArtistPhrase_RealWorldCase()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Farewell Ballad", "Black Label Society"),
            CreateTrack(2, "The Farewell", "Alexey Omelchuk"),
            CreateTrack(3, "Running In Circles", "Dead Poet Society"));

        var results = _searchEngine.Search(snapshot, "black label", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Farewell Ballad", match.Name);
    }

    [Fact]
    public void Search_FindsTrackByExactArtistPhrase_RealWorldCase()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Farewell Ballad", "Black Label Society"),
            CreateTrack(2, "The Farewell", "Alexey Omelchuk"),
            CreateTrack(3, "Running In Circles", "Dead Poet Society"));

        var results = _searchEngine.Search(snapshot, "black label society", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Farewell Ballad", match.Name);
    }

    [Fact]
    public void Search_FindsTrackByExactSongTitle_RealWorldCase()
    {
        var snapshot = CreateSnapshot(
            CreateTrack(1, "Farewell Ballad", "Black Label Society"),
            CreateTrack(2, "The Farewell", "Alexey Omelchuk"),
            CreateTrack(3, "Running In Circles", "Dead Poet Society"));

        var results = _searchEngine.Search(snapshot, "farewell ballad", maxResults: 10);

        var match = Assert.Single(results);
        Assert.Equal("Farewell Ballad", match.Name);
    }

    private static PlaylistSnapshot CreateSnapshot(params PlaylistTrack[] tracks)
    {
        return new PlaylistSnapshot(
            DateTimeOffset.Parse("2026-05-08T10:00:00+00:00"),
            "测试播放列表",
            tracks,
            "hash");
    }

    private static PlaylistTrack CreateTrack(int displayIndex, string name, string artist)
    {
        return new PlaylistTrack(
            displayIndex,
            displayIndex,
            name,
            artist,
            string.Empty,
            SearchTextNormalizer.Compose(name, artist));
    }
}