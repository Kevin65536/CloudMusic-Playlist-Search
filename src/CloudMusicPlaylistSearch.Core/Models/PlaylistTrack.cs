namespace CloudMusicPlaylistSearch.Core.Models;

public sealed record PlaylistTrack(
    long TrackId,
    int DisplayIndex,
    string Name,
    string Artist,
    string Album,
    string SearchText);