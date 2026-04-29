# BigShots Tweaks

A MelonLoader mod for **BIG SHOTS** (the Mono Unity game by AlterEyes). Currently ships one tweak: a configurable max-player cap that lifts the vanilla 2-player limit (default 8, range 2–200).

## Features

| Setting | Default | Range | Notes |
|---|---|---|---|
| `MaxPlayers` | 8 | 2–200 | Photon Fusion Shared mode supports up to 200. The lobby UI only has 2 visible slots — players 3+ join via room code. |

Config lives at `BIG SHOTS/UserData/BigShotsTweaks.cfg` and is auto-created on first run. Edit and restart the game.

```toml
[BigShotsTweaks]
MaxPlayers = 8
```

## Install (end users)

1. Install [MelonLoader 0.7.2+](https://melonloader.co/) into the BIG SHOTS folder.
2. Drop `BigShotsTweaks.dll` (from [Releases](../../releases)) into `BIG SHOTS/Mods/`.
3. Launch the game once. The mod creates `UserData/BigShotsTweaks.cfg` — edit it to taste, restart.

---

## Building from source

### One-time setup

The game's Steam folder is named `BIG SHOTS®` with a registered-trademark glyph, which makes melon loader unhappy. The `.csproj` references the clean path `BIG SHOTS`, so we need one of the approaches below.

**Option A — Rename + patch Steam manifest (recommended).** Run the helper script in `scripts/`. It closes Steam, renames the folder to `BIG SHOTS`, patches `appmanifest_<appid>.acf`'s `installdir`, and backs up the original manifest. Re-run with `-Revert` to undo.

```cmd
scripts\Setup-BigShotsFolder.bat
```

```powershell
.\scripts\Setup-BigShotsFolder.ps1            # rename
.\scripts\Setup-BigShotsFolder.ps1 -Revert    # undo
```

Caveat: Steam's **Verify Integrity of Game Files** may reset `installdir` and rename the folder back to `BIG SHOTS®`. Just re-run the script if that happens.

**Option B — Junction (no admin needed).** A junction is a directory link that doesn't require admin or Windows Developer Mode:

```cmd
mklink /J "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS" "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS®"
```

**Option C — Symbolic link (admin required).** Same idea as a junction but requires admin or Developer Mode:

```powershell
$steam = "C:\Program Files (x86)\Steam\steamapps\common"
New-Item -ItemType SymbolicLink -Path "$steam\BIG SHOTS" -Target "$steam\BIG SHOTS®"
```

### Build

```bash
dotnet build mod/BigShotsTweaks.csproj -c Release
```

Two things happen automatically on a Release build:

1. **Deploy** — `BigShotsTweaks.dll` is copied to `BIG SHOTS/Mods/` so you can launch and test immediately.
2. **Stage release** — a versioned copy lands in `Releases/BigShotsTweaks-v<Version>.dll`.

Bump `<Version>` in `mod/BigShotsTweaks.csproj` (and the matching `MelonInfo` string in `Mod.cs`) before tagging a release.

### Launch

Either click Play in Steam, or:

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS\BigShots.exe"
```

MelonLoader's log is at `BIG SHOTS/MelonLoader/Latest.log`. Look for `[BigShotsTweaks] Loaded. MaxPlayers = ...` to confirm it's running.

---

## Decompiling the game (for development)

Game source isn't shipped, but you can dump readable C# locally with [ilspycmd](https://github.com/icsharpcode/ILSpy):

```bash
dotnet tool install -g ilspycmd
cd "/c/Program Files (x86)/Steam/steamapps/common/BIG SHOTS/BigShots_Data/Managed"
for dll in AlterEyes.BigShots.*.dll; do
  name="${dll%.dll}"; name="${name#AlterEyes.BigShots.}"
  out="/c/Users/micha/Documents/GitHub/BigShots-tweaks/decompiled/$name"
  mkdir -p "$out" && ilspycmd -p -o "$out" "$dll"
done
```

The `decompiled/` folder is `.gitignore`d — regenerate after game updates.

---

## Releasing

Local: every Release build stages a versioned DLL in `Releases/`.

GitHub Releases: push a tag and the workflow at `.github/workflows/release.yml` creates a release with the staged DLL attached.

```bash
git tag v0.1.0
git push origin v0.1.0
```

Note: GitHub's CI runners don't have BIG SHOTS installed, so the workflow's build step is gated behind `stubs/` reference assemblies. For now it functions as a tag-and-publish helper — build locally, commit the staged `Releases/*.dll`, tag, push.

---

## License

No license selected. Modding is for personal/community use; respect AlterEyes' rights to the game.
