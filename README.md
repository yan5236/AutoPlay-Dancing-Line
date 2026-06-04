# AutoPlay

基于 BepInEx 的《跳舞的线》(Dancing Line) **Steam 版**自动播放插件。通过读取游戏内置的点击时间表，在关卡中自动触发转向，实现自动通关演示。

## 安装

1. 确保已安装 [BepInEx](https://github.com/BepInEx/BepInEx)（IL2CPP 版本）
2. 将 `AutoPlay.dll` 放入 `BepInEx\plugins\` 目录
3. 启动游戏

## 如何启用 AutoPlay

在游戏中按下 **F8** 键即可切换自动播放的开启/关闭状态。屏幕右上角会显示当前状态：

- `AutoPlay: ON` — 自动播放已启用，进入关卡后会自动执行转向
- `AutoPlay: OFF` — 自动播放已关闭

进入关卡后，插件会自动读取该关卡的点击时间表并驱动角色转向。切换关卡时，插件也会自动切换对应的点击数据。

## 已知限制

### 存档与成绩保护

插件在自动播放开启时会**尽力拦截**存档写入、成就上报和 Steam 统计数据，但由于游戏内部存档路径较多，**无法保证 100% 拦截所有存档写入**。部分通过非标准路径写入的进度数据可能仍会被持久化，建议在使用 AutoPlay 前备份存档。

### 部分关卡无法正常通关

自动播放依赖游戏内置的点击时间表数据。部分关卡可能因为**关卡数据缺失**（点击时间表为空或不完整）导致无法启动自动播放，表现为角色在关卡中不进行任何转向。

### 帧率与点击次数累积误差

自动播放的点击时序基于关卡时间（`CurrentTime`）驱动，但实际运行中存在以下偏差来源：

- **帧率波动**：低帧率或帧时间不稳定会导致预测步长（`GetPredictedStep`）与实际关卡推进不同步，转向可能比预期提前或延迟触发
- **累积误差**：随着关卡进行，单次微小的时序偏差会逐步累积，导致后续转向点与路线逐渐偏离
- **物理浮点精度**：长时间的连续运行会放大浮点运算的舍入误差

以上因素叠加后，自动播放的路线可能随时间推移越来越偏离正确路径，最终表现为**撞墙或掉下路线**。该问题目前无法从根本上解决，因为它是运行时环境（帧率、硬件性能）与确定性的点击时间表之间的固有矛盾。

如果您能解决以上任何限制，欢迎提交 PR。

## 技术架构

| 文件 | 职责 |
|------|------|
| `Plugin.cs` | BepInEx 插件入口，管理开关状态与 UI 文本 |
| `AutoPlayRuntime.cs` | Unity MonoBehaviour，监听 F8 热键并兜底驱动 |
| `AutoPlayDriver.cs` | 核心执行器，读取点击时间表并按时序触发转向 |
| `AutoPlayPatches.cs` | Harmony 补丁，挂钩关卡生命周期与成就系统 |
| `AutoPlayProgressGuard.cs` | 拦截 `UserDataManager.Save` 等多种存档写入 |
| `AutoPlayLevelDataGuard.cs` | 拦截 `LevelData` 的通关/完美/收集记录写入 |
| `AutoPlaySteamGuard.cs` | 拦截 Steam 成就解锁与统计提交 |
| `AutoPlaySessionState.cs` | 管理当前 AutoPlay 保护关卡状态 |
| `AutoPlayOverlay.cs` | 右上角状态文本 Canvas |

## 依赖

- .NET 6.0
- [BepInEx](https://github.com/BepInEx/BepInEx) (Unity IL2CPP)
- [Harmony](https://github.com/pardeike/Harmony)
- Dancing Line (Steam)
