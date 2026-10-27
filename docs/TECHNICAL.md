# Technical notes

[Back to Island](../README.md) · [Feature coverage](../Feature-Coverage-1.3.md)

Commands below run from the repository root.

## Explicit hardware controls

Camera, microphone mute, per-app volume, window snapping, pixel sampling, keyboard cleaning and DDC brightness changes happen only after a user action. Camera requests may trigger Windows permission dialogs; DDC calls require monitor/driver support and can take seconds. The cleaning lock expires after 20 seconds; **Ctrl+Alt+Esc** exits immediately, and Windows secure attention remains available. Native keyboard hooks can be blocked by elevated processes or system policy.

Notifications require the MSIX edition and Windows permission. The local API, clipboard collection and download watcher are off by default. Optional network actions include weather, lyrics, web search and browser translation.

## Notification-capable MSIX edition

`Package.ps1` can create a locally signed MSIX and public development certificate. GitHub releases currently distribute the portable app. The package declares notification-listener, global-media and webcam capabilities. No certificate or app was installed or trusted during development.

Build the optional package from the repository root using `Package.ps1` after publishing the portable app. Packaging requires the Windows SDK. GitHub releases provide the portable ZIP; no signing keys are included.

## Developer API and shell reports

Enable Workspace > Developer and copy the per-install token. It is stored encrypted for the Windows account using DPAPI. The server binds only `127.0.0.1:9999`. Nothing listens while disabled. Requests need `Authorization: Bearer <token>`; no CORS permission is granted. HTTP requests are capped at 8 KB headers / 64 KB body, 16 active connections and a three-second read deadline; up to 100 activities are retained in memory.

POST JSON to `/activities`:

```json
{"id":"my-build","title":"Building the app","kind":"build","progress":0.5}
```

Kinds: `activity`, `build`, `download`, `agent`, `approval`, `usage`, `complete`, `error`. Optional `provider`, `tokens` and `costUsd` must be reported by the client. GET `/activities` returns the current list and any user decision (`allow`/`deny`). Client-supplied decisions are ignored; updating an activity preserves a decision made in the UI. A token holder can request approval but cannot grant it through POST. These decisions apply only to integrations you write, not to native agent security controls.

The source includes `Invoke-IslandCommand.ps1`. After explicitly enabling the API and setting `ISLAND_API_TOKEN` in your shell, run:

```powershell
.\Invoke-IslandCommand.ps1 -Title 'Build' -Command { dotnet build }
```

This reports start/completion/failure. It does not modify any shell profile, install hooks, send commands to an agent, or run in the background automatically.

## Data

Settings remain in `%LOCALAPPDATA%\IslandDesktop\settings.json`. Notes, pinned clips, snippets, launcher paths, shelf references, reminders and focus history are stored in `workspace.json` in the same folder. These user-authored data are local plaintext; the API token is DPAPI encrypted in `developer-token.bin`. No token, clipboard content, local note or user log file is included in the release/source archives. Error logs are bounded. Image/ZIP output uses temporary files and commit-on-success to protect existing data.

## Validation and limits

Release compilation has zero warnings/errors. **156 self-checks** cover the original app plus media glyph state, every workspace page, arithmetic validation, URI restrictions, lyrics timing, activity validation, safe ZIP round trip/traversal rejection, PNG/PDF export and usage log parsing, in-island routing for every tool, Escape/search handling, vector geometry and accessible button state. **9 HTTP integration checks** verify authentication, malformed JSON handling, activity round trip, approval trust boundaries, oversized-request rejection/recovery and listener shutdown.

The local 1.3 performance sample measured 0.191% total collapsed idle CPU (3.828% of one logical core on a 20-thread PC) after opening and closing tools. Three media and two tool transitions averaged 92.5 render callbacks/s, p95 interval 15.01 ms and worst 30.49 ms, with zero window resizes. Render callbacks are not GPU presentation timestamps; this is not a universal 60 FPS or CPU guarantee. This sample predates the response-based spring update.

Live media/session and WASAPI loopback reads were tested. Camera/permission flow, OCR language availability, microphone or per-app volume writes, keyboard lock, DDC brightness writes, cloud/provider networking, notification consent, secure-session transitions and multi-monitor/DPI behavior remain untested end to end. The archive signature is a local development signature, not a publicly trusted publisher.

Windows secure lock-screen overlays, Apple AirDrop/Shortcuts/Reminders integration, proprietary AirPods controls and several requested provider/driver features remain unavailable or unimplemented. The coverage document names every gap; no fake prices, terminal, global privacy indicator, biometric unlock or automatic-update feed is presented as working.

Clone this repository to obtain the source. `Build.ps1 -SelfContained` builds the portable app; `Package.ps1` creates a locally signed MSIX using the Windows SDK. `Island.exe --self-test`, `--api-self-test`, `--diagnostics` and `--performance-test` produce local reports. Diagnostics can contain current track/device names; review before sharing. Preview PNGs use synthetic sample media/notes, not private live content.

References and licenses are documented in `THIRD_PARTY_NOTICES.md`. The two reference repositories were reviewed for inspiration; no Swift/fenUI renderer code, Apple fonts or assets were copied into the app.

## Motion update

Motion.cs now defines response/damping presets for Open, Close, Smooth, Snappy, Bouncy, Tracking and Release. IslandWindow selects the preset for each interaction; Gentle applies at least critical damping, and Instant/reduced motion still snap directly. Fades use smoothstep and edge swipes use the bounded rational rubber-band curve. The Windows release workflow includes these presets.
