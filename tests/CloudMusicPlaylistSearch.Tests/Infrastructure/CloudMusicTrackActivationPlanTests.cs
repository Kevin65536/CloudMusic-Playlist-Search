using CloudMusicPlaylistSearch.Infrastructure.Playback;

namespace CloudMusicPlaylistSearch.Tests.Infrastructure;

public sealed class CloudMusicTrackActivationPlanTests
{
    [Fact]
    public void BuildNavigationKeys_FirstTrack_UsesHomeThenEnter()
    {
        var keys = CloudMusicTrackActivationPlan.BuildNavigationKeys(1);

        Assert.Equal(new ushort[] { 0x24, 0x0D }, keys);
    }

    [Fact]
    public void BuildNavigationKeys_ThirdTrack_UsesHomeDownDownEnter()
    {
        var keys = CloudMusicTrackActivationPlan.BuildNavigationKeys(3);

        Assert.Equal(new ushort[] { 0x24, 0x28, 0x28, 0x0D }, keys);
    }

    [Fact]
    public void BuildNavigationKeys_NonPositiveIndex_ClampsToFirstTrack()
    {
        var keys = CloudMusicTrackActivationPlan.BuildNavigationKeys(0);

        Assert.Equal(new ushort[] { 0x24, 0x0D }, keys);
    }
}