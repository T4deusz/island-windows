# References and components

This is an independently implemented C# / WPF Windows app. The macOS visual direction and interaction ideas were studied in [TheBoredTeam/boring.notch](https://github.com/TheBoredTeam/boring.notch), including its ContentView, MusicManager and shelf layout. That repository is GPL-3.0. No Swift implementation, source assets, or mediaremote adapter from it are bundled here.

[FlorianButz/fenUISharp](https://github.com/FlorianButz/fenUISharp) was reviewed for Windows media-session events, view transitions and native-window design. Its author marks it unmaintained and not production-ready. It is Apache-2.0 licensed. No fenUI renderer, Skia dependency, fonts or source assets are incorporated.

Windows API interop definitions follow the Microsoft Windows SDK interfaces. The portable distribution contains the Microsoft .NET and Windows Desktop runtime and Windows SDK projection assemblies under their supplied licenses/notices. Weather uses Open-Meteo (CC BY 4.0; free API for non-commercial use). Optional synced lyrics are requested from LRCLIB only after an explicit user action.

This app is not affiliated with Apple, Alcove, Notchy, Boring Notch or fenUISharp. No Apple fonts, SF Symbols assets, logos or binaries are redistributed.

The user-requested [logo-design skill](https://github.com/kaankiziltug/logo-design-skill) was installed and consulted for the custom UI icon system: consistent geometry, SVG audit and small-size visual inspection. The 50 interface paths were authored for Island; no library brand marks are bundled.
