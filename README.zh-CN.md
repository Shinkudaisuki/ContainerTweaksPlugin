# ContainerTweaks

ContainerTweaks 是一个用于 **未知伤亡**（英文名：**Casualties: Unknown Demo**）的 BepInEx 插件，用于改进容器界面。

## 语言

- [English](README.md)
- [简体中文](README.zh-CN.md)

## 功能

- 默认将容器网格从 3 列扩展为 6 列。
- 为物品数量超过可见区域的容器添加鼠标滚轮滚动。
- 在扩展后的容器区域内交互时，减少容器界面意外关闭的情况。
- 添加可配置的快速转移快捷键（默认左<kbd>Ctrl</kbd>），用于在容器之间移动同类物品。
- 支持在兼容的水容器之间快速转移液体。
- 支持在相同弹药类型的弹匣之间快速转移子弹。
- 支持快速装填霰弹枪子弹。
- 支持向弹匣快速装填子弹。
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
| `Quick Transfer` | `QuickTransferKey` | `LeftControl` | 按住该按键并将物品拖放到目标容器上，可快速转移匹配物品或兼容液体。 |

## 快速转移

按住配置的快速转移按键，将物品拖放到另一个容器类物品上即可触发。

- 普通物品：将所在容器中名称相同的匹配物品移动到目标容器。
- 液体容器：将所在容器中所有含有相同类型液体的液体容器移动到目标**物品容器**。
- 液体容器：当目标**液体容器**为空或液体种类完全相同时，可转移液体。
- 子弹：将所在容器的所有同种子弹装填到目标弹匣。
- 弹匣（包括12号霰弹盒）：在相同弹药类型的弹匣之间转移子弹。
- 霰弹枪子弹：将所在容器（包括12号霰弹盒）的所有子弹装填进目标霰弹枪。
- 目标容器必须能够容纳被拖拽的物品。

## 更新日志

### v1.2.0

- 新增相同弹药类型弹匣之间的快速转移。
- 新增霰弹枪子弹与霰弹枪弹盒的快速装填支持。
- 新增在装填子弹到弹匣时自动批量装填同类子弹。

### v1.1.0

- 新增可配置快速转移快捷键，默认值为 `LeftControl`。
- 新增容器之间匹配物品的批量转移。
- 新增兼容水容器之间的液体转移支持。

### v1.0.0

- 扩展容器网格
- 添加容器滚动功能

## 安装

1. 为 **未知伤亡** 安装 BepInEx。
2. 构建或获取 `ContainerTweaks.dll`。
3. 将 `ContainerTweaks.dll` 复制到游戏的 BepInEx 插件目录：
   - `BepInEx/plugins/ContainerTweaks.dll`
4. 启动游戏。

## 致谢

容器滚动功能的实现参考了 [QoL-Unknown](https://github.com/jimmyking9999999/QoL-Unknown)。

由于 QoL-Unknown 在本插件创建时尚不支持游戏的最新版本 v7.1，本插件移植了其中的容器滚动功能。

感谢 QoL-Unknown 的原作者。如果原作者希望，本项目可以移除相关参考或移植代码。

## 许可证

本项目使用 GPL 许可证。