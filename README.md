# Awakened Warframe Market

Check what an item is worth on [warframe.market](https://warframe.market) without leaving the game: hover an item, press a key, and the sellers show up in an overlay next to it.

## Features

- **One key**: hover an item in your inventory or on a vendor screen (like Nightwave offerings) and press `Ctrl+D` (you can change it)
- **All sellers**: price, quantity, rank and listing age for every seller, filtered by In game / Online / All
- **Summary**: lowest and median price among sellers in game, how many sellers there are and how many are in game now
- **Ranks**: pick the rank you're after for mods and arcanes ("Rank 3 of 5"), or Any / Max
- **Sets**: switch between the whole set and each of its parts right in the overlay
- **Buy**: one click copies the same whisper warframe.market gives you, ready to paste into the game chat
- **Manual search**: if the name can't be read, type it and pick from the closest items
- **Stays out of the way**: the hotkey only works while Warframe is in front, the overlay closes when you click back into the game, and the app sits quietly in the tray

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Warframe in **windowed** or **borderless fullscreen** mode: Windows can't show other windows on top of exclusive fullscreen
- Warframe interface language set to **English**: names are read from the screen and looked up in the English item list
- PC prices only, for now

## Installation

1. Download the latest release (or [build from source](#building-from-source))
2. Extract it to any folder
3. Run `AwakenedWarframeMarket.exe`
4. On the first run the app downloads the text recognition data for English (about 23 MB), so it needs an internet connection

## Usage

1. Start Warframe and the app (it can stay in the tray)
2. Hover an item: put the cursor **in the middle of the item's picture**, not on its name. The name is read from the bottom of the item's cell, and a cursor on top of the text hides part of it
3. Press `Ctrl+D` (or your own hotkey)
4. The overlay opens next to the item with its sellers

### Keys in the overlay

| Key | Action |
|-----|--------|
| `B` | Copy the whisper for the selected seller (or click **Buy** next to any seller) |
| `T` | Open the item on warframe.market |
| `↑` `↓` | Move the selection between sellers |
| `Esc` | Close |

In the manual search: type the name, `↑` `↓` to choose, `Enter` to search.

The overlay closes by itself when it loses focus: when you click back into the game, Alt+Tab away or open the item on the site.

## Settings

Open **Settings** in the app window:

- **Price check hotkey**: click the field and press the combination you want
  - Ctrl, Alt and/or Shift together with a letter or a digit, e.g. `Ctrl + D`, `Ctrl + Shift + 1`
  - or F1-F12, alone or with modifiers
  - a letter or digit without Ctrl, Alt or Shift isn't allowed, it would fire while you type in chat
- **Only while Warframe is in front** (on by default): the app only takes the hotkey while Warframe or the app itself is in front, so other programs can use the same keys the rest of the time

## Troubleshooting

The app window shows its status at the top, which explains most problems:

| Status | What it means |
|--------|---------------|
| **Ready** | Everything works |
| **Waiting for Warframe** | The hotkey is paused because Warframe isn't the active window. Click into the game |
| **Hotkey unavailable** | Another program already uses this combination. Pick another one in Settings |
| **Couldn't start** | Text recognition couldn't load, usually because the first-run download failed. Check your connection and restart the app |

In the overlay:

**"Couldn't read the item name"** or **"Couldn't match the name"**
- Put the cursor in the middle of the item's picture, not on the name, and try again
- Or just type the name in the search box and press `Enter`
- Check that the game language is English

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

## How it works

For the curious, here is what happens after you press the hotkey:

1. **Capture.** The app takes a screenshot of a small area around the cursor (about 11% × 20% of the screen), before the overlay appears so it doesn't get in the way.
2. **Read.** [Tesseract](https://github.com/tesseract-ocr/tesseract) recognises the text in that area. Long names wrap onto several lines, so lines that sit right under each other are joined into one name. Of all the text found, the app takes the label at or below the cursor: in Warframe an item's name is always at the bottom of its cell.
3. **Match.** The text is looked up in the warframe.market item list: first an exact match, then the closest name allowing a few misread letters. If the text is too vague (say, only "Hildryn Prime" was read), the app asks you instead of guessing between the set and its parts.
4. **Prices.** The sellers and the item's set come from the public warframe.market API (v2). Apart from the one-time download of the recognition data, the app only talks to warframe.market, and only to ask for item data. The screenshot only lives in memory and is thrown away right after reading; the only files the app writes are its settings (`settings.json`) and the recognition data it downloads once (`tessdata`), both next to the exe.

## Dependencies

- [Tesseract OCR](https://github.com/charlesw/tesseract) - Text recognition
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON parsing
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) - Screen capture

## License

MIT License

## Credits

- Data provided by [warframe.market](https://warframe.market). This is a fan-made tool, not affiliated with warframe.market or Digital Extremes
- OCR powered by [Tesseract](https://github.com/tesseract-ocr/tesseract)
