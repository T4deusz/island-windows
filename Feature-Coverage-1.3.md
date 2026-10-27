# Island 1.3 feature coverage

All 74 items from the pasted request are listed. This release does not implement every item. Partial and unavailable capabilities are explicit. Hardware/provider validation is still required. Tools open directly inside the island; detaching is optional.

| Requested | Implementation |
| --- | --- |
| Face Unlock | Windows Hello setup link; Windows manages actual secure face unlock. No password storage or lock-screen bypass. |
| Now Playing | Implemented: event-driven play/pause, artwork, seeking, capability-gated shuffle/repeat/speed. Per-app volume in Mixer; no Apple Music favorite integration. |
| AI Usage Tracker | Partial: explicitly chosen Codex/Claude/normalized Cursor/Copilot logs; reported tokens/cost/limits, provider ordering/hiding. No automatic discovery, API polling or limit alerts. |
| Sound Mixer | Partial: real per-app Windows volume/mute. Three-band EQ needs a processing driver and is not implemented. |
| Search & Translate | Partial: nine web engines, browser translation on click, arithmetic/history. No live translation, custom engine editor or video downloader. |
| Command Palette | Implemented inside the island: Ctrl+Alt+K tool search, quick actions, calculations and snippet copying. Ctrl+1-9 switches tool pages. |
| Knock to Control | Not implemented; chassis and trackpad knock sensing require hardware-specific integration. |
| Menu Bar Detach | Windows equivalent: tools open directly in the island by default; optionally detach a page into a resizable native window. Tray and Keep Awake controls remain. |
| Clipboard History | Implemented for text: opt-in, searchable in-memory history and persistent pins. Source exclusion flags respected where provided. |
| Zoom Teleprompter | Windows equivalent: detachable local script scroller with adjustable speed, positioned by the user below the webcam. |
| System HUDs | Partial: animated volume/brightness HUDs and standard volume-key interception. Brightness/vendor flyouts may remain. |
| AirPods Manager | Partial: Windows Bluetooth connection/battery data where exposed. No nearby-case discovery, per-bud protocol or one-click pairing. |
| Pomodoro Timer | Partial: focus/break chaining, pause/resume, stopwatch and seven-day focus-minute history. No mascot or streak visualization. |
| Menu Bar Calendar | Partial: existing local agenda/month calendar; no cloud calendar synchronization. |
| Camera Mirror | Implemented: explicit-start native local mirrored camera feed, stopped when leaving the page. Requires hardware and Windows permission; hardware untested. |
| Hide the Notch | Windows equivalent: tray hide/show for the virtual island. No macOS menu bar to mask. |
| 100% Native | Windows equivalent: native C#/WPF desktop UI without Electron or a browser runtime. |
| Liquid Glass UI | Visual adaptation: attached notch, dark translucent workspace, rounded panels and traffic-light controls. Not Apple's material engine; whole-app scale only. |
| Quick Notes | Implemented: local notes with debounced automatic persistence. |
| File Shelf | Implemented with limits: drag onto island, persistent original-file references, multi-select and Alt-drag out. Removing references never deletes originals. |
| Keep Awake | Implemented: timed, indefinite or until a chosen clock time; automatic expiry and exit cleanup. |
| Live Weather | Implemented: existing opt-in Open-Meteo weather with manual city configuration. |
| Focus Mode | Partial: quiet mode and presentation/fullscreen suppression; no universal Windows Focus/DND synchronization. |
| Battery HUD | Implemented: charger, low-battery and configurable full-charge target alerts. |
| Synced Lyrics | Implemented: explicit LRCLIB request and time-synchronized current line. Network/provider matching dependent. |
| Apple Reminders | Windows equivalent: local due reminders while Island runs; no Apple/iCloud sync. |
| Image Converter | Partial: PNG/JPEG/TIFF/BMP/PDF output and first-page PDF input. HEIC input needs a Windows codec; no HEIC output. |
| Audio Switcher | Partial: opens Windows output settings. No built-in default-endpoint switcher. |
| Device Battery | Partial: existing Bluetooth battery properties where Windows drivers expose them; no proprietary Magic accessory protocol. |
| Download Alerts | Implemented with limits: opt-in default Downloads-folder watcher, session history and reveal actions. |
| Apple Shortcuts | Windows equivalent: explicitly pinned .lnk/.url/apps. Apple Shortcuts workflows cannot run on Windows. |
| Zip & Unzip | Implemented: ZIP shelf files; extraction checks traversal, symlinks, entry count and expanded size. No forced overwrite. |
| Screenshot Shelf | Partial: Windows region capture plus explicit staging of the clipboard image. No automatic screenshot collection. |
| Drive Eject | Partial: removable-drive list/open and Windows device settings. No confirmed one-click ejection. |
| App Launcher | Implemented with limits: pin apps/shortcuts, launch, remove and move up. No drag reorder. |
| Window Snapping | Partial: tile the previously focused window to halves/top/bottom/maximize. No window-drag interception. |
| Text Snippets | Partial: persistent named snippets and palette copying without history insertion. No injected paste or automatic expansion. |
| OCR & Search | Partial: explicit native clipboard-image OCR using installed Windows language packs; text history search. No automatic OCR indexing. |
| Screen Capture | Windows equivalent: Windows region selector; clipboard result can be staged and OCRed. |
| Color Eyedropper | Implemented with limits: click a pixel, copy hex and show HUD; Escape cancels. No magnifier. |
| Mic Mute | Implemented with limits: mute default communications input endpoint. Other input devices are unaffected; hardware untested. |
| Keystroke HUD | Not implemented; no generic typed-key capture or false password-field safety guarantee. |
| Keyboard Cleaning Lock | Implemented: explicit 20-second lock, Ctrl+Alt+Escape exit and unconditional timeout. Secure attention remains Windows-managed; hardware untested. |
| Menu Bar Icon Hiding | Not implemented; shell-owned Windows tray icons have no supported macOS-style manipulation API. |
| System Stats | Partial: native CPU/memory while page is open; no network graph. |
| Call Island | Not implemented; requires provider-specific call and answer/decline integrations. |
| Stocks Ticker | Not implemented; requires a configured market-data provider, chart and delay/session metadata. No fabricated prices. |
| Notch Terminal | Not implemented; no embedded ConPTY/VT100 terminal or fake command-box substitute. |
| Desktop Widgets | Partial Windows equivalent: detached native workspace pages. No Windows Widgets-board extension. |
| Agent Approvals | Integration foundation: authenticated approval activities and explicit Allow/Deny decisions for configured clients. No native agent hook installed; no bypass of native permissions. |
| Privacy Indicator | Not implemented system-wide; explicit own-camera use and Windows privacy settings links only. |
| Caps Lock HUD | Implemented: modifier-state polling and toggle confirmation; no typed-key logging. |
| Full Charge Island | Implemented: configurable 80-100 percent crossing alert. No added low-battery sound option. |
| Alt HUD Styles | Partial: sidecar/inline positions retained. No edge-pinned or circular gauge. |
| Live Audio Spectrum | Implemented: existing actual WASAPI loopback FFT with smoothing and endpoint recovery. |
| Custom Idle Animation | Not implemented; no Lottie runtime/importer. |
| Multiple Live Activities | Partial: simultaneous authenticated activity list. No adaptive-width ranked multi-chip pill. |
| Lock Screen Widgets | Unavailable to this desktop overlay on Windows secure lock screen. Existing hide-on-lock behavior remains. |
| Message Island | Partial: Windows notification bodies can be mirrored; no inline SMS/Messages replies. |
| Notification Mirror | Implemented with limits: packaged opt-in listener and session-only history; no action forwarding or old-toast replay. |
| Agent Activity | Integration foundation: scripts can POST agent/token/cost reports; no automatic agent hook registration. |
| Shell Activity | Integration foundation: included PowerShell command wrapper posts start/completion; no shell profile modified. |
| Developer API | Implemented: opt-in authenticated 127.0.0.1:9999 server, per-user DPAPI token, bounded connections/requests and activity/decision endpoint. |
| External Display Control | Implemented with limits: explicit DDC/CI discovery/brightness writes. Requires compatible monitor/driver; hardware untested. |
| Keyboard Backlight | Not implemented; requires vendor-specific Windows interfaces. |
| AirDrop from the Shelf | AirDrop unavailable on Windows; native shelf file drag-out only. |
| Clipboard Smart Actions | Partial: copy, HTTP(S) link opening, email compose and hex swatches. No injected paste or burst UI. |
| Peek on Hover | Implemented: compact peek with previous/play-pause/next controls. |
| Drift-free Stopwatch | Implemented with limits: absolute count-up, pause/resume/reset in Focus; not persisted or shown in collapsed pill. |
| Gestures & Hover Open | Partial: horizontal/vertical switches, hover peek and auto-collapse. No hidden-edge activation. |
| Hidden from Screen Capture | Implemented best effort: Windows display-affinity exclusion; not guaranteed against every capture path. Tray hide remains available. |
| Pill on Any Mac | Windows equivalent: virtual island with monitor, scale and offset/attached-notch settings. |
| Timer Mascots | Not implemented; no animated mascot picker. |
| Automatic Updates | Not implemented; portable/MSIX releases only, no signed hosted feed or beta channel. |
