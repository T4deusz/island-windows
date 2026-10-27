# Island

**Your desktop. A little less boring.**

A macOS-inspired dynamic island for Windows. Music, files, focus and everyday tools, right at the top of your screen.

**[Download for Windows ↗](https://github.com/T4deusz/island-windows/releases/latest)** · [What's supported](Feature-Coverage-1.3.md) · [Build it yourself](#build-it-yourself)

![Island expanded](docs/images/Capture%20d'écran%202026-10-27%20153805.png)
![Island compact](docs/images/Capture%20d'écran%202026-10-27%20153810.png)

## Small pill. Lots going on.

| | What's inside |
| --- | --- |
| **Your soundtrack** | Artwork, play/pause, skipping, seeking and a visualizer driven by actual system audio. |
| **Your focus** | Pomodoro, breaks, a stopwatch and Keep Awake. |
| **Your daily stuff** | File shelf, quick notes, clipboard pins, snippets, reminders and app shortcuts. |
| **Your quick tools** | App volume mixer, camera mirror, image-to-text, calculator, search and a teleprompter. |
| **Your setup** | Spring motion, artwork-matched accents, compact volume/brightness HUDs and reorderable widgets. |
| **Your integrations** | Optional local activity API and AI usage from logs you choose. |

**17 tool pages. All inside the island.** Pop a page into its own window whenever you want.

Native C# / WPF, with 50 custom vector icons. Pick an attached notch or a floating pill and make it yours.

## Get into it

1. Grab **Island-Windows-x64.zip** from [Releases](https://github.com/T4deusz/island-windows/releases/latest).
2. Extract the whole folder and open **Island.exe**. Keep the included files together.
3. Click the pill to expand. Hit **Workspace** for your tools.

Already running an older version? Quit it from the tray first.

**Windows 10 (build 19041+) or Windows 11 · x64 · no separate .NET install needed**

| Shortcut | What it does |
| --- | --- |
| `Ctrl + Alt + Space` | Open or collapse the island |
| `Ctrl + Alt + K` | Open the command palette |
| `Ctrl + 1–9` | Switch tool pages |
| Scroll on the pill | Adjust volume |
| Swipe left/right | Switch widget pages |

## A few things to know

This is a **development build**. The portable app is unsigned, so Windows may show an unfamiliar-publisher prompt. Device and media-app support varies; some features are partial.

- Clipboard history, the local API and download monitoring are **off by default**.
- Notes and saved workspace data stay on your PC as **unencrypted local files**. The API token is encrypted for your Windows account.
- Optional weather, lyrics, search and translation use online services.
- Notification mirroring needs a packaged MSIX build and Windows permission. Secure lock-screen widgets and Apple-only integrations aren't available.

[Full feature coverage](Feature-Coverage-1.3.md) · [Technical notes & API](docs/TECHNICAL.md)

## Build it yourself

Windows + the **.NET 10 SDK**, then:

```powershell
./Build.ps1 -SelfContained
```

Output: `../Island-Windows-1.3`. Built-in checks run with `Island.exe --self-test` and `Island.exe --api-self-test`.

**156 self-checks + 9 API checks** passed for the published build. Hardware-specific behavior still needs testing on your setup.

---

Inspired by Alcove and boring.notch. Independently built for Windows. [Credits & third-party notices](THIRD_PARTY_NOTICES.md).
