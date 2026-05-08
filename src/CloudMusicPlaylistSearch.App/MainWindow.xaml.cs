using CloudMusicPlaylistSearch.Core.Models;
using CloudMusicPlaylistSearch.Core.Search;
using CloudMusicPlaylistSearch.Infrastructure.Playlist;
using System.Windows;
using System.Windows.Input;

namespace CloudMusicPlaylistSearch.App;

public partial class MainWindow : Window
{
    private readonly PlaylistSnapshotLoader _snapshotLoader = new();
    private readonly PlaylistSearchEngine _searchEngine = new();
    private readonly string _playlistPath = CloudMusicPaths.PlayingListPath;

    private PlaylistSnapshot? _snapshot;
    private bool _isLoading;

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
        PreviewTrackActivation();
    }

    private void ResultsListView_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        PreviewTrackActivation();
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
                "本轮原型已打通本地文件加载和内存搜索。覆盖层附着与歌曲激活将在后续阶段接入。";

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

    private void PreviewTrackActivation()
    {
        if (ResultsListView.SelectedItem is not PlaylistTrack track)
        {
            return;
        }

        FooterTextBlock.Text =
            $"已选中 {track.Name} - {track.Artist}。实际切歌执行链将在下一阶段接入。";
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