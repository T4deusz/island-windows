# Island 1.1 · UI and motion upgrade

The existing WPF app was refactored in place. Media, local agenda, calendar, weather, Bluetooth/battery alerts, notifications, focus timer, quiet mode, tray behavior and settings persistence are retained. Existing preferences migrate with defaults for the new controls.

## Try it

1. Quit the currently running Island using its tray menu when you are ready to switch. Its process was left running during development.
2. Open `Island-Windows-1.1/Island.exe`, or extract `Island-Windows-1.1.zip` and open the executable there. Keep the bundled runtime files beside it. The old portable folder was not overwritten.
3. Open **General** to select Spring/Gentle/Instant motion, artwork accents, live visualizer, debug FPS, widget order and HUD position. Reduced motion overrides spring animation and disables continuous visualization.
4. For notification mirroring, use `Island-Installer-1.1.zip` and its install instructions. The package is locally signed, not publicly trusted. No certificate or package was installed during this upgrade.

## What changed

- A damped spring (stiffness 300, damping 25) drives expand, collapse, hover peek, page crossfades and HUD entry/exit. Gentle mode uses damping 34. Retargeting keeps the current position and velocity.
- The native window stays the same size throughout each transition. Background surfaces, artwork, title, content and HUDs use render transforms and opacity. Placement and overall size change only when settings/display configuration change.
- One artwork image and one title visual move and scale between compact and expanded states. Rounded compact/expanded background surfaces crossfade during the morph. Expanded media content enters with 0.92→1 scale, vertical offset and 30 ms staggering.
- Cached blurred artwork, a dark gradient scrim, dominant-color accents, 8%-white borders and soft shadows add depth. Text hierarchy and Fluent/vector icons replace the previous symbolic controls.
- The eight visualizer bars measure actual system output through WASAPI loopback and a 1024-point FFT, with fast attack and smooth falloff. Output format is detected; float32 and signed PCM16/24/32 are supported. Audio remains in memory, is never saved/uploaded, and the microphone is not used. Capture is stopped when collapsed, hidden, locked, on another widget, disabled, or reduced-motion mode is active. Default endpoint changes are checked while capturing and reopened as needed.
- Hover gives a small peek; click opens fully. Horizontal drags follow the pointer and rebound at the first/last widget. Dots and tabs follow the configured widget order. Vertical drags still expand/collapse.
- The playback slider shows a time tooltip while dragging and requests a seek on release. Unsupported apps disable it. A track change during the drag cancels the seek.
- Scrolling the compact pill produces an inline level HUD. Other level updates can appear below the island or inline according to Settings. HUD level, entry and dismissal use spring-driven transforms/opacity.
- Standard physical volume/mute keys can be intercepted while the island is visible, unlocked and has an audio output. The hook reads only those key codes, does no audio work in its callback, and keeps no input log. The feature can be switched off. Turning the level up unmutes the output.
- Periodic Core Audio reads now run off the UI thread. The render callback detaches when transitions settle and no active visualizer/debug view needs it. Unavailable WMI brightness hardware is not repeatedly queried.
- Debug FPS displays render-callback rate, worst recent callback interval, graphics tier and loopback status. The app requests the default hardware-capable WPF pipeline and reports the actual graphics tier. It does not force software rendering.

## Verification

Release compilation: **zero warnings and errors**. **32 self-checks** cover old settings migration, new settings validation, spring settling/stability/retargeting, rubber-band bounds, PCM conversion, FFT silence/frequency behavior, artwork palette extraction, shared-visual and fixed-window invariants, all settings/widget renders, and timer start/pause behavior.

Live diagnostics captured a **48 kHz, eight-channel** default output, received **117,600 audio frames** in the sample, and produced nonzero spectrum levels. Core Audio and a browser media session were also read successfully. No system settings were changed by the diagnostic.

The final optimization sample on this PC recorded:

| Measurement | Result |
| --- | --- |
| Collapsed idle, 20 seconds, debug off | **0.187% total CPU**, 20 logical CPUs |
| Same CPU time expressed against one logical core | 3.749% |
| Working set in the sample | 228.2 MB |
| Five expand/collapse cycles | 158.2 render callbacks/s average |
| 95th-percentile callback interval | 11.11 ms |
| Worst sampled callback interval | 16.76 ms |
| Native window resizes during those transitions | **0** |
| Graphics tier / render mode | **Tier 2 / Default** |

See `Performance-1.1.txt` for the saved report. The performance harness uses a separate temporary instance with volume interception disabled and does not change saved preferences. Render callbacks are not GPU presentation timestamps. These measurements do not establish a universal “never below 60 FPS” guarantee or a CPU limit on every machine. The debug overlay itself intentionally keeps the render callback active.

## Remaining boundaries

**Universal brightness-flyout suppression is not implemented.** Brightness can be detected/controlled only on WMI-compatible displays, with Island's animated sidecar HUD. Vendor volume keys, app-command events, elevated windows and protected/exclusive-mode audio may bypass the standard interception/capture paths. No Windows shell components are patched or hidden.

Physical-key flyout interception, hardware brightness changes, seeking with every media app, multi-monitor/DPI behavior, MSIX installation/notification consent and lock/unlock have not been verified end to end. Existing secure-lock-screen, calendar account-sync and proprietary accessory limitations remain as documented in README.md.

The previews are WPF renders with labeled synthetic sample artwork. They are not screenshots of a live media service or an animation frame-rate recording.
