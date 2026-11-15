# RADLER Multiplayer Launcher v0.1

A first Windows launcher prototype for The Amazing Spider-Man 2.

## What this build does
- Lets you select and launch `Game.exe`.
- Provides HOST and CLIENT controls.
- Sends/receives small UDP HELLO/WELCOME messages over a LAN.
- Displays connection messages in a log.

## Important limitation
This is **not yet an in-game multiplayer mod**. It does not inject code, read game memory, spawn a second Spider-Man, or synchronize gameplay. It is only the first launcher + LAN transport smoke test.

## Build without installing tools on your PC
1. Create a new empty GitHub repository, e.g. `RADLER-Multiplayer-Launcher`.
2. Upload all files from this folder, preserving `.github/workflows/build.yml`.
3. Open the repository's **Actions** tab.
4. Select **Build RADLER Launcher**, click **Run workflow**.
5. When it finishes, open the successful run and download the artifact `RADLER-Multiplayer-Launcher-win-x64`.
6. Extract it and run `RADLER.Launcher.exe` on Windows.

## LAN test
1. Both PCs must be on the same local network.
2. On the host PC, click **HOST — Create room**.
3. Find the host PC's IPv4 address with `ipconfig` in Command Prompt.
4. On the client PC, enter that IPv4 address, keep the same UDP port, and click **CLIENT — Connect**.
5. If Windows Firewall asks, allow the app on Private networks. If no prompt appears, allow inbound UDP port 27025 for private networks.

The first version is intentionally minimal. The next stage is researching a safe game integration point for the actual in-game player synchronization.
