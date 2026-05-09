using CloudMusicPlaylistSearch.Core.Models;
using CloudMusicPlaylistSearch.Core.Search;
using CloudMusicPlaylistSearch.Infrastructure.Playback;
using CloudMusicPlaylistSearch.Infrastructure.Playlist;
using System.Windows;
using System.Windows.Input;

namespace CloudMusicPlaylistSearch.App;

public partial class MainWindow : Window
{
    private readonly PlaylistSnapshotLoader _snapshotLoader = new();
    private readonly PlaylistSearchEngine _searchEngine = new();
    private readonly CloudMusicTrackActivator _trackActivator = new();
    private readonly string _playlistPath = CloudMusicPaths.PlayingListPath;

    private PlaylistSnapshot? _snapshot;
    private bool _isLoading;
    private bool _isActivatingTrack;

    public MainWindow()
    {
        InitializeComponent();
        PathValueTextBlock.Text = _playlistPath;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadPlaylistAsync();
    }

    private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ReloadPlaylistAsync();
    }

    private void SearchTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading || _snapshot is null)
        {
            return;
        }

        ApplySearch();
    }

    private void SearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_isActivatingTrack)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var track = ResultsListView.SelectedItem as PlaylistTrack
                ?? ResultsListView.Items.OfType<PlaylistTrack>().FirstOrDefault();

            if (track is not null)
            {
                _ = ActivateTrackAsync(track);
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Down || ResultsListView.Items.Count == 0)
        {
            return;
        }

        ResultsListView.Focus();
        ResultsListView.SelectedIndex = 0;
        e.Handled = true;
    }

    private void ResultsListView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsListView.SelectedItem is PlaylistTrack track)
        {
            _ = ActivateTrackAsync(track);
        }
    }

    private void ResultsListView_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (ResultsListView.SelectedItem is PlaylistTrack track)
        {
            _ = ActivateTrackAsync(track);
        }

        e.Handled = true;
    }

    private async Task ReloadPlaylistAsync()
    {
        SetLoadingState(true, "正在读取本地 playingList...");

        try
        {
            var snapshot = await Task.Run(() => _snapshotLoader.LoadFromFile(_playlistPath));
            _snapshot = snapshot;

            SourceValueTextBlock.Text = string.IsNullOrWhiteSpace(snapshot.SourceName)
                ? "当前播放列表"
                : snapshot.SourceName;

            FooterTextBlock.Text =
                "双击结果或按 Enter 会尝试切换到对应歌曲。当前版本优先依赖 CloudMusic 中已打开的播放列表面板。";

            ApplySearch();

            StatusValueTextBlock.Text =
                $"已加载 {snapshot.Tracks.Count} 首，更新时间 {snapshot.UpdatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _snapshot = null;
            ResultsListView.ItemsSource = Array.Empty<PlaylistTrack>();
            CountValueTextBlock.Text = "0";
            SourceValueTextBlock.Text = "未加载";
            StatusValueTextBlock.Text = $"加载失败：{ex.Message}";
            FooterTextBlock.Text = "请确认网易云音乐已启动，并且本地 playingList 文件存在。";
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void ApplySearch()
    {
        if (_snapshot is null)
        {
            return;
        }

        var results = _searchEngine.Search(_snapshot, SearchTextBox.Text, maxResults: 500);
        ResultsListView.ItemsSource = results;
        CountValueTextBlock.Text = $"{results.Count} / {_snapshot.Tracks.Count}";
        ResultsListView.SelectedIndex = -1;
    }

    private async Task ActivateTrackAsync(PlaylistTrack track)
    {
        if (_snapshot is null || _isLoading || _isActivatingTrack)
        {
            return;
        }

        _isActivatingTrack = true;
        ReloadButton.IsEnabled = false;
        SearchTextBox.IsEnabled = false;
        ResultsListView.IsEnabled = false;
        StatusValueTextBlock.Text = $"正在尝试切换到 {track.Name} - {track.Artist}...";

        try
        {
            var result = await _trackActivator.ActivateTrackAsync(track);
            FooterTextBlock.Text = result.Message;
            StatusValueTextBlock.Text = result.IsSuccess
                ? $"已切换到 {track.Name} - {track.Artist}"
                : $"切歌失败：{result.Message}";
        }
        catch (Exception ex)
        {
            FooterTextBlock.Text = $"切歌执行异常：{ex.Message}";
            StatusValueTextBlock.Text = "切歌失败";
        }
        finally
        {
            _isActivatingTrack = false;
            ReloadButton.IsEnabled = !_isLoading;
            SearchTextBox.IsEnabled = !_isLoading;
            ResultsListView.IsEnabled = !_isLoading;
        }
    }

    private void SetLoadingState(bool isLoading, string? statusMessage = null)
    {
        _isLoading = isLoading;
        ReloadButton.IsEnabled = !isLoading;
        SearchTextBox.IsEnabled = !isLoading;

        if (statusMessage is not null)
        {
            StatusValueTextBlock.Text = statusMessage;
        }
    }
}