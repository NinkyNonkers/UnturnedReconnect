# UnturnedReconnect
Automatic reconnect mod for the Unity 3D, mono-based game Unturned

## Usage

This serves as a proof of concept for bypassing the strange implementation of BattlEye assembly checking. As the user is kicked at questionably long intervals for un-whitelisted assemblies loaded, the BattlEye client can just be reset on every new connection. This mod does this seamlessly by resetting the connection without reloading the level, workshop and other rendered data.
