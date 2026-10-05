# Win 10 Share Hotspot (为方便使用localsend而写的Win 10 共享热点小工具)

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)

[中文](#中文) | [English](#english)

***

## 中文

单文件目录：

```
Win10ShareHotspot\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Win10ShareHotspot.exe
```

或者直接点本页面右侧👉→ [Releases](#) 下载

***

# 版本更新记录

v\_0.0.1 首次发布，支持一键开关热点、自定义名称与密码、定时自动关闭、已连接设备列表、中英文与明暗主题切换

***

一个基于 .NET 8.0 Windows 窗体 (WinForms) 与 WinRT `NetworkOperatorTetheringManager` 开发的轻量化、纯绿色免安装工具。用于快速开关电脑的移动热点、设置热点名称与密码，并查看已连接的设备。支持定时自动关闭、中英文双语切换以及明暗主题。

## 📥 运行环境要求 (重要)

* **系统**：Windows 10 1809 (10.0.17763) 或更高版本。
* **硬件与网络**：电脑需带有支持"移动热点"的无线网卡，并且当前有可用的网络连接（热点需要共享一个已联网的连接）。
* **.NET 运行时**：
  * **独立版 (自带运行时)**：无需安装任何环境，体积较大，双击即用。
  * **框架依赖版**：体积仅几百 KB，但电脑必须安装 **.NET 8.0 或更高版本** 的桌面运行时 (Desktop Runtime)。如果双击程序时提示缺少环境，请前往微软官方网站下载并安装： 👉 [.NET 8.0 下载页面](https://dotnet.microsoft.com/download/dotnet/8.0) _(请选择 **Windows** 平台下的 **Download .NET Desktop Runtime**)_

***

## 🚀 主要功能

* **一键开关热点**：实时显示热点状态（已开启 / 已关闭 / 切换中 / 不可用）。
* **自定义名称与密码**：名称 1~32 个字符，密码 8~63 个字符，可切换显示密码。
* **本机热点 IP**：开启后显示本机在热点网络中的 IP。
* **定时自动关闭**：设定分钟数后开始倒计时，到时自动关闭热点；`0` 表示不自动关闭。
* **已连接设备列表**：显示设备名称、IP 地址、MAC 地址，可手动刷新。
* **系统节能开关**：控制 Windows"无设备连接时自动关闭热点"的系统设置。
* **退出时关闭热点**：可选，关闭本程序时同时关闭热点。
* **🌐 雙語切換**：右上角一键切换中/英文 (中/EN)。
* **🌓 主题切换**：右上角一键切换明/暗模式。
* **记住设置**：主题、语言、自动关闭时间、退出时关闭热点、节能偏好保存在当前用户注册表 `HKEY_CURRENT_USER\Software\Win10ShareHotspot`。

***

## 📖 使用方法

1. 运行 `Win10ShareHotspot.exe`。
2. 填写热点名称和密码，点击 **应用名称与密码**。
3. 点击 **开启热点**。
4. 需要时在 **自动关闭(分钟)** 中设定时长。
5. 在 **已连接设备** 列表中查看接入的设备。

> 提示：设备的 IP 地址由系统 ARP 表补全，刚连接的设备可能需要稍等片刻，再点一次 **刷新** 才会显示 IP。

***

## 🛠️ 开发与编译环境

如果您需要自行修改源代码并重新打包，请参考以下环境配置：

* **开发语言**：C# (LangVersion: latest)
* **目标框架**：`net8.0-windows10.0.19041.0`
* **发布命令 (独立单文件，自带运行时)**：
  ```bash
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
  ```
* **发布命令 (框架依赖单文件)**：
  ```bash
  dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none
  ```

> 不建议开启裁剪 (Trim)，WinForms 与 WinRT 投影在裁剪后可能在运行时出错。

***

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

***

## 📈 Star History

[![Star History Chart](https://api.star-history.com/svg?repos=<GitHub用户名>/<仓库名>&type=Date)](https://star-history.com/#<GitHub用户名>/<仓库名>&Date)

---

## English

Output path:

```
Win10ShareHotspot\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Win10ShareHotspot.exe
```

Or download from [Releases](#) on the right side of this page.

***

# Changelog

v\_0.0.1 First release: one-click hotspot toggle, custom SSID and password, auto-off timer, connected devices list, Chinese/English UI and light/dark theme

***

A lightweight, portable, install-free tool built with .NET 8.0 WinForms and the WinRT `NetworkOperatorTetheringManager` API. Quickly turn the mobile hotspot on or off, set its SSID and password, and see who is connected. Supports an auto-off timer, Chinese/English UI and light/dark themes.

## 📥 Requirements

* **OS**: Windows 10 version 1809 (10.0.17763) or later.
* **Hardware & network**: a wireless adapter that supports Mobile Hotspot, and an active internet connection to share.
* **.NET runtime**:
  * **Self-contained build**: nothing to install, larger file, just double-click.
  * **Framework-dependent build**: only a few hundred KB, but requires the **.NET 8.0 (or later) Desktop Runtime**: 👉 [.NET 8.0 download page](https://dotnet.microsoft.com/download/dotnet/8.0) _(choose **Download .NET Desktop Runtime** under **Windows**)_

***

## 🚀 Features

* **One-click hotspot toggle** with live status (ON / OFF / switching / unavailable).
* **Custom SSID and password**: SSID 1–32 characters, password 8–63 characters, with a show/hide option.
* **Hotspot host IP** display.
* **Auto-off timer**: set a number of minutes and the hotspot turns off when the countdown ends; `0` means never.
* **Connected devices list** with device name, IP address and MAC address, plus manual refresh.
* **System power-saving toggle**: controls Windows' "turn off hotspot when no device is connected" setting.
* **Turn off hotspot on exit** (optional).
* **🌐 Language switch**: Chinese / English from the top-right button.
* **🌓 Theme switch**: light / dark from the top-right button.
* **Remembers settings**: theme, language, auto-off minutes, turn-off-on-exit and power-saving preference are stored per user under `HKEY_CURRENT_USER\Software\Win10ShareHotspot`.

***

## 📖 Usage

1. Run `Win10ShareHotspot.exe`.
2. Enter an SSID and password, then click **Apply SSID & password**.
3. Click **Turn ON**.
4. Optionally set **Auto-off (min)**.
5. Check the **Connected devices** list to see who is connected.

> Note: IP addresses are resolved from the system ARP table, so a device that just connected may need a moment and another click on **Refresh** before its IP appears.

***

## 🛠️ Build from source

* **Language**: C# (LangVersion: latest)
* **Target framework**: `net8.0-windows10.0.19041.0`
* **Publish (self-contained single file)**:
  ```bash
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
  ```
* **Publish (framework-dependent single file)**:
  ```bash
  dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none
  ```

> Trimming is not recommended: WinForms plus the WinRT projection can fail at runtime when trimmed.

***

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
