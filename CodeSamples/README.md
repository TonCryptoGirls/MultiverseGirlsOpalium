# Corrected Unity C# Samples

These are corrected copies of selected scripts from **Multiverse Girls: Opalium**. The original project files were not modified.

## Included files

- `GameManager.cs` — protects the singleton lifecycle and avoids applying camp healing twice per interval.
- `Chest/CardInventoryManager.cs` — adds duplicate-instance protection, UID-based deduplication, and null/index validation.
- `DataBase/NFTRewardSystem.cs` — validates input and responses, handles network failures, avoids logging wallet identifiers, escapes query values, and replaces the hardware identifier with a random installation ID.
- `DataBase/SaveSystem.cs` — replaces unsafe `BinaryFormatter` persistence with JSON and maintains a backup during writes.
- `FestivalBoss/FestivalBossManager.cs` — validates damage RPC senders, prevents duplicate boss rewards, and resets session state when leaving a room.

## Integration notes

These scripts reference other project types such as `CardStats`, `SaveData`, `SaveLoadManager`, `FestivalBoss`, and Photon PUN. They are intended to replace the corresponding files inside the existing Unity project, not to compile as a standalone project.

### Save migration

`SaveSystem.cs` now writes `game-progress.json`. It intentionally does not deserialize the old `save.dat` file because the previous `BinaryFormatter` format is unsafe. Existing test saves should be backed up before replacing the script. If preservation of old player saves is required, create a one-time migration in a trusted offline build and remove it after migration.

`SaveLoadManager` uses a separate `save.json` file for inventory data. The new filename avoids overwriting that file.

### NFT reward endpoint

The current server endpoint is preserved for compatibility. Query values are escaped and sensitive values are no longer printed to logs. A future server update should move the state-changing `check-and-reward` operation from GET to an authenticated POST request and enforce idempotency on the server.

### Multiplayer authority

The boss manager now accepts damage only on the master client and verifies that the RPC sender matches the claimed actor number. A production multiplayer game should additionally calculate or validate allowed damage on the authoritative side rather than trusting the amount supplied by a client.

## Before replacing project files

1. Back up the Unity project and current player save files.
2. Replace one script at a time.
3. Open Unity and allow scripts to recompile.
4. Check Inspector references, especially the NFT reward UI fields.
5. Test saving/loading, inventory restoration, NFT rewards, scene changes, multiplayer damage, boss defeat, reward delivery, and respawn.

No API keys, passwords, wallet secrets, or Photon credentials are included in this package.
