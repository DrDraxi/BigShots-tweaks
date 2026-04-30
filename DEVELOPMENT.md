# Development

For end-user install, see [README.md](README.md). This file is for building, decompiling, and releasing.

## Requirements

- .NET SDK 9
- BIG SHOTS installed via Steam
- MelonLoader 0.7.2+ installed in the game folder
- (Optional) [`ilspycmd`](https://github.com/icsharpcode/ILSpy) for decompiling

## Building

The `mod/BigShotsTweaks.csproj` references the game's managed DLLs at the literal path `C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS\BigShots_Data\Managed\...`. Steam ships the folder as `BIG SHOTS®`, so to build you need to either run the setup script (which renames the folder) or create a junction:

```bash
# rename + patch + install via the setup script
pwsh ./scripts/Setup-BigShotsFolder.ps1

# OR a junction (no admin, no Steam manifest changes)
mklink /J "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS" "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS®"
```

Then build:

```bash
dotnet build mod/BigShotsTweaks.csproj -c Release
```

A Release build does two extra things via MSBuild targets:

1. **Deploy** — copies `BigShotsTweaks.dll` into `<game>/Mods/` so the next launch uses the fresh build.
2. **Stage release** — drops a versioned zip at `Releases/BigShotsTweaks-v<Version>.zip` containing the DLL + setup scripts. This is what end users download.

Bump `<Version>` in `mod/BigShotsTweaks.csproj` and the matching `MelonInfo` string in `mod/Mod.cs` together before tagging a release.

## Launch

Click Play in Steam, or:

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\BIG SHOTS\BigShots.exe"
```

MelonLoader log: `BIG SHOTS/MelonLoader/Latest.log`.

## Decompiling the game

Game source isn't shipped, but `ilspycmd` (a dotnet global tool) produces readable C#:

```bash
dotnet tool install -g ilspycmd
cd "/c/Program Files (x86)/Steam/steamapps/common/BIG SHOTS/BigShots_Data/Managed"
for dll in AlterEyes.BigShots.*.dll; do
  name="${dll%.dll}"; name="${name#AlterEyes.BigShots.}"
  out="../../../../decompiled/$name"
  mkdir -p "$out" && ilspycmd -p -o "$out" "$dll"
done
```

The `decompiled/` folder is `.gitignore`d — regenerate after game updates.

## Releasing

Local: every Release build stages a versioned zip in `Releases/`.

GitHub Releases: tag and the workflow at `.github/workflows/release.yml` creates a release with the staged zip attached:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub's CI runners don't have BIG SHOTS installed, so the workflow's build step is gated behind a `stubs/` folder existing. For now it functions as a tag-and-publish helper — build locally, then push the tag.
