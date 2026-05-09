namespace CloudMusicPlaylistSearch.Infrastructure.Playback;

public static class CloudMusicTrackActivationPlan
{
    private const ushort VkHome = 0x24;
    private const ushort VkDown = 0x28;
    private const ushort VkReturn = 0x0D;
    private const int MaxSupportedIndex = 5000;

    public static IReadOnlyList<ushort> BuildNavigationKeys(int displayIndex)
    {
        var normalizedIndex = Math.Clamp(displayIndex, 1, MaxSupportedIndex);
        var keys = new List<ushort>(normalizedIndex + 1)
        {
            VkHome,
        };

        for (var index = 1; index < normalizedIndex; index++)
        {
            keys.Add(VkDown);
        }

        keys.Add(VkReturn);
        return keys;
    }
}