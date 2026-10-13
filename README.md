# Awakened Warframe Market

Check what an item is worth on [warframe.market](https://warframe.market) without leaving the game: hover an item, press a key, and the sellers show up in an overlay next to it.

**[⬇ Download the latest version](https://github.com/Deriol4/Awakened-Warframe-Makret/releases/latest)**

## Features

- **One key**: hover an item in your inventory or on a vendor screen (like Nightwave offerings) and press `Alt+X` (you can change it)
- **All sellers**: price, quantity, rank and listing age for every seller, filtered by In game / Online / All
- **Summary**: lowest and median price among sellers in game, how many sellers there are and how many are in game now
- **Ranks**: pick the rank you're after for mods and arcanes ("Rank 3 of 5"), or Any / Max
- **Variants**: relics are priced per refinement (Intact … Radiant), and the overlay lets you switch between them; charges and Ayatan stars are shown too
- **Sets**: switch between the whole set and each of its parts right in the overlay
- **Buy**: one click copies a whisper in the same format as warframe.market, ready to paste into the game chat
- **Search anything**: press `Alt+F` to look up any item by name, also ones you don't have yet. Any part of the name works ("hotshot", "volt neuro")
- **Manual fallback**: if the name under the cursor can't be read, the search opens with what was read, ready to correct
- **English and Russian**: the interface, the game's item names and the search work in both languages
- **Stays out of the way**: the hotkey only works while Warframe is in front, the overlay closes when you click back into the game, and the app sits quietly in the tray

## Requirements

- Windows 10 or 11, 64-bit
- [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe): text recognition needs it. Most PCs with games already have it
- Warframe in **windowed** or **borderless fullscreen** mode: Windows can't show other windows on top of exclusive fullscreen
- Warframe interface language **English** or **Russian** (pick it in Settings → Game language): names are read from the screen in that language
- PC prices only, for now

## Installation

1. Download the zip from the [latest release](https://github.com/Deriol4/Awakened-Warframe-Makret/releases/latest)
2. Extract it to any folder, for example `C:\Games\AwakenedWarframeMarket`
3. Run `AwakenedWarframeMarket.exe`. Running it again just brings the open copy to the front

.NET is included in the zip, there's nothing else to install. To remove the app, delete its folder (and `%APPDATA%\AwakenedWarframeMarket` with the settings).

### "Windows protected your PC"

The app isn't code-signed yet (that's in progress), so on the first launch Windows SmartScreen shows a blue warning. Click **More info**, then **Run anyway**. This is needed only once.

## Is it safe?

- **Open source.** All the code is in this repository, and it's small enough to read through
- **Built by GitHub, not on a PC.** Release zips are built by [GitHub Actions](.github/workflows/release.yml) straight from this code, with a public build log. Each release lists the zip's SHA-256, and you can check that a zip really came from that build: `gh attestation verify <zip> --repo Deriol4/Awakened-Warframe-Makret`
- **Doesn't touch the game.** No memory reading, no injected code, no key presses or clicks sent to Warframe. It only takes a screenshot around the cursor when you press the hotkey, the same way a screenshot tool would
- **Private.** The screenshot stays in memory and is thrown away after reading. The app only connects to warframe.market, collects nothing about you, writes nothing but its settings file and doesn't need admin rights

## Usage

1. Start Warframe and the app (it can stay in the tray)
2. Hover an item (in the inventory, at a vendor, in the mods screen). Resting the cursor on the item's picture works best: a cursor on top of the name hides part of it
3. Press `Alt+X` (or your own hotkey)
4. The overlay opens next to the item with its sellers

To buy something you don't have, press `Alt+F` anywhere in the game, type part of its name and press `Enter`.

### Keys in the overlay

| Key | Action |
|-----|--------|
| `B` | Copy the whisper for the selected seller (or click **Buy** next to any seller) |
| `T` | Open the item on warframe.market |
| `↑` `↓` | Move the selection between sellers |
| `Esc` | Close |

In the search: type any part of the name, `↑` `↓` to choose, `Enter` to open it.

The overlay closes by itself when it loses focus: when you click back into the game, Alt+Tab away or open the item on the site.

## Settings

Open **Settings** in the app window:

- **Price check hotkey** and **Search hotkey**: click a field and press the combination you want
  - Ctrl or Alt (Shift optional) together with a letter or a digit, e.g. `Alt + X`, `Ctrl + Shift + 1`
  - or an F-key, alone or with modifiers, e.g. `F6`, `Ctrl + F5`
  - not allowed: a letter or digit without Ctrl or Alt (it would fire while you type in chat), Ctrl/Shift with W, A, S or D (you hold those while moving), `Alt + F4` and `F12` (Windows and Steam use them), the same combination for both hotkeys
- **Interface language**: English or Русский. By default it follows the Windows display language; a change applies after restarting the app
- **Game language**: the language Warframe shows item names in, English or Russian, so they can be read from the screen. The search understands item names in both languages either way
- **Only while Warframe is in front** (on by default): the app only takes the hotkeys while Warframe or the app itself is in front, so other programs can use the same keys the rest of the time

## Troubleshooting

The app window shows its status at the top, which explains most problems:

| Status | What it means |
|--------|---------------|
| **Ready** | Everything works |
| **Waiting for Warframe** | The hotkey is paused because Warframe isn't the active window. Click into the game |
| **Hotkey unavailable** | Another program already uses this combination. Pick another one in Settings |
| **Couldn't start** | Text recognition couldn't load. Usually the Visual C++ Redistributable (x64) is missing: install it and restart the app |

In the overlay:

**"Not found on warframe.market"**
- Some items can't be traded, so warframe.market doesn't list them: Umbral mods, some Primed mods (like Primed Shred) and others. The overlay shows what it read so you can check
- If the name was read wrong, correct it in the search box

**"Couldn't read the item name"**
- Rest the cursor on the item's picture rather than on its name, and try again
- Or just type the name in the search box and press `Enter`
- Check that **Game language** in Settings matches the language Warframe is set to

**"Couldn't reach warframe.market"**
- No connection, or the site is down. Try again in a moment

**The overlay doesn't appear**
- Warframe is in exclusive fullscreen: switch to borderless fullscreen in the game's display settings

## Building from Source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer).

```bash
git clone https://github.com/Deriol4/Awakened-Warframe-Makret.git
cd Awakened-Warframe-Makret

dotnet build
dotnet run --project src/AwakenedWarframeMarket
```

To publish a release, push a version tag (`git tag v1.0.1` then `git push origin v1.0.1`). GitHub Actions builds the zip and creates a draft release to review and publish.

## How it works

For the curious, here is what happens after you press the hotkey:

1. **Capture.** The app takes a screenshot of the area around the cursor (about 16% × 33% of the screen, mostly below the cursor, big enough for a hovered mod card), before the overlay appears so it doesn't get in the way. Warframe draws its own cursor into the picture, so the app paints over it.
2. **Read.** [Tesseract](https://github.com/tesseract-ocr/tesseract) recognises the text in that area. Everything readable becomes a candidate: single lines, lines wrapped under each other ("Primed Bane of / Orokin") and parts of lines.
3. **Match.** Each candidate is looked up in the warframe.market item list: exactly, with a few misread letters, or by the visible parts of its words when the cursor hid some letters. Of the candidates that are real items, the app picks the one that looks like the hovered item's label: in the cursor's column, printed largest, with a description under it on mod cards. If nothing fits clearly (say, only "Hildryn Prime" was read), the app asks you instead of guessing between the set and its parts.
4. **Prices.** The sellers and the item's set come from the public warframe.market API (v2). The app only talks to warframe.market, and only to ask for item data. The screenshot only lives in memory and is thrown away right after reading. The English and Russian recognition data (`tessdata`) ships with the app, and the only file the app writes is its settings: `%APPDATA%\AwakenedWarframeMarket\settings.json`.

## Dependencies

- [Tesseract OCR](https://github.com/charlesw/tesseract) - Text recognition
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON parsing
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) - Screen capture

## Code signing policy

Release builds are made from this repository by [GitHub Actions](.github/workflows/release.yml); nothing is built or signed on a personal computer. Only the app's own files are signed; third-party libraries ship as published by their authors.

Free code signing for this project has been requested from [SignPath.io](https://signpath.io), with the certificate by [SignPath Foundation](https://signpath.org). Until it's approved, releases are unsigned.

**Team roles**

- Committers and reviewers: [Deriol4](https://github.com/Deriol4)
- Approvers: [Deriol4](https://github.com/Deriol4)

**Privacy policy**

This program will not transfer any information to other networked systems unless specifically requested by the user. When you price-check or search for an item, the app asks the [warframe.market](https://warframe.market) API for that item's data and sellers; this is covered by warframe.market's own privacy policy. Nothing else is sent, and the app doesn't collect any statistics about you.

## License

[MIT](LICENSE). Third-party components and their licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- Data provided by [warframe.market](https://warframe.market). This is a fan-made tool, not affiliated with warframe.market or Digital Extremes
- OCR powered by [Tesseract](https://github.com/tesseract-ocr/tesseract)
