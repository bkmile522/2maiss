# 2maiss 🖱️🖱️

[![License: PolyForm Noncommercial](https://img.shields.io/badge/License-PolyForm%20Noncommercial-blue.svg)](https://polyformproject.org/licenses/noncommercial/1.0.0)

**2maiss** (pronounced *Two Mice*) is a lightweight dual-mouse touch solution designed for a specific "washing machine" rhythm game. Experience hardcore charts using just two standard USB mice.

### ✨ Key Features

* **Ultra Low-Cost**: No expensive touchscreens or custom controllers required. Use any two standard USB mice.
* **GUI Launcher**: Easy-to-use interface for mouse binding and one-click game launching.
* **Hardware-Level Separation**: Intercepts raw input to ensure left and right hands do not interfere.
* **Visual Calibration**: Built-in overlay to visualize and adjust touch zones.

---

## 🛠️ 1. Installation & Prerequisites (Mandatory)

Everyone must complete these steps before the first run.

1.  **Software Prep**:
    * **AquaMai**: Ensure **AquaMai** is installed in your game directory. Open `AquaMai.toml` and set:
        ```toml
        HideSubMonitor = true
        BaudRate = 115200
        ```
    * **com0com**: Install `com0com` and create a virtual port pair: `COM33` <-> `COM3`.
2.  **Extract the Tool**:
    * Extract the `2maiss` release `.zip` to a dedicated folder.
    * ⚠️ **IMPORTANT:** Do **NOT** move the `.exe` files out of this folder. `2maiss_Launcher.exe`, `2maiss_Backend.exe`, and `lut.png` must stay in the same folder.
3.  **Install Mod**:
    * Copy the `Package` folder from the tool directory into your **Game Root Directory**.
    * Verify `YourGameRoot\Package\Mods\2maiss.dll` and `UserData\2maiss\` assets (including `left.png`, `right.png`, `lut.png`) exist.

---

## ⚡ 2. Quick Play (Recommended for Most Users)

Follow this section to start playing immediately with optimal settings.

1.  **First-Time Setup**:
    * Run `2maiss_Launcher.exe`.
    * **Mouse Binding**: Go to **Hardware & Binding**, click **Start Mouse Binding**, and follow the prompts to identify your Left and Right mice.
    * **Game Path**: In **Play Settings**, browse and select your game's startup `.bat` file.
    * **Click the `Save Config`.**
2.  **Launch & Preferences**:
    * **Default Settings**: The initial configuration is already optimized. You don't need to change sensitivity or radius values to start.
    * **Mouse Acceleration**: Simulated acceleration is **Enabled** by default. If you prefer raw, linear movement, uncheck "Enable simulated Windows Enhance Pointer Precision" in **Play Settings**.
    * **Framerate**: It is highly recommended to run the game at **60fps** for better experience.
3.  **In-Game Control**:
    * Click **Start Game** in the launcher.
    * Press **F8** to activate. Your **System Cursor** will be locked and hidden—this is normal and indicates the tool has taken control.
    * Press **F8** again to release the cursor back to Windows.

---

## 📐 3. Advanced Configuration & Calibration

For users who need to fine-tune their experience.

### In-Game Visual Calibration (F11)
If you have set `HideSubMonitor = true` as instructed, the touch area should already align perfectly with the screen. You likely **won't need** to calibrate unless you have adjusted the screen position.

* **Open/Save**: Press `F11` in-game to toggle the calibration UI.
* **Adjust**: Use `Arrow Keys` to move the zone, and `-` / `=` to scale.
* **Speed**: Use `[` and `]` to change the adjustment increment.
* **Save**: Press `F11` again to save and exit.

### Troubleshooting FAQ
* **No Touch Response?**: Ensure `F8` is toggled on, `com0com` is active, and `lut.png` is present in `UserData\2maiss\`.
* **Buttons Remapping**: You can change which mouse buttons trigger **Tap** vs. **Wipe** in the Launcher's Play Settings.

---
## 🏗️ Building from Source

Due to copyright restrictions, the required Unity and MelonLoader assemblies are not included in this repository. 

**Required dependencies:**
* `MelonLoader.dll`
* `UnityEngine.CoreModule.dll`
* `UnityEngine.IMGUIModule.dll`
* `UnityEngine.ImageConversionModule.dll`

**Steps:**
1. Copy the DLLs listed above.
2. Place them in the `Frontend` folder.
3. Open the project in Visual Studio or run `dotnet build` to compile.

*Note: The Backend and Launcher are standalone and can be built right out of the box without any external proprietary DLLs.*

## 💖 Credits & Acknowledgements

* **[Mai2Touch](https://github.com/Sucareto/Mai2Touch)**: For the serial protocol reference.
* **Cursor Assets**: Cursor pack provided by [Kenney](https://kenney.nl/assets/cursor-pack).
* **Gemini 3.1 Pro**: Core logic and UI structure assistance. 100% vibe coding! 🤖
* **[stb_image](https://github.com/nothings/stb)**: Single-file public domain library used for loading the LUT image.
* **[mINI](https://github.com/pulzed/mINI)**: A tiny and fast header-only C++ INI file parser used for configuration management.
* **[MelonLoader](https://github.com/LavaGang/MelonLoader)**: The foundational modding framework that makes the frontend overlay possible.
---
*Disclaimer: This project is for technical research and educational purposes only. It is free and not affiliated with any commercial entities.*
