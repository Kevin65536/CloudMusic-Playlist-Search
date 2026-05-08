# CloudMusic Playlist Search 架构设计

## 1. 目标

为网易云音乐 Windows 客户端增加“仅针对当前播放列表”的本地搜索能力，形态为外部覆盖层：

- 网易云启动后自动附着
- 仅在播放列表面板展开时显示
- 支持按歌曲名、歌手搜索
- 支持键盘上下选择结果
- 支持回车切换到目标歌曲
- 常驻资源尽量低，不修改网易云安装文件，不向客户端注入代码

## 2. 已验证事实

### 2.1 播放列表数据源

- 当前播放列表文件路径：C:/Users/qdsxh/AppData/Local/NetEase/CloudMusic/webdata/file/playingList
- 文件是单个大 JSON
- 顶层关键字段：list
- 每个条目的关键字段：
  - track.id
  - track.name
  - track.artists[].name
  - track.album.name
  - fromInfo.sourceData.name

这意味着“当前播放列表检索”不需要逆向客户端内部内存，只需监听本地文件变化并维护一份内存索引。

### 2.2 客户端锚点

- 进程名：cloudmusic.exe
- 主窗口类名：OrpheusBrowserHost
- 主窗口可稳定拿到句柄和标题

这意味着覆盖层可以通过 Win32 窗口附着，而不需要侵入式集成。

### 2.3 UI Automation 可见性边界

- UIA 能识别到 Chromium/CEF 外壳窗口
- 目前未验证到可直接操作的播放列表项节点

结论：

- UIA 可以作为一层能力
- 但首版架构不能单押 UIA
- 播放列表展开判断与结果触发都必须设计回退策略

## 3. 已确定的设计决策

- 技术栈：C# / .NET 8
- 形态：单文件桌面程序，登录后后台轻驻，检测到网易云后附着
- 接入方式：纯外部覆盖层，可使用 UI Automation，但不注入客户端
- 首版搜索范围：歌曲名 + 歌手
- 首版结果动作：回车切换到目标歌曲

## 4. 总体方案

采用“单进程 AppHost + 事件驱动监控 + 惰性创建覆盖层 + 多策略探测/执行器”的结构。

### 4.1 逻辑分层

1. AppHost
2. CloudMusicSession
3. PlaylistIndex
4. OverlayShell
5. InteractionAdapters

### 4.2 数据流

1. AppHost 启动后保持单实例并进入后台消息循环
2. CloudMusicSession 监听 cloudmusic.exe 进程和主窗口句柄
3. PlaylistIndex 监听 playingList 文件变化并增量刷新内存索引
4. PanelDetector 判断播放列表面板是否处于展开状态
5. 满足“网易云存活 + 主窗口可见 + 面板展开”时，OverlayShell 显示搜索框和结果列表
6. 用户输入后，SearchEngine 在内存中完成检索，不触碰磁盘
7. 用户回车后，由 ActionExecutor 选择最优适配器触发目标歌曲

## 5. 模块设计

## 5.1 AppHost

职责：

- 单实例互斥锁
- 开机自启动注册
- 后台消息循环
- 全局异常日志
- 服务编排和生命周期管理

设计要求：

- 默认无主窗口
- 仅在调试模式显示诊断面板
- 不创建托盘图标，除非后续需要手动控制入口

## 5.2 CloudMusicSession

职责：

- 检测 cloudmusic.exe 是否存在
- 获取主窗口句柄、矩形、DPI、最小化状态
- 跟随窗口移动、缩放、切换前后台
- 驱动覆盖层显示/隐藏

建议实现：

- 主路径：WinEventHook 监听窗口位置和可见性变化
- 回退路径：低频轮询，每 1000 ms 检查一次主窗口句柄

原因：

- 纯轮询最简单，但长期成本高
- WinEventHook 足够轻，适合常驻工具

## 5.3 PlaylistRepository

职责：

- 使用 FileSystemWatcher 监听 playingList
- 对文件变化做防抖
- 读取 JSON 并解析为内部模型
- 计算内容哈希，避免重复重建索引

内部模型建议：

```csharp
public sealed record PlaylistSnapshot(
    DateTimeOffset UpdatedAt,
    string SourceName,
    IReadOnlyList<PlaylistTrack> Tracks,
    string ContentHash);

public sealed record PlaylistTrack(
    long TrackId,
    int DisplayIndex,
    string Name,
    string Artist,
    string Album,
    string SearchText);
```

设计原则：

- 只保留当前快照，不做历史备份
- 只维护当前播放列表，不扩展到歌单库
- SearchText 在索引构建阶段一次性归一化，查询阶段不重复分词

## 5.4 SearchEngine

职责：

- 对当前快照做内存检索
- 支持歌曲名、歌手名模糊匹配
- 返回稳定排序结果

建议规则：

- 先精确前缀，再子串匹配
- 名称命中优先于歌手命中
- 原播放顺序作为最终稳定排序键

首版不做：

- 拼音搜索
- 模糊纠错
- 跨播放列表搜索

## 5.5 OverlayShell

职责：

- 显示搜索输入框和虚拟化结果列表
- 跟随网易云主窗口定位
- 在非需要状态时完全隐藏
- 不抢焦点，只有用户点击或快捷键激活时才接管输入

UI 建议：

- WPF 无边框透明窗口
- 始终位于网易云窗口之上，但不全局 TopMost
- 宽度与右侧播放列表抽屉对齐
- 输入框位于抽屉顶部，结果列表覆盖抽屉内容区

关键点：

- 覆盖层不是整窗蒙层，只覆盖右侧抽屉区域
- 不显示时窗口应切换为隐藏或点击穿透状态
- 结果列表必须虚拟化，避免大列表时产生多余控件开销

## 5.6 PanelDetector

职责：

- 判断“当前是否已展开播放列表面板”
- 输出布尔值和置信度

采用策略接口：

```csharp
public interface IPanelDetector
{
    PanelDetectionResult Detect(CloudMusicWindow window);
}
```

首版采用三层策略：

1. UiaPanelDetector
2. VisualProbePanelDetector
3. ManualOverrideDetector

各层说明：

- UiaPanelDetector：尝试从自动化树中识别“播放列表”相关节点；当前不可靠，但保留接口
- VisualProbePanelDetector：默认主路径，只截取右侧极小 ROI，判断抽屉背景、标题区、分隔线等视觉特征
- ManualOverrideDetector：当自动检测置信度不足时，允许热键强制显示，用于调试和兜底

资源策略：

- 仅在网易云主窗口可见且位于前台时做高频探测
- 其余时间降为低频或停探测
- ROI 截图尺寸控制在最小必要范围内

## 5.7 ActionExecutor

职责：

- 将“结果项”转换成“客户端中的可执行动作”
- 完成聚焦、定位、触发播放

也采用策略接口：

```csharp
public interface IActionExecutor
{
    Task<bool> ActivateTrackAsync(PlaylistTrack track, CancellationToken cancellationToken);
}
```

首版执行链建议：

1. UiaActionExecutor
2. KeyboardNavigationExecutor
3. CoordinateClickExecutor

说明：

- UiaActionExecutor：若后续发现可操作节点，优先使用
- KeyboardNavigationExecutor：优先尝试聚焦播放列表后发送 Home、方向键、Enter
- CoordinateClickExecutor：如果键盘导航不可靠，则根据列表几何信息与当前滚动状态执行点击/滚轮

注意：

- 第二层和第三层都需要真实客户端验证
- 因此这部分是首版唯一高风险模块

## 6. 资源占用策略

为了尽可能少占系统资源，整体实现遵循以下约束：

- 单进程，不拆常驻服务和 UI 进程
- 不使用 WebView，不引入浏览器内核
- 文件监听采用 FileSystemWatcher，而不是轮询读取大文件
- 搜索完全基于内存快照，不做磁盘查询
- 覆盖层窗口惰性创建，隐藏时不渲染动画
- 仅在网易云窗口前台且可见时做面板探测
- 所有日志默认写滚动文件，发布版关闭详细调试日志

预期常驻成本目标：

- 空闲时：接近“文件监听 + 少量窗口事件订阅”的水平
- 活跃搜索时：主要成本来自 WPF 渲染和小范围视觉探测

## 7. 推荐项目结构

```text
CloudMusic Playlist Search/
  docs/
    architecture.md
  src/
    CloudMusicPlaylistSearch.App/
      App.xaml
      App.xaml.cs
      Bootstrap/
      Overlay/
      Interop/
      Diagnostics/
    CloudMusicPlaylistSearch.Core/
      Models/
      Search/
      Contracts/
    CloudMusicPlaylistSearch.Infrastructure/
      Playlist/
      CloudMusic/
      Detection/
      Execution/
      Logging/
  tests/
    CloudMusicPlaylistSearch.Tests/
```

分层原则：

- Core 不依赖 WPF 和 Win32
- Infrastructure 负责文件系统、UIA、截图、输入模拟等外部能力
- App 只做装配、生命周期和界面

## 8. 首版里程碑

### M1 基础附着

- 单实例
- 后台启动
- 检测 CloudMusic 进程
- 跟随主窗口移动/缩放
- 读取并解析 playingList

验收标准：

- 网易云启动后 2 秒内完成附着
- 播放列表文件变化后 500 ms 内刷新内存快照

### M2 可搜索覆盖层

- Overlay 显示/隐藏
- 搜索框输入
- 结果列表虚拟化
- 歌曲名、歌手名检索

验收标准：

- 1000 首级别列表中输入搜索基本无卡顿

### M3 面板检测

- UIA 探测接口接入
- VisualProbe 默认实现
- 热键兜底

验收标准：

- 对“播放列表已展开”的识别可稳定驱动覆盖层显示

### M4 结果触发

- 至少打通一种可稳定的目标歌曲激活路径
- 回车后切换到目标歌曲

验收标准：

- 常见窗口尺寸下成功率可接受

## 9. 当前风险与后续需要验证的点

1. 网易云当前版本的 UIA 暴露很有限，不能假设能直接拿到播放列表项。
2. “回车切换到目标歌曲”很可能需要键盘导航或几何点击回退，这部分必须实机验证。
3. 抽屉展开状态的视觉探测要兼顾不同窗口尺寸、DPI 和皮肤主题。
4. 如果客户端对列表进行了虚拟滚动，CoordinateClickExecutor 需要额外维护滚动同步逻辑。

## 10. 下一轮设计细化建议

下一轮建议优先收敛这三件事：

1. PanelDetector 的视觉探测方案和 ROI 选点
2. ActionExecutor 的第一条稳定执行链
3. WPF 覆盖层的坐标系、焦点策略和输入穿透规则