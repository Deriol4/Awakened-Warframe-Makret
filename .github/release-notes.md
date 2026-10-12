## What's new

- (fill in before publishing)

## Install

1. Download **{{ZIP}}** below
2. Extract it to any folder (for example `C:\Games\AwakenedWarframeMarket`)
3. Run `AwakenedWarframeMarket.exe`
4. In Warframe, hover an item and press **Alt+X**

Needs Windows 10/11 64-bit and the [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe), which most PCs with games already have. .NET is included, nothing else to install.

### "Windows protected your PC"

The app isn't code-signed yet, so Windows SmartScreen warns about it on the first launch. Click **More info → Run anyway**. You only need to do this once.

### Is it safe?

- The full source code is in this repository, and this zip was built from it by GitHub Actions, not on someone's PC. The build log is public, and you can confirm the zip came from it: `gh attestation verify {{ZIP}} --repo Deriol4/Awakened-Warframe-Makret`
- SHA-256: `{{SHA256}}`
- The app never touches the game: no memory reading, no injected code, no key presses sent to Warframe. It only takes a screenshot around the cursor when you press the hotkey, keeps it in memory and throws it away after reading
- It only connects to warframe.market, doesn't need admin rights and doesn't collect anything about you
