# Security Review — Nitrox

**Date:** 2026-05-30  
**Scope:** Full codebase — triggered by user reports of malware hiding in save folders

---

## Findings

### 🟣 CRITICAL — Unrestricted TypeNameHandling in save-file deserializer
**File:** `Nitrox.Server.Subnautica/Models/Serialization/ServerJsonSerializer.cs:24`  
**CVE class:** CWE-502 (Deserialization of Untrusted Data)  
**Confidence:** 10/10

`TypeNameHandling.Auto` was configured on the `Newtonsoft.Json` serializer **with no `ISerializationBinder`**. This allows any reachable .NET type to be instantiated by including a `$type` field in a JSON save file (`EntityData.json`, `WorldData.json`, `PlayerData.json`, `GlobalRootData.json`). An attacker crafts a save folder containing a poisoned JSON file and distributes it. When a victim loads the save, the server deserializes the file and instantiates arbitrary .NET types — enabling remote code execution without any user confirmation.

**This is the most likely root cause of the reported malware.**

**Fix applied:** Added `NitroxSerializationBinder` to `ServerJsonSerializer`, restricting type instantiation to types whose namespace begins with `Nitrox.`, `NitroxClient.`, or `NitroxPatcher.`. Any `$type` outside this allowlist throws a `JsonSerializationException`.

---

### 🔴 HIGH — `PostSaveCommandPath` allows arbitrary executable from save config
**File:** `Nitrox.Server.Subnautica/Services/SaveService.cs:39–55`  
`Nitrox.Model/Configuration/SubnauticaServerOptions.cs:86`  
**CVE class:** CWE-78 (OS Command Injection)  
**Confidence:** 8/10

`server.cfg` (which lives inside the save folder) exposed a `PostSaveCommandPath` property. The server executed the specified file path with `Process.Start` on every autosave. The only guard was `File.Exists()` — no path restriction, no allowlist, no user confirmation. An attacker distributing a crafted save folder could bundle a `server.cfg` pointing to a dropped executable and trigger it automatically on every save cycle.

This pairs directly with the CRITICAL finding above: the deserialization gadget could drop a file to a known path, then `PostSaveCommandPath` would execute it on the next autosave.

**Fix applied:** `PostSaveCommandPath` property and `ExecutePostSaveCommand()` method removed entirely. This feature had no safe way to be scoped to admin-only, non-imported configurations.

---

### 🟠 MEDIUM — Unrestricted TypeNameHandling in prefab cache deserializer
**File:** `Nitrox.Server.Subnautica/Models/Resources/Parsers/PrefabPlaceholderGroupsResource.cs:38`  
**CVE class:** CWE-502 (Deserialization of Untrusted Data)  
**Confidence:** 8/10

A second independent `JsonSerializer` instance used `TypeNameHandling.Auto` without a `SerializationBinder`. This serializer reads `PrefabPlaceholdersGroupAssetsCache.json` from `%APPDATA%\Nitrox\cache\` — a user-writable, predictable location. Any process running as the current user (e.g., malware installed via the CRITICAL vector above) could plant a crafted cache file to achieve persistent code execution on every Nitrox server startup, even after save files are cleaned up.

**Fix applied:** Same `NitroxSerializationBinder` applied to this serializer.

---

### 🟠 MEDIUM — Shell metacharacter injection in generated restore/update scripts
**Files:** `Nitrox.Launcher/Models/Services/BackupService.cs:244–320`  
`Nitrox.Launcher/ViewModels/UpdatesViewModel.cs:110–180`  
**CVE class:** CWE-78 (OS Command Injection)  
**Confidence:** 7/10

Both `CreateRestoreScriptAsync` and `CreateUpdaterScriptAsync` embedded filesystem paths directly into generated shell scripts via C# string interpolation without escaping shell metacharacters:

- **Windows/PowerShell:** `backupPath` and `extractPath` were placed inside PowerShell single-quoted strings (`'...'`). A path containing `'` (e.g., a user's home directory `/home/o'neil/`) would escape the string and allow injecting arbitrary PowerShell commands.
- **Linux/macOS/bash:** All paths were placed inside bash double-quoted strings (`"..."`). Characters like `$`, `` ` ``, `\`, and `"` in paths would enable variable expansion or command substitution.

**Fix applied:** Added `ScriptHelper.EscapeForPowerShell()` and `ScriptHelper.EscapeForBash()` helpers (`Nitrox.Launcher/Models/Utils/ScriptHelper.cs`). All path variables embedded in shell scripts now go through the appropriate escaping function before interpolation.

---

## Files Changed

| File | Change |
|------|--------|
| `Nitrox.Server.Subnautica/Models/Serialization/Json/NitroxSerializationBinder.cs` | **New** — type allowlist binder |
| `Nitrox.Server.Subnautica/Models/Serialization/ServerJsonSerializer.cs` | Added `SerializationBinder` |
| `Nitrox.Server.Subnautica/Models/Resources/Parsers/PrefabPlaceholderGroupsResource.cs` | Added `SerializationBinder` |
| `Nitrox.Model/Configuration/SubnauticaServerOptions.cs` | Removed `PostSaveCommandPath` |
| `Nitrox.Server.Subnautica/Services/SaveService.cs` | Removed `ExecutePostSaveCommand()` |
| `Nitrox.Launcher/Models/Utils/ScriptHelper.cs` | **New** — shell path escaping helpers |
| `Nitrox.Launcher/Models/Services/BackupService.cs` | Applied bash/PowerShell path escaping |
| `Nitrox.Launcher/ViewModels/UpdatesViewModel.cs` | Applied bash path escaping |
