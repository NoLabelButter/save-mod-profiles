# 存档模组管理器 / Save Mod Profiles

[简体中文](#简体中文) | [English](#english)

## 简体中文

### 项目简介

存档模组管理器为每个存档保存独立的模组清单，并支持新游戏预设、模组搜索与排序、最近切换记录、难度颜色和上次游玩存档入口。模组仍由游戏官方模组管理器安装、授权和全局启用；本 MOD 在进入存档前更新官方模组清单，不会在运行中卸载或加载程序集。

这是一个面向 **Probably Stolen** 的独立社区 MOD，与游戏开发者及发行方无隶属或背书关系。

### 功能

- 分别保存每个存档的模组选择，也可在新游戏专精页面设置新存档清单。
- 保存、载入、更新和删除玩家预设。
- 搜索模组名称、ID、作者和清单简介；使用 `@[作者关键词]` 按作者筛选。
- 查看未激活模组，并通过游戏自己的授权流程启用。
- 查看最近五次模组清单切换详情，并为存档名称显示难度颜色。
- 标记最近游玩的存档，便于从载入页面进入。

### 隐私与安全

- 配置保存在游戏用户数据目录的 `SaveModProfiles.json` 中，包括存档模组清单、预设和切换记录；不上传这些内容。
- 静态检查未发现网络请求、凭据读取、原生库调用或创建外部进程的代码；项目不包含遥测功能。
- 清单声明 `files`（读写本 MOD 的配置数据）和 `reflection-external`（与游戏内部模组授权、存档及界面 API 交互）。启用未激活 MOD 时仍由游戏显示官方授权提示。
- 修改模组清单后需要退出游戏，再由玩家手动重新启动游戏使其生效。

以上是对本仓库当前源码的静态检查结论，不代表对游戏、游戏本身或其他 MOD 的安全保证。

### 构建

构建需要本机游戏目录中的 `Probably Stolen_Data/Managed` 程序集。Windows PowerShell 示例：

```powershell
dotnet build .\SaveModProfiles.csproj -c Release -p:GameDir='C:\Path\To\Probably Stolen'
```

将生成的 `SaveModProfiles.dll` 与仓库根目录的 `manifest.xml` 放入游戏 `Mods` 下的 `存档模组管理器 Save Mod Profiles` 文件夹。游戏程序集不随仓库分发。

### 许可

本项目源码采用 **PolyForm Noncommercial License 1.0.0**。允许非商业用途的使用、修改和再分发；商业用途须事先取得著作权方另行许可。完整条款见 [`LICENSE`](LICENSE)，中文说明见 [`LICENSE.zh-CN.md`](LICENSE.zh-CN.md)。

该许可仅适用于本项目原创源码，不授予游戏、游戏素材、商标或第三方依赖的权利。

## English

### About

Save Mod Profiles keeps a separate mod list for each save. It also supports new-game presets, mod search and sorting, recent change history, difficulty colors, and a shortcut to the most recently played save. Mods are still installed, approved, and globally enabled through the game's official mod manager. This mod updates the official mod list before a save is entered; it does not unload or load assemblies while the game is running.

This is an independent community mod for **Probably Stolen**. It is not affiliated with or endorsed by the game's developers or publisher.

### Features

- Keep independent mod selections for each save and configure a new save from the specialization screen.
- Save, load, update, and delete player presets.
- Search mod names, IDs, authors, and manifest descriptions; use `@[author keyword]` to filter by author.
- Show inactive mods and enable them through the game's own authorization flow.
- Review the five most recent mod-list changes and show difficulty colors on save names.
- Mark the most recently played save for quick access from the load screen.

### Privacy and security

- Settings are stored in `SaveModProfiles.json` in the game's user-data directory. They include save mod lists, presets, and change history; this mod does not upload them.
- Static review found no network requests, credential access, native-library calls, or external-process creation in this repository. The project contains no telemetry.
- The manifest declares `files` (read/write this mod's configuration data) and `reflection-external` (interact with internal game APIs for mod authorization, saves, and UI). The game continues to show its official authorization prompt when an inactive mod is enabled.
- After changing a mod list, quit the game and restart it manually to apply the change.

These are static-review findings for the current repository, not a security guarantee for the game or other mods.

### Build

Building requires the game assemblies in the local `Probably Stolen_Data/Managed` directory. Example for Windows PowerShell:

```powershell
dotnet build .\SaveModProfiles.csproj -c Release -p:GameDir='C:\Path\To\Probably Stolen'
```

Place the resulting `SaveModProfiles.dll` and the root `manifest.xml` in `Mods\存档模组管理器 Save Mod Profiles`. Game assemblies are not distributed in this repository.

### License

The source is licensed under the **PolyForm Noncommercial License 1.0.0**. Noncommercial use, modification, and redistribution are permitted; commercial use requires separate prior permission from the copyright holder. See [`LICENSE`](LICENSE) for the license terms and [`LICENSE.zh-CN.md`](LICENSE.zh-CN.md) for a Chinese explanation.

The license applies only to original source code in this project. It does not grant rights to the game, its assets or trademarks, or third-party dependencies.
