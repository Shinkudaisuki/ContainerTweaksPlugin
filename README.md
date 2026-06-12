# ContainerTweaks

ContainerTweaks is a BepInEx plugin for **Casualties: Unknown Demo** that improves the container UI.

## Languages

- [English](README.md)
- [简体中文](README.zh-CN.md)

## Features

- Expands the container grid from 3 columns to 6 columns by default.
- Adds mouse wheel scrolling for containers with more items than the visible area.
- Keeps the expanded container area from closing unexpectedly while interacting with it.
- Adds a configurable quick-transfer hotkey (default: <kbd>LeftControl</kbd>) for moving matching items between containers.
- Supports quick liquid transfer between compatible water containers.
- Supports quick magazine-to-magazine transfers for the same ammo type.
- Supports quick shotgun round loading and shotgun box loading.
- Supports quick loading rounds into a magazine.
- Provides BepInEx configuration entries for grid layout, scrolling, drag threshold values, and enabling / disabling specific features.
- Supports multiplayer mod [Casualties Together](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop).

## Configuration

After the game starts once with the plugin installed, BepInEx creates the configuration file:

`BepInEx/config/com.user.containertweaks.cfg`

Available options:

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| `Feature Toggles` | `ContainerViewExpansionEnabled` | `true` | Enable expanded container grid layout. |
| `Feature Toggles` | `ContainerScrollingEnabled` | `true` | Enable mouse wheel scrolling in container views. |
| `Feature Toggles` | `QuickTransferEnabled` | `true` | Enable all quick transfer features. |
| `Container Grid` | `ColumnCount` | `6` | Number of columns in the container grid. |
| `Container Grid` | `VisibleRowCount` | `5` | Number of visible rows before scrolling is needed. |
| `Container Grid` | `CellSpacing` | `64` | Pixel spacing between item cells. |
| `Container Grid` | `StartX` | `34.5` | Anchored X position of the first item cell. |
| `Container Grid` | `StartY` | `-34.5` | Anchored Y position of the first item cell. |
| `Scrolling` | `ScrollStep` | `64` | Scroll distance per mouse wheel notch. |
| `Scrolling` | `ScrollLerpSpeed` | `20` | Scroll smoothing speed. |
| `Dragging` | `DragDistanceThreshold` | `1000` | Drag distance threshold of closing inventory view. Set to `600` to restore the original game value. |
| `Quick Transfer` | `QuickTransferKey` | `LeftControl` | Hold this key while dropping an item onto a target container to quickly transfer matching items or compatible liquids. |
| `Quick Transfer` | `ItemTransferEnabled` | `true` | Enable moving matching items into compatible target containers. |
| `Quick Transfer` | `LiquidTransferEnabled` | `true` | Enable transferring liquid between compatible liquid containers. |
| `Quick Transfer` | `MagazineTransferEnabled` | `true` | Enable moving rounds between magazines of the same ammo type. |
| `Quick Transfer` | `ShotgunRoundLoadEnabled` | `true` | Enable loading shotgun rounds from the source container into a shotgun. |
| `Quick Transfer` | `ShotgunBoxLoadEnabled` | `true` | Enable loading shotgun shells from shotgun ammo boxes into a shotgun. |
| `Quick Transfer` | `MagazineQuickLoadEnabled` | `true` | Enable quick loading matching loose rounds into magazines. |

## Quick Transfer

Hold the configured quick-transfer key while dragging an item onto another container-like item.

- For normal items, matching items with the same full name are moved from the source container to the target container.
- For water containers, **themselves** with same liquid types in the source container are transferred to the target **item** container.
- For water containers, **liquids** are transferred when the target **water** container is empty or has the same liquid types.
- For ammo, all rounds of the same type are loaded into the target magazine (including a 12gauge box).
- For magazines (including 12gauge boxes), rounds are transferred between matching magazines of the same ammo type.
- For shotgun ammo, all shotgun ammo in the source container/12gauge box is loaded into the target shotgun.
- The target container must be able to hold the dragged item.

## Installation

1. Install BepInEx for **Casualties: Unknown Demo**.
2. Build or obtain `ContainerTweaks.dll`.
3. Copy `ContainerTweaks.dll` to the game's BepInEx plugins folder:
   - `BepInEx/plugins/ContainerTweaks.dll`
4. Start the game.

## Know issues

- The background and volumn bar of the container view won't scale with the number of columns. Because I can't figure out how they are drawn.
- The simi-transparent strip indicates closing inventory view won't scale for the same reasion above. But since the drop feature of the base game still works, I'd like to call this a feature ;) .

## Credits

The container scrolling implementation references code from [QoL-Unknown](https://github.com/jimmyking9999999/QoL-Unknown).

Because QoL-Unknown does not support the latest current version (v7.1) of the game by the time this plugin was created, the container scrolling feature was ported into this plugin.

Thanks to the original author of QoL-Unknown. If the original author prefers, the related referenced or ported code can be removed from this project.

## Changelog

### v1.3.0

- Added support for multiplayer mod [Casualties Together](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop).
- Added feature toggles in the configuration file.
- Restructure whole project.

### v1.2.0

- Added quick magazine-to-magazine transfers for matching ammo types.
- Added quick shotgun round and shotgun box loading support.
- Added bulk quick-loading for matching rounds when loading ammo into a magazine.

### v1.1.0

- Added configurable quick-transfer hotkey, defaulting to `LeftControl`.
- Added batch transfer for matching items between containers.
- Added support for transferring compatible liquids between water containers.

### v1.0.0

- Expanded the container grid.
- Added container scrolling.

## License

This project is licensed under the GPL license.