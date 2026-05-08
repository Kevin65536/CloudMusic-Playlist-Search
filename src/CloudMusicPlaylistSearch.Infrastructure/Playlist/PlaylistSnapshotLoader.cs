using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudMusicPlaylistSearch.Core.Models;
using CloudMusicPlaylistSearch.Core.Search;

namespace CloudMusicPlaylistSearch.Infrastructure.Playlist;

public sealed class PlaylistSnapshotLoader
{
    public PlaylistSnapshot LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = File.ReadAllText(path, Encoding.UTF8);
        var updatedAt = File.GetLastWriteTimeUtc(path);
        return LoadFromJson(json, updatedAt);
    }

    public PlaylistSnapshot LoadFromJson(string json, DateTimeOffset? updatedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var tracks = new List<PlaylistTrack>();
        var sourceName = string.Empty;

        if (root.TryGetProperty("list", out var listElement)
            && listElement.ValueKind == JsonValueKind.Array)
        {
            var fallbackIndex = 1;
            foreach (var item in listElement.EnumerateArray())
            {
                if (sourceName.Length == 0)
                {
                    sourceName = ReadSourceName(item);
                }

                var track = ReadTrack(item, fallbackIndex);
                if (track is not null)
                {
                    tracks.Add(track);
                }

                fallbackIndex++;
            }
        }

        return new PlaylistSnapshot(
            updatedAt ?? DateTimeOffset.UtcNow,
            sourceName,
            tracks,
            ComputeHash(json));
    }

    private static PlaylistTrack? ReadTrack(JsonElement item, int fallbackIndex)
    {
        if (!item.TryGetProperty("track", out var trackElement)
            || trackElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = ReadString(trackElement, "name") ?? "未知歌曲";
        var artist = ReadFirstArtist(trackElement);
        var album = ReadNestedString(trackElement, "album", "name") ?? string.Empty;

        var displayIndex = fallbackIndex;
        if (TryReadInt32(item, "displayOrder", out var displayOrder))
        {
            displayIndex = displayOrder + 1;
        }

        return new PlaylistTrack(
            ReadTrackId(trackElement),
            displayIndex,
            name,
            artist,
            album,
            SearchTextNormalizer.Compose(name, artist, album));
    }

    private static long ReadTrackId(JsonElement trackElement)
    {
        if (!trackElement.TryGetProperty("id", out var idElement))
        {
            return 0;
        }

        if (idElement.ValueKind == JsonValueKind.Number
            && idElement.TryGetInt64(out var numericId))
        {
            return numericId;
        }

        if (idElement.ValueKind == JsonValueKind.String
            && long.TryParse(idElement.GetString(), out var stringId))
        {
            return stringId;
        }

        return 0;
    }

    private static string ReadFirstArtist(JsonElement trackElement)
    {
        if (!trackElement.TryGetProperty("artists", out var artistsElement)
            || artistsElement.ValueKind != JsonValueKind.Array)
        {
            return "未知歌手";
        }

        foreach (var artist in artistsElement.EnumerateArray())
        {
            var name = ReadString(artist, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return "未知歌手";
    }

    private static string ReadSourceName(JsonElement item)
    {
        return ReadNestedString(item, "fromInfo", "sourceData", "name") ?? string.Empty;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string? ReadNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static bool TryReadInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out value);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(property.GetString(), out value);
        }

        return false;
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}