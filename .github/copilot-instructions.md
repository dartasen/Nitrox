# Nitrox – Copilot Instructions

Nitrox is an open-source multiplayer mod for the game **Subnautica**. It hooks into the game at runtime via HarmonyX patches and provides client/server networking on top.

## Build & Test

Building `NitroxPatcher` and `NitroxClient` requires a local Subnautica installation. It is auto-discovered via `Nitrox.Discovery.MSBuild`, or set `SUBNAUTICA_INSTALLATION_PATH` to override.

```sh
# Build the launcher (also builds server + patcher as dependencies)
dotnet build Nitrox.Launcher/Nitrox.Launcher.csproj -c Debug -r win-x64

# Run all tests (also requires game files to be present)
dotnet test Nitrox.Test/Nitrox.Test.csproj

# Run a single test class or method
dotnet test Nitrox.Test/Nitrox.Test.csproj --filter "FullyQualifiedName~ClassName.MethodName"
```

Supported runtime identifiers for the launcher: `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

## Architecture

### Project Map

| Project | Target | Role |
|---|---|---|
| `Nitrox.Model` | net10.0 | Shared models, packets, DI infrastructure, logging |
| `Nitrox.Model.Subnautica` | net472 + net10.0 | Subnautica-specific packets, extensions |
| `Nitrox.Assets.Subnautica` | shared project | Static assets (DLLs, language files, asset bundles) |
| `NitroxClient` | net472 + net10.0 | Client-side game logic, packet processors, Unity MonoBehaviours |
| `NitroxPatcher` | net472 + net10.0 | Entry point into Subnautica; all HarmonyX patches live here |
| `Nitrox.Server.Subnautica` | net10.0 | Standalone multiplayer server (console app with .NET Generic Host) |
| `Nitrox.Launcher` | net10.0 | Avalonia desktop UI for launching game + managing servers |
| `Nitrox.Test` | net10.0 | MSTest test project; has `InternalsVisibleTo` for all library projects |

### Runtime Flow

1. The **Launcher** (`Nitrox.Launcher`) starts and deploys Nitrox files to the game folder.
2. **`NitroxPatcher.Main.Execute()`** is called by the game on startup; it sets up assembly resolution and calls `Patcher.Initialize()`.
3. `Patcher.Initialize()` applies **persistent patches** immediately (menu-always-on), then registers `Apply`/`Restore` on `Multiplayer.OnBeforeMultiplayerStart` / `OnAfterMultiplayerEnd` for **dynamic patches**.
4. On multiplayer start, `NitroxServiceLocator.BeginNewLifetimeScope()` activates DI-scoped services. On exit, `EndCurrentLifetimeScope()` tears them down and invalidates `Cache<T>`.

### Networking

- Transport: **LiteNetLib** (UDP), framed into reliable/unreliable channels.
- Packets: all inherit from `Nitrox.Model.Packets.Packet`. Serialized with **Nitrox.BinaryPack** inside a `Packet.Wrapper` struct.
- New packets go in `Nitrox.Model.Subnautica/Packets/`.
- Server-side handlers implement `IPacketProcessor<TContext, TPacket>` (in `Nitrox.Server.Subnautica`).
- Client-side handlers implement the same interface (in `NitroxClient/Communication/Packets/Processors/`).

### Dependency Injection

Autofac is used throughout (except the Launcher — see below). Each project exposes an `IAutoFacRegistrar` implementation that registers its services. The container is initialized once; a **lifetime scope** is begun per multiplayer session.

In Unity MonoBehaviours (where constructor injection is impossible), use `NitroxPatch.Resolve<T>()` or `NitroxServiceLocator.LocateService<T>()`. The `NitroxServiceLocator.Cache<T>` helper caches singleton lookups and auto-invalidates when the lifetime scope ends.

### Launcher (`Nitrox.Launcher`)

The Launcher is an **Avalonia** desktop app (cross-platform, not WPF). It uses its own DI stack separate from the rest of Nitrox:

- **DI**: `Microsoft.Extensions.DependencyInjection` + `ServiceScan.SourceGenerator` (auto-registers services; not Autofac).
- **MVVM**: CommunityToolkit.Mvvm — `[ObservableProperty]`, `[RelayCommand]`, `ObservableValidator`.

#### ViewModel Hierarchy

```
ObservableValidator
└── ViewModelBase              ← all VMs; unregisters WeakReferenceMessenger on Dispose
    └── RoutableViewModelBase  ← main content screens; has ViewContentLoadAsync / ViewContentUnloadAsync lifecycle hooks

ObservableValidator
└── ModalViewModelBase         ← popup dialogs; implicit bool operator (true = accepted / no errors)
```

`ViewModelBase` has a debug assertion that throws if DI accidentally called the empty constructor instead of the dependency-injecting one.

#### Navigation

Navigation is purely message-based via `WeakReferenceMessenger` — there is no page stack:

```csharp
ChangeView(vm)          // sends ShowViewMessage        → MainWindowViewModel swaps ActiveViewModel
ChangeViewToPrevious()  // sends ShowPreviousViewMessage → MainWindowViewModel goes back
```

All modal dialogs go through `DialogService.ShowAsync<TViewModel>()`, which maps ViewModel types to Avalonia `Window` factories registered at startup.

#### Launcher ↔ Server IPC

The Launcher runs an in-process **Kestrel HTTP/2 server** (random port, written to a port file) hosting a **MagicOnion** gRPC service. When a server process starts it connects back to this endpoint:

```
Launcher (Kestrel + ServersManagement StreamingHub)
    ▲  AddOutputLine / SetPlayers (server → launcher)
    ▼  OnCommand                  (launcher → server via CommandQueue channel)
Server process
```

- Commands flow: `ServerEntry.CommandQueue` (Channel\<string\>) → `ServersManagement.HandleCommandLoopAsync` → `Client.OnCommand(cmd)`.
- Output flows back: server calls `AddOutputLine` / `SetPlayers` on the hub → updates `ServerEntry.Output` / `PlayerCount`.

#### `ServerEntry`

The central model for a save/server. It is an `ObservableObject` wrapping a save directory and holds all server settings (`Port`, `GameMode`, `AllowPvP`, etc.), live state (`IsOnline`, `PlayerCount`, `Output`), and a `CommandQueue` channel. Settings are persisted as JSON in the save folder.

Servers can run **embedded** (child process shown inside the launcher via `EmbeddedServerView`) or **external** (standalone process). The `preferEmbedded` flag in `IKeyValueStore` controls the default.

#### Design-Time Support

All ViewModels guard network/file operations with `if (!IsDesignMode)` (`IsDesignMode` is `Design.IsDesignMode` from Avalonia, imported via `GlobalUsings.cs` as `static GlobalStatic`).

#### CLI Args

| Invocation | Effect |
|---|---|
| `Nitrox.Launcher` | Normal UI startup |
| `Nitrox.Launcher --crash-report` | Opens `CrashWindow` with the last crash log |
| `Nitrox.Launcher instantlaunch <save> <player…>` | Skips UI, starts server immediately |

## Key Conventions

### Harmony Patches

- File naming: **`TargetClass_TargetMethod_Patch.cs`** (e.g. `Player_OnKill_Patch.cs`).
- Class declaration: `public sealed partial class Foo_Bar_Patch : NitroxPatch, IDynamicPatch` (or `IPersistentPatch`).
- Declare `TARGET_METHOD` using `Reflect.Method(...)` for compile-time–safe reflection:
  ```csharp
  public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Player t) => t.OnKill());
  ```
- **`IDynamicPatch`** – applied on multiplayer start, removed on multiplayer end.
- **`IPersistentPatch`** – applied once at game startup and never removed.
- Comment patches when the patching strategy or rationale isn't obvious (e.g. why Prefix instead of Transpiler).

### Packet Suppression

When a client applies state received from the server and that would re-trigger the same patch, suppress the outgoing packet with a `using` block:

```csharp
using PacketSuppressor<MyPacket>.Suppress()
{
    // apply state that would normally send MyPacket
}
```

### Code Style (enforced by `.editorconfig`)

- **No `var`** – always write the explicit type.
- **Braces everywhere** – even for single-line `if`/`else` bodies.
- **Explicit null checks** – do not use null propagation (`?.`).
- `UPPER_SNAKE_CASE` for all `const` fields.
- `camelCase` for private instance fields (no `_` prefix).
- `PascalCase` for types, public fields, and properties.
- File-scoped namespaces (`namespace Foo;`).
- Implicit usings are **disabled**; each project has a `GlobalUsings.cs`.
- `JetBrains.Annotations` is available under the `JB` alias (`extern alias JB`).

### Testing

- Framework: **MSTest** with **FluentAssertions** and **NSubstitute**.
- Use **Bogus** for generating fake test data.
- Do not use DI in tests — instantiate objects directly or mock with NSubstitute.
- Test classes mirror the source structure under `Nitrox.Test/`.

### Subnautica Source Code

Subnautica's `Assembly-CSharp.dll` is publicized at build time via `BepInEx.AssemblyPublicizer.MSBuild`, making all private/internal members accessible. Never upload or embed Subnautica source code; reference only the publicized assembly.

### Central Package Management

All NuGet package versions are managed centrally in `Directory.Packages.props`. Do not set `Version` on individual `<PackageReference>` items in project files.
