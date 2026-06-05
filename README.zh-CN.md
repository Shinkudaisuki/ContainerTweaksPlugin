# ContainerTweaks

ContainerTweaks 是一个用于 **未知伤亡**（英文名：**Casualties: Unknown Demo**）的 BepInEx 插件，用于改进容器界面。

## 语言

- [English](README.md)
- [简体中文](README.zh-CN.md)

## 功能

- 默认将容器网格从 3 列扩展为 6 列。
- 为物品数量超过可见区域的容器添加鼠标滚轮滚动。
- 在扩展后的容器区域内交互时，减少容器界面意外关闭的情况。
- 使用 BepInEx 配置文件调整网格布局、滚动和拖拽阈值。

## 配置

安装插件并启动一次游戏后，BepInEx 会生成配置文件：

`BepInEx/config/com.user.containertweaks.cfg`

可用配置项：

| 分组 | 键 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Container Grid` | `ColumnCount` | `6` | 容器网格列数。 |
| `Container Grid` | `VisibleRowCount` | `5` | 需要滚动前可见的行数。 |
| `Container Grid` | `CellSpacing` | `64` | 物品格之间的像素间距。 |
| `Container Grid` | `StartX` | `34.5` | 第一个物品格的锚定 X 坐标。 |
| `Container Grid` | `StartY` | `-34.5` | 第一个物品格的锚定 Y 坐标。 |
| `Scrolling` | `ScrollStep` | `64` | 鼠标滚轮每格滚动的距离。 |
| `Scrolling` | `ScrollLerpSpeed` | `20` | 滚动平滑速度。 |
| `Dragging` | `DragDistanceThreshold` | `1000` | 拖拽距离阈值。设置为 `600` 可恢复游戏原始值。 |


## 安装

1. 为 **未知伤亡** 安装 BepInEx。
2. 构建或获取 `ContainerTweaks.dll`。
3. 将 `ContainerTweaks.dll` 复制到游戏的 BepInEx 插件目录：
   - `BepInEx/plugins/ContainerTweaks.dll`
4. 启动游戏。

## 致谢

容器滚动功能的实现参考了 [QoL-Unknown](https://github.com/jimmyking9999999/QoL-Unknown)。

由于 QoL-Unknown 当前不支持游戏的最新版本，本插件移植了其中的容器滚动功能。

感谢 QoL-Unknown 的原作者。如果原作者希望，本项目可以移除相关参考或移植代码。

## 许可证

本项目使用 GPL 许可证。