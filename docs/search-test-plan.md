# 搜索能力测试方案

## 目标

确保当前播放列表搜索在以下场景下稳定可用：

- 用户输入包含空格
- 用户输入包含多个词
- 用户输入跨歌曲名和歌手字段
- 用户输入包含英文标点
- 多首歌曲共享同一关键词时都能被召回
- 真实歌单中的多词歌名和乐队名案例可稳定回归
- 结果排序符合预期
- 单结果和首结果在界面中始终可见

## 分层测试

### 1. 单元测试

覆盖位置：

- tests/CloudMusicPlaylistSearch.Tests/Core/PlaylistSearchEngineTests.cs

重点断言：

- 空查询时按原播放顺序返回
- 歌曲名命中优先于歌手命中
- 支持歌手字段子串匹配
- 支持由空格分隔的多词查询
- 支持跨歌曲名和歌手字段的多词查询
- 支持多余空格和常见标点归一化
- 共享关键词的多首歌曲能同时返回
- 真实案例 black label、black label society、farewell ballad 都能正确命中 Farewell Ballad - Black Label Society

### 2. 解析集成测试

覆盖位置：

- tests/CloudMusicPlaylistSearch.Tests/Infrastructure/PlaylistSnapshotLoaderTests.cs

重点断言：

- playingList JSON 可正确解析为快照
- track.id、displayOrder、歌曲名、歌手、专辑能正确提取
- SearchText 与归一化规则一致

### 3. 手工验证

在调试窗口中对真实 playingList 做这组回归检查：

1. 输入 take back，应命中 Take It Back。
2. 输入 pink hopes，应命中 High Hopes - Pink Floyd。
3. 输入 dont stop，应命中 Don't Stop。
4. 输入 stop，应同时看到多首包含 stop 的歌曲。
5. 输入前后插入多余空格，结果应保持一致。
6. 中英文混合曲名、歌手名应保持可检索。
7. 输入 black label，Farewell Ballad - Black Label Society 必须可见。
8. 输入 black label society，Farewell Ballad - Black Label Society 必须可见。
9. 输入 farewell ballad，Farewell Ballad - Black Label Society 必须可见。
10. 只有 1 条结果时，该结果不能因为默认选中态而变得不可读。

## 回归门槛

- 修改搜索逻辑前必须先补失败用例
- 修改搜索逻辑后至少运行：
  - dotnet test tests/CloudMusicPlaylistSearch.Tests/CloudMusicPlaylistSearch.Tests.csproj --filter FullyQualifiedName~PlaylistSearchEngineTests
  - dotnet test CloudMusicPlaylistSearch.slnx
- 若后续引入拼音、别名或模糊匹配，需要单独新增测试组，不覆盖现有精确/分词行为