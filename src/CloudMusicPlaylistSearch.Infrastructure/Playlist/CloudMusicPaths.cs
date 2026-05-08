namespace CloudMusicPlaylistSearch.Infrastructure.Playlist;

public static class CloudMusicPaths
{
    public static string PlayingListPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetEase",
        "CloudMusic",
        "webdata",
        "file",
        "playingList");
}