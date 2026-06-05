# ContainerTweaks

ContainerTweaks is a BepInEx plugin for **Casualties: Unknown Demo** that improves the container UI.

## Languages

- [English](README.md)
- [简体中文](README.zh-CN.md)

## Features

- Expands the container grid from 3 columns to 6 columns by default.
- Adds mouse wheel scrolling for containers with more items than the visible area.
- Keeps the expanded container area from closing unexpectedly while interacting with it.
- Provides BepInEx configuration entries for grid layout, scrolling, and drag threshold values.

## Configuration

After the game starts once with the plugin installed, BepInEx creates the configuration file:

`BepInEx/config/com.user.containertweaks.cfg`

Available options:

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| `Container Grid` | `ColumnCount` | `6` | Number of columns in the container grid. |
| `Container Grid` | `VisibleRowCount` | `5` | Number of visible rows before scrolling is needed. |
| `Container Grid` | `CellSpacing` | `64` | Pixel spacing between item cells. |
| `Container Grid` | `StartX` | `34.5` | Anchored X position of the first item cell. |
| `Container Grid` | `StartY` | `-34.5` | Anchored Y position of the first item cell. |
| `Scrolling` | `ScrollStep` | `64` | Scroll distance per mouse wheel notch. |
| `Scrolling` | `ScrollLerpSpeed` | `20` | Scroll smoothing speed. |
| `Dragging` | `DragDistanceThreshold` | `1000` | Drag distance threshold. Set to `600` to restore the original game value. |


## Installation

1. Install BepInEx for **Casualties: Unknown Demo**.
2. Build or obtain `ContainerTweaks.dll`.
3. Copy `ContainerTweaks.dll` to the game's BepInEx plugins folder:
   - `BepInEx/plugins/ContainerTweaks.dll`
4. Start the game.

## Credits

The container scrolling implementation references code from [QoL-Unknown](https://github.com/jimmyking9999999/QoL-Unknown).

Because QoL-Unknown does not support the latest current version of the game, the container scrolling feature was ported into this plugin.

Thanks to the original author of QoL-Unknown. If the original author prefers, the related referenced or ported code can be removed from this project.

## License

This project is licensed under the GPL license.