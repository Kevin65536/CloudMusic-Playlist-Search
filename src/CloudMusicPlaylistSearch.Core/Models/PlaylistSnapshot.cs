namespace CloudMusicPlaylistSearch.Core.Models;

public sealed record PlaylistSnapshot(
    DateTimeOffset UpdatedAt,
    string SourceName,
    IReadOnlyList<PlaylistTrack> Tracks,
    string ContentHash);