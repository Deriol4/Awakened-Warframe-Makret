# Awakened Warframe Market

A fast overlay tool for checking Warframe item prices from [warframe.market](https://warframe.market).

## Features

- **Global Hotkey**: Press `Ctrl+D` (customizable) to check prices instantly
- **OCR Detection**: Automatically reads item names from your screen
- **All sellers**: price, quantity, rank and listing age for every seller, filter by All / Online / In game
- **Summary**: lowest and median price in game, how many sellers were found and how many are in game now
- **Ranks**: pick the rank you want for mods and arcanes ("Rank 3 of 5"), or Any / Max
- **Sets**: switch between the whole set and each of its parts right in the overlay
- **Buy**: one click copies the whisper for a seller, ready to paste into the game chat
- **Manual search**: if the name can't be read, type it and pick from the closest items
- **Tray Support**: Minimize to system tray

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Warframe running in windowed or borderless windowed mode

## Installation

1. Download the latest release
2. Extract to any folder
3. Run `AwakenedWarframeMarket.exe`
4. On first run, the app will download OCR data (~4MB)

## Usage

1. Start Warframe
2. Launch Awakened Warframe Market
3. Hover your cursor over an item in Warframe (its name is read from the bottom of the cell)
4. Press `Ctrl+D` (or your custom hotkey)
5. View prices in the overlay
6. Press `ESC` or click `✕` to close the overlay

### Keys in the overlay

| Key | Action |
|-----|--------|
| `B` | Copy the whisper for the selected seller (or click **Buy** next to any seller) |
| `T` | Open the item on warframe.market |
| `↑` `↓` | Move the selection between sellers |
| `Esc` | Close |

In the manual search: type the name, `↑` `↓` to choose, `Enter` to search.

The overlay closes by itself when it loses focus: when you click back into the game, Alt+Tab away or open the item on the site.

By default the hotkey is only taken while Warframe (or this app) is in front, so other programs can use the same keys. Turn this off in Settings to make it work everywhere.

## Settings

Click the **Settings** button to customize:
- **Hotkey**: click the field and press the combination you want
- **Only while Warframe is in front**: release the hotkey for other apps when you're not in the game (on by default)
- **Debug Mode**: Enable to save screenshots for troubleshooting

## Hotkey Options

- Ctrl, Alt and/or Shift together with a letter (A-Z) or a digit (0-9), e.g. `Ctrl + D`, `Ctrl + Shift + 1`
- F1-F12, alone or with modifiers

A plain letter or digit without a modifier isn't allowed: it would fire while you type in the game chat.

## Troubleshooting

### "Could not detect item name"
- Make sure your cursor is directly over the item name text
- Warframe should be in windowed or borderless windowed mode
- Enable Debug Mode in settings to save screenshots

### "Hotkey not working"
- Another application may be using the same hotkey
- Try a different key combination in Settings

### "Couldn't match the name"
- The OCR misread the name: type the correct name in the search box and press `Enter`
- Enable Debug Mode to see what was captured and read (`debug` folder next to the exe)

## Building from Source

```bash
# Clone the repository
git clone https://github.com/Deriol4/Awakened-Warframe-Makret.git
cd Awakened-Warframe-Makret

# Build
dotnet build

# Run
dotnet run
```

## Dependencies

- [Tesseract OCR](https://github.com/charlesw/tesseract) - Text recognition
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON parsing
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) - Screen capture

## API

This application uses the public [warframe.market API](https://warframe.market/api_docs) to fetch item prices.

## License

MIT License

## Credits

- Data provided by [warframe.market](https://warframe.market)
- OCR powered by [Tesseract](https://github.com/tesseract-ocr/tesseract)
