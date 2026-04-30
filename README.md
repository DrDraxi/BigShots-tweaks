# BigShots Tweaks

A MelonLoader mod for **BIG SHOTS** by AlterEyes that lifts the 2-player session cap.

- Configurable max players (default **8**, range 2-200)
- In-game slider in **Settings -> Game**, right after Region
- Live **Party X/N** in the Game tab header
- Auto-clicks **Continue Offline** if the startup connection prompt appears

> **What works at 4-8 players:** networking, lobby, combat, waves, scoring, revives, level transitions.
> **The compromise:** the start-of-shift drop-in and end-of-shift pickup cinematics are hardcoded for 2 dropships per level. Players 3+ skip both cinematics — they're spawned in directly at mission start and warped to the next level on completion. Everyone else still gets the normal animations.

---

## Install

### 1. Install MelonLoader

Download the [MelonLoader installer](https://melonloader.co/), launch it, click **Select** and point it at `BigShots.exe` (in your BIG SHOTS Steam folder), pick MelonLoader 0.7.2 or newer, and click **Install**. Run BIG SHOTS once after — MelonLoader will create the `Mods/` and `UserData/` folders inside the game directory.

### 2. Run the setup script

Download **[the latest release zip](../../releases/latest)** and extract it anywhere.

> **What the script will do — read this before running.**
> - Stop Steam (and BIG SHOTS if it's running). Don't run this during a Steam update or while verifying game files.
> - Rename the install folder from `BIG SHOTS®` to `BIG SHOTS` (the ® causes problems for some Unity tooling).
> - Patch Steam's `appmanifest_<appid>.acf` so Steam still sees the game as installed. The original is backed up next to it as `appmanifest_<appid>.acf.bst.bak`.
> - Copy `BigShotsTweaks.dll` into `<game>/Mods/`.

Then run **`Setup-BigShotsFolder.bat`** (or the `.ps1` from PowerShell). It auto-detects Steam (registry + common paths, scans every Steam library), prints the plan, and waits for confirmation before doing anything.

If the script can't find `<game>/Mods/` it stops with a red error pointing back to MelonLoader — that means step 1 was skipped or failed.

### 3. Launch BIG SHOTS via Steam

Look in `BIG SHOTS/MelonLoader/Latest.log` for `[BigShotsTweaks] Loaded.` to confirm the mod is running.

### Script flags

```
.\Setup-BigShotsFolder.ps1                     # full install (interactive)
.\Setup-BigShotsFolder.ps1 -Revert             # rename folder back, restore manifest (does NOT uninstall the mod)
.\Setup-BigShotsFolder.ps1 -NoInstallMod       # rename + manifest patch only
.\Setup-BigShotsFolder.ps1 -SteamRoot "D:\Steam"   # override auto-detect
.\Setup-BigShotsFolder.ps1 -Yes                # skip the "press Enter to continue" prompt - only use if you know what the script does
```

### Manual install (skip the rename)

If you'd rather not touch Steam's manifest, drop `BigShotsTweaks.dll` directly into `BIG SHOTS®/Mods/`. The mod itself doesn't care about the folder name — only the build/dev tooling does. Steam's "Verify Integrity" won't undo this, but you also don't get the cleaner folder name.

---

## Configuration

The config is auto-created on first run at `BIG SHOTS/UserData/BigShotsTweaks.cfg`:

```toml
[BigShotsTweaks]
MaxPlayers = 8                  # 2-200 (in-game slider caps at 8; edit cfg for higher)
AutoContinueOffline = true      # auto-click the startup "continue offline" button
```

The in-game slider (Settings -> Game) writes the same file live.

---

## Uninstall

1. Delete `<game>/Mods/BigShotsTweaks.dll`.
2. (Optional) Run `Setup-BigShotsFolder.ps1 -Revert` to rename the folder back to `BIG SHOTS®` and restore the original Steam manifest.
3. (Optional) Delete `<game>/UserData/BigShotsTweaks.cfg` to remove your settings.

To remove MelonLoader entirely, see the [MelonLoader uninstall docs](https://melonwiki.xyz/#/?id=uninstalling-melonloader).

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Nothing happens / no log line | MelonLoader isn't installed, or DLL isn't in `Mods/` |
| "Mods folder does not exist" red error from the script | MelonLoader install was skipped or failed |
| Folder renamed back to `BIG SHOTS®` after a Steam update | Steam's "Verify Integrity" reset the manifest. Re-run `Setup-BigShotsFolder.bat`. |
| `Setup` script can't find Steam | Pass `-SteamRoot "X:\Path\To\Steam"` |
| Players 3+ get stuck on the dropship at mission start/end | Known limitation — see the warning at the top |

---

## License

No license selected. Modding is for personal/community use; respect AlterEyes' rights to the game.

---

Building from source, decompiling, releasing: see **[DEVELOPMENT.md](DEVELOPMENT.md)**.
