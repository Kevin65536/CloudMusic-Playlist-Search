using CloudMusicPlaylistSearch.Infrastructure.Playlist;

namespace CloudMusicPlaylistSearch.Tests.Infrastructure;

public sealed class PlaylistSnapshotLoaderTests
{
    private const string SampleJson = """
        {
          "list": [
            {
              "displayOrder": 0,
              "fromInfo": {
                "sourceData": {
                  "name": "收藏喜欢的歌"
                }
              },
              "track": {
                "id": "101",
                "name": "Take It Back",
                "album": {
                  "name": "The Division Bell"
                },
                "artists": [
                  {
                    "name": "Pink Floyd"
                  }
                ]
              }
            },
            {
              "displayOrder": 1,
              "track": {
                "id": 102,
                "name": "Islands",
                "album": {
                  "name": "Islands"
                },
                "artists": [
                  {
                    "name": "King Crimson"
                  }
                ]
              }
            },
            {
              "track": {
                "id": "103",
                "name": "With This Tear",
                "album": {
                  "name": "Lovesexy"
                },
                "artists": [
                  {
                    "name": "Prince"
                  }
                ]
              }
            }
          ]
        }
        """;

    [Fact]
    public void LoadFromJson_ExtractsPlaylistSummaryAndTracks()
    {
        var loader = new PlaylistSnapshotLoader();

        var snapshot = loader.LoadFromJson(
            SampleJson,
            DateTimeOffset.Parse("2026-05-08T12:30:00+08:00"));

        Assert.Equal("收藏喜欢的歌", snapshot.SourceName);
        Assert.Equal(3, snapshot.Tracks.Count);
        Assert.Equal("2026-05-08T12:30:00.0000000+08:00", snapshot.UpdatedAt.ToString("O"));
        Assert.NotEmpty(snapshot.ContentHash);

        var firstTrack = snapshot.Tracks[0];
        Assert.Equal(101, firstTrack.TrackId);
        Assert.Equal(1, firstTrack.DisplayIndex);
        Assert.Equal("Take It Back", firstTrack.Name);
        Assert.Equal("Pink Floyd", firstTrack.Artist);
        Assert.Equal("The Division Bell", firstTrack.Album);
        Assert.Contains("pink floyd", firstTrack.SearchText);

        var thirdTrack = snapshot.Tracks[2];
        Assert.Equal(3, thirdTrack.DisplayIndex);
    }
}