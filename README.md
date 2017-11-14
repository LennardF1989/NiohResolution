# NiohResolution

NiohResolution adds support for any resolution to *Nioh: Complete Edition* on PC.

## How to use?

1) Download the latest release of NiohResolution.
2) Extract the ZIP to the root of the *Nioh: Complete Edition* game-directory. By default this would be `C:\Program Files\Steam\steamapps\common\Nioh`.
3) Run `NiohResolution.exe` and follow the instructions.
4) Start the launcher and set your resolution to 1920x1080.
5) Start the game and enjoy!

## How does it work?

Before doing anything, the game will be unpacked using [Steamless](https://github.com/atom0s/Steamless). This is required because the Steam DRM will otherwise not allow a modified executable.

Once unpacked, the patcher will look for the byte representation of the 1920x1080 resolution, and change all occurances to your desired resolution.

In theory, this patcher should work for any version of the game.
