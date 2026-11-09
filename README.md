# Sudoku (large print)

A simple Sudoku game with big numbers and buttons, for Windows, iPhone and Android.

Website (iPhone + Android): **https://rlx.github.io/sudoku/**

## Installing

### Windows 10/11
1. Copy `dist/Sudoku.exe` onto a USB stick (or email it).
2. Drag it onto her Desktop and double-click it. Nothing else needs installing.

If Windows shows **"Windows protected your PC"**, click **More info**, then **Run anyway** (only the first time).

### iPhone
1. Open **https://rlx.github.io/sudoku/** in **Safari**.
2. Tap the Share button (square with an arrow ↑; on newer iPhones tap **•••** first).
3. Tap **Add to Home Screen**, then **Add**.
4. Open Sudoku from the Home Screen. It runs full-screen and works without internet.

### Android
1. Open **https://rlx.github.io/sudoku/** in Chrome and tap **Download the app**.
   (Or send her `dist/Sudoku.apk` directly.)
2. Open the downloaded file. If asked, allow installing from this source, then tap **Install**.
   If a "Play Protect" box appears, tap **Install anyway**.

## How to play

- Tap/click a square, then a big number button (keyboard number keys work on computers).
- Wrong numbers turn **red**.
- **Erase** clears a square; **Undo** takes back the last change.
- A number button turns grey once all nine of that number are placed correctly.
- The game saves after every move and reopens on the same puzzle.
- **New Puzzle** shows Easy / Medium / Hard; "Back to my puzzle" returns to the current one.

## Project layout

| Folder | What |
|---|---|
| `src/BigSudoku/` | Windows app (C#, WinForms, .NET Framework 4.8) |
| `web/` | Phone/web version (plain HTML/CSS/JS, works offline) |
| `android/` | Android wrapper that bundles `web/` into an app |
| `dist/` | Built `Sudoku.exe` and `Sudoku.apk` |
| `tests/` | Puzzle-logic tests and a screenshot script |

## Building (on a Mac)

```
brew install dotnet openjdk@21
brew install --cask android-commandlinetools
sdkmanager --sdk_root=$HOME/Library/Android/sdk "platforms;android-36" "build-tools;37.0.0"

# Windows
dotnet build src/BigSudoku -c Release && cp src/BigSudoku/bin/Release/net48/Sudoku.exe dist/

# Android (bump VERSION_CODE in android/build.sh for each update)
android/build.sh

# Tests
dotnet run --project tests/LogicTests -c Release
node tests/web/logic.test.js
```

Pushing to `main` publishes `web/` (plus `dist/Sudoku.apk`) to GitHub Pages.
When you change web files, bump `CACHE` in `web/sw.js` so installed copies pick up the update.

**Android signing key:** `android/release.keystore` and `android/keystore.properties` are not in git.
Back them up somewhere safe. Android only installs updates signed with the same key.
