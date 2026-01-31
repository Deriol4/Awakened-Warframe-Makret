# Awakened Warframe Market

A fast overlay tool for checking Warframe item prices from [warframe.market](https://warframe.market).

## Features

- **Global Hotkey**: Press `Ctrl+D` (customizable) to check prices instantly
- **OCR Detection**: Automatically reads item names from your screen
- **Fast API Lookup**: Shows top 20 sell orders sorted by price
- **Overlay Display**: Clean, semi-transparent overlay with price information
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
3. Hover your cursor over an item name in Warframe
4. Press `Ctrl+D` (or your custom hotkey)
5. View prices in the overlay
6. Press `ESC` or click `✕` to close the overlay

## Settings

Click the **Settings** button to customize:
- **Hotkey**: Change the modifier (Ctrl, Alt, Shift) and key
- **Debug Mode**: Enable to save screenshots for troubleshooting

## Hotkey Options

Modifiers:
- Ctrl
- Alt
- Shift
- Ctrl+Shift
- Ctrl+Alt
- Alt+Shift

Keys:
- A-Z
- F1-F12
- 0-9

## Troubleshooting

### "Could not detect item name"
- Make sure your cursor is directly over the item name text
- Warframe should be in windowed or borderless windowed mode
- Enable Debug Mode in settings to save screenshots

### "Hotkey not working"
- Another application may be using the same hotkey
- Try a different key combination in Settings

### "Item not found"
- The OCR may have misread the name
- Try positioning cursor closer to the center of the item name

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
