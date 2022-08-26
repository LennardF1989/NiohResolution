# NiohResolution

NiohResolution adds support for any resolution to *Nioh: Complete Edition* on PC.

## How to use?

1) Download the latest release of NiohResolution.
2) Save `NiohResolution.exe` to the root of the *Nioh: Complete Edition* game-directory. By default this would be `C:\Program Files\Steam\steamapps\common\Nioh` for the Steam version.
3) Run `NiohResolution.exe` and follow the instructions.
4) Start the launcher, set the render resolution to High and set your resolution to 1920x1080.
5) Start the game and enjoy!

## How does it work?

Before doing anything, the game will be unpacked using [Steamless](https://github.com/atom0s/Steamless). This is required because the Steam DRM will otherwise not allow a modified executable. This step will be skipped for the Epic Games version.

Once unpacked, the patcher will look for the byte representation of the 1920x1080 resolution, change all occurances to your desired resolution and perform a handful of other patches to make sure the aspect ratio is maintained.

## Compatibility

The current patcher is compatible with Nioh v1.24.07 and potentially any future version of the game. 

If you are running an older version of the game, please use an older version of this patcher as well!