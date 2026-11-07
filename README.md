# Nightreign PhotoKit (source edition)

Chinese-language launcher source and build notes for the Nightreign photo / Boss practice toolkit.

This public repository contains the original launcher source code and documentation. It is **not** the ready-to-run convenience package. It does not include the game, Boss Arena, ME3, crittercam, translated game resources, offline launch proxies, runtime installers, or any third-party executables.

## Build

Requirements: Windows x64 and the .NET Framework 4.x C# compiler included with Windows.

```powershell
./build.ps1 -PackageRoot C:\path\to\your\locally-assembled-package
```

The script builds `启动工具包.exe` in the package root and embeds SHA-256 checks for the required component files. You must obtain and assemble the components yourself as described below; the script will stop if it cannot find a complete component tree.

## Components (download from their original sources)

- Nightreign Boss Arena by PositronCannon: <https://www.nexusmods.com/eldenringnightreign/mods/449>
- ME3 by garyttierney and contributors: <https://github.com/garyttierney/me3>
- crittercam by gaknippel: <https://github.com/gaknippel/crittercam>
- Microsoft Visual C++ Redistributable (x64): <https://aka.ms/vs/17/release/vc_redist.x64.exe>
- Microsoft Edge WebView2 Runtime: <https://go.microsoft.com/fwlink/?linkid=2124701>

The upstream Boss Arena page prohibits uploading its files to other sites. The crittercam repository's license wording does not clearly grant redistribution rights. Download these components from their original sources and follow their current terms. No third-party binaries or translated game-resource files are mirrored here.

## Launcher layout

The launcher expects these paths relative to the package root:

```text
components/me3/bin/me3.exe
components/crittercam/crittercam.exe
runtimes/vc_redist.x64.exe
runtimes/MicrosoftEdgeWebView2RuntimeInstallerX64.exe
```

It also expects the Boss Arena mod files and the separate Sandbox/Progression save directories for the launch modes. Obtain and install each component from its upstream source. The repository does not provide an automated downloader or redistribute those files. Exact supported package versions and layout may change with upstream releases.

## Scope

- Original launcher source: MIT, see [LICENSE](LICENSE).
- Third-party components retain their own licenses and permissions; this repository's MIT license does not cover them.
- This source edition does not include the optional `start_protected_game.exe` replacement feature present in the private convenience package.
- Game launch and in-game behavior must be tested against the specific installed game and mod versions.

## Disclaimer

Elden Ring Nightreign and its game assets are property of their respective owners. This is an unofficial community tool and is not affiliated with or endorsed by FromSoftware or Bandai Namco.
