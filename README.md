# RADLER D3D9 Probe (x86)

This is a FIRST GRAPHICS-HOOK TEST for The Amazing Spider-Man 2, not the finished multiplayer mod.

## What the uploaded files establish

- `Game.exe` is PE32/x86 and imports `d3d9.dll` (`Direct3DCreate9`, `D3DPERF_SetMarker`).
- `Game.exe` loads `Data\\GameLogic.dll` dynamically.
- `GameLogic.dll` is PE32/x86 and exports `GLGetLogicFn` and `GLInitAPIFn`.
- `VideoDetection.dll` is a separate PE32/x86 DLL; it imports D3D9/D3DX9 and exports hardware/video helper functions. It is not the game's `d3d9.dll` proxy.

## What this probe does

It acts as a local `d3d9.dll` proxy and hooks the Direct3D 9 device `EndScene` call. It draws a translucent stick-figure diagnostic marker in screen space. This checks whether the game accepts a D3D9 proxy and whether drawing can be injected into its frame.

**Important:** the marker is not yet a Spider-Man model and is not positioned in world space. It is a deliberate first test before attempting to hook the game's camera/world matrices and spawn or render a real mesh. No game memory is modified by this first test.

## Build

Extract the ZIP contents directly into the root of your existing RADLER GitHub repository. The workflow has a unique filename (`build-d3d9-probe.yml`) and will not overwrite your current launcher workflow. GitHub Actions builds an x86 DLL on Windows. Artifact name: `RADLER-D3D9-Probe-x86`.

## Test (after building)

1. Back up the original game folder.
2. Confirm there is no existing `d3d9.dll` in the game root. Do not overwrite an existing mod/proxy.
3. Copy the built DLL into the same directory as `Game.exe` and rename it to `d3d9.dll`.
4. Launch the game. If it starts, look for a translucent cyan stick figure near the right side of the screen.
5. If the game fails to start or the marker does not appear, remove the proxy DLL; the original game files remain untouched.

The proxy currently forwards the two D3D9 exports observed in the uploaded `Game.exe`. If another module requires additional D3D9 exports, the proxy must be extended before use.

## Next milestone

After confirming the hook, replace the screen-space marker with a real world-space render path. That requires reverse-engineering the game's camera matrices, object/model resource format and render timing in `Data\\GameLogic.dll`; the current files do not yet establish a safe spawn function or a ready-to-use Spider-Man mesh API.
