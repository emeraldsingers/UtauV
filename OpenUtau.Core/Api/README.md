# OpenUtau Plugin API

This directory contains the public managed plugin API for OpenUtau.

As of this repository state, there are two practical managed extension surfaces:

1. `Phonemizer` plugins
2. Piano-roll `INoteBatchEditPlugin` plugins

This document describes the actual API currently implemented in the host. It is intended for plugin authors working against this codebase, not a theoretical future API.

## Scope

Currently documented here:

- managed phonemizer plugins loaded from .NET assemblies
- managed piano-roll batch edit plugins loaded from .NET assemblies

Not covered as managed plugin API:

- classic UTAU-style external editor plugins
- arbitrary custom windows, dock panels, or editor widgets
- general-purpose dependency injection or service registration

## Assembly Discovery

Managed plugin discovery is performed by `DocManager.SearchAllPlugins()`:

- host file: [../DocManager.cs](../DocManager.cs)
- managed plugin folder: `PathManager.Inst.PluginsPath`
- builtin managed assembly also loaded: `OpenUtau.Plugin.Builtin.dll`

Discovery behavior:

- only managed `.dll` files are considered
- all exported types are scanned
- exported `Phonemizer` subclasses are registered through `PhonemizerFactory`
- exported `INoteBatchEditPlugin` implementations are instantiated through `BatchEditPluginLoader`

Relevant files:

- [Phonemizer.cs](Phonemizer.cs)
- [PhonemizerFactory.cs](PhonemizerFactory.cs)
- [INoteBatchEditPlugin.cs](INoteBatchEditPlugin.cs)
- [INoteBatchEditAction.cs](INoteBatchEditAction.cs)
- [BatchEditPluginLoader.cs](BatchEditPluginLoader.cs)

## Target Framework And References

Example plugin projects in this repository target `net8.0` and reference `OpenUtau.Core`.

Minimal plugin project pattern:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\\OpenUtau.Core\\OpenUtau.Core.csproj" />
  </ItemGroup>
</Project>
```

Example projects:
- [OpenUtau.AIAutoTunePlugin.csproj](https://github.com/emeraldsingers/OpenUtau.AIAutoTunePlugin)

## Plugin Packaging

OpenUtau scans the `Plugins` directory recursively for managed assemblies.

Typical install layout:

```text
Plugins\
  MyPlugin\
    MyPlugin.dll
    MyPlugin.deps.json
    optional-runtime-assets...
```

If your plugin requires extra assets such as ONNX models, dictionaries, or config files, keep them next to the plugin assembly and copy them to output in the plugin `.csproj`.

## 1. Phonemizer API

Phonemizers are the most established managed plugin type in OpenUtau.

### Required Pieces

To create a phonemizer plugin:

1. Create a public non-abstract class deriving from `OpenUtau.Api.Phonemizer`
2. Add the `[Phonemizer(...)]` attribute
3. Implement `SetSinger(...)`
4. Implement `Process(...)`

Core API file:

- [Phonemizer.cs](Phonemizer.cs)

### Attribute

Use `PhonemizerAttribute` to expose metadata:

```csharp
[Phonemizer("My English Phonemizer", "EN MYTAG", author: "You", language: "EN")]
public class MyPhonemizer : Phonemizer {
    // ...
}
```

Attribute fields:

- `name`: display name
- `tag`: short identifier shown in UI
- `author`: optional
- `language`: optional IETF-like language code used for grouping

### Base Class Contract

Main methods on `Phonemizer`:

- `SetSinger(USinger singer)`
- `SetUp(Note[][] notes, UProject project, UTrack track)`
- `Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevs)`
- `CleanUp()`

The main algorithm entry point is:

```csharp
public abstract Result Process(
    Note[] notes,
    Note? prev,
    Note? next,
    Note? prevNeighbour,
    Note? nextNeighbour,
    Note[] prevs);
```

Important input semantics:

- `notes` is one note group: the leading note plus extender notes
- `prev` and `next` are adjacent groups when present
- `prevNeighbour` and `nextNeighbour` are only set when immediately adjacent in timing
- `prevs` contains the previous full extended note group and may be empty

Important output semantics:

- return phoneme aliases, not tone-mapped aliases unless you intentionally need that
- `position` is relative to the first note in the input group
- `expressions` may suggest phoneme expressions that the user can later override

### Useful Base-Class Helpers

`Phonemizer` exposes several helpers:

- `DictionariesPath`
- `PluginDir`
- `ToUnicodeElements(string lyric)`
- `MakeSimpleResult(string phoneme)`
- `MapPhoneme(string phoneme, int tone, string color, string alt, USinger singer)`

Timing helpers exist but are marked obsolete:

- `TickToMs(int tick)`
- `MsToTick(double ms)`

### Singer-Specific Resources

Load singer-specific files in `SetSinger(USinger singer)` using `singer.Location`.

Typical pattern:

- cache the current singer path
- lazily load dictionary/config files from singer folder
- avoid mutating the `USinger`

### Example Phonemizers

Start with these real implementations:

- simplest: [../DefaultPhonemizer.cs](../DefaultPhonemizer.cs)
- Japanese VCV: [../../OpenUtau.Plugin.Builtin/JapaneseVCVPhonemizer.cs](../../OpenUtau.Plugin.Builtin/JapaneseVCVPhonemizer.cs)
- Chinese CVV: [../../OpenUtau.Plugin.Builtin/ChineseCVVPhonemizer.cs](../../OpenUtau.Plugin.Builtin/ChineseCVVPhonemizer.cs)
- Arpasing: [../../OpenUtau.Plugin.Builtin/ArpasingPhonemizer.cs](../../OpenUtau.Plugin.Builtin/ArpasingPhonemizer.cs)

### Minimal Example

```csharp
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

[Phonemizer("Example Phonemizer", "EXAMPLE", language: "EN")]
public class ExamplePhonemizer : Phonemizer {
    public override void SetSinger(USinger singer) {
    }

    public override Result Process(
        Note[] notes,
        Note? prev,
        Note? next,
        Note? prevNeighbour,
        Note? nextNeighbour,
        Note[] prevs) {
        return MakeSimpleResult(notes[0].lyric);
    }
}
```

## 2. Piano-Roll Batch Edit Plugin API

This repository also exposes a managed plugin surface for contextual piano-roll actions.

These plugins contribute actions to the existing batch edit menus:

- `Notes`
- `Lyrics`
- `Reset`

Core API files:

- [INoteBatchEditPlugin.cs](INoteBatchEditPlugin.cs)
- [INoteBatchEditAction.cs](INoteBatchEditAction.cs)
- [NoteBatchEditContext.cs](NoteBatchEditContext.cs)
- [NoteBatchEditMenuCategory.cs](NoteBatchEditMenuCategory.cs)
- [PluginNoteBatchEditAdapter.cs](PluginNoteBatchEditAdapter.cs)

Host integration files:

- [../DocManager.cs](../DocManager.cs)
- [../../OpenUtau/Controls/PianoRollBatchEditPluginMenuLoader.cs](../../OpenUtau/Controls/PianoRollBatchEditPluginMenuLoader.cs)
- [../../OpenUtau/Controls/PianoRoll.axaml.cs](../../OpenUtau/Controls/PianoRoll.axaml.cs)

### Plugin Entry Point

Each assembly may export one or more public `INoteBatchEditPlugin` implementations:

```csharp
public interface INoteBatchEditPlugin {
    string Id { get; }
    string DisplayName { get; }
    string Description => string.Empty;

    IEnumerable<INoteBatchEditAction> CreateNoteBatchEditActions();
}
```

Requirements:

- public non-abstract class
- default constructible
- stable `Id`
- return concrete action instances from `CreateNoteBatchEditActions()`

### Action Contract

Each action implements `INoteBatchEditAction`:

```csharp
public interface INoteBatchEditAction {
    string Id { get; }
    string DisplayName { get; }
    string Description => string.Empty;
    NoteBatchEditMenuCategory Category { get; }
    int Order => 0;
    bool IsAsync => false;

    bool CanExecute(NoteBatchEditContext context) => true;
    void Execute(NoteBatchEditContext context);
    void ExecuteAsync(
        NoteBatchEditContext context,
        Action<int, int> setProgressCallback,
        CancellationToken cancellationToken);
}
```

Key points:

- `Category` controls menu placement
- `Order` controls ordering within a category
- `CanExecute` is evaluated at runtime for the current editor context
- async actions should set `IsAsync = true` and implement `ExecuteAsync`
- synchronous actions can implement only `Execute`

### Menu Categories

`NoteBatchEditMenuCategory` currently supports:

- `Notes`
- `Lyrics`
- `Reset`

### Execution Context

`NoteBatchEditContext` exposes the real host editing context:

- `Project`
- `Part`
- `Track`
- `Document`
- `PartNotes`
- `SelectedNotes`
- `TargetNotes`
- `HasSelection`

Behavior:

- `TargetNotes` means selected notes if any exist, otherwise the full part note list
- use `ExecuteCommand(UCommand command)` to participate in the host command system
- use `RunInUndoGroup(...)` to make your action undoable and validated correctly

### Recommended Editing Model

Batch-edit plugins should not mutate the document silently.

Preferred pattern:

1. inspect `context.TargetNotes`
2. compute the desired transformation
3. issue real `UCommand` instances
4. wrap the change in `context.RunInUndoGroup(...)`

This keeps your plugin aligned with OpenUtau's undo/redo and validation pipeline.

### Minimal Example

```csharp
using System.Collections.Generic;
using OpenUtau.Api;

public sealed class ExampleBatchEditPlugin : INoteBatchEditPlugin {
    public string Id => "Example.BatchEditPlugin";
    public string DisplayName => "Example Batch Edit Plugin";

    public IEnumerable<INoteBatchEditAction> CreateNoteBatchEditActions() {
        return new INoteBatchEditAction[] {
            new ExampleAction(),
        };
    }
}
```

```csharp
using OpenUtau.Api;

internal sealed class ExampleAction : INoteBatchEditAction {
    public string Id => "Example.BatchEditPlugin.ExampleAction";
    public string DisplayName => "Example Action";
    public NoteBatchEditMenuCategory Category => NoteBatchEditMenuCategory.Notes;

    public void Execute(NoteBatchEditContext context) {
        context.RunInUndoGroup("command.batch.note", true, () => {
            // issue UCommand instances here
        });
    }
}
```

### Real Example Plugins

Use these repository examples as the authoritative reference:

- [../../OpenUtau.BatchEditPlugin](../../OpenUtau.BatchEditPlugin)
- [../../OpenUtau.AIAutoTunePlugin](../../OpenUtau.AIAutoTunePlugin)

Useful concrete files:

- [../../OpenUtau.BatchEditPlugin/BatchEditPluginEntry.cs](../../OpenUtau.BatchEditPlugin/BatchEditPluginEntry.cs)
- [../../OpenUtau.BatchEditPlugin/Infrastructure/BatchEditActionBase.cs](../../OpenUtau.BatchEditPlugin/Infrastructure/BatchEditActionBase.cs)
- [../../OpenUtau.AIAutoTunePlugin/AIAutoTunePluginEntry.cs](../../OpenUtau.AIAutoTunePlugin/AIAutoTunePluginEntry.cs)
- [../../OpenUtau.AIAutoTunePlugin/Actions/AIAutoTuneAction.cs](../../OpenUtau.AIAutoTunePlugin/Actions/AIAutoTuneAction.cs)

### Discovery Rules

`BatchEditPluginLoader`:

- loads exported `INoteBatchEditPlugin` types
- instantiates them with `Activator.CreateInstance`
- rejects duplicate plugin IDs
- orders actions by `Category`, `Order`, then `DisplayName`

Practical implications:

- plugin types must be public
- they must have a public parameterless constructor
- plugin IDs must be unique across loaded assemblies

## Legacy External Plugins

OpenUtau still supports classic external plugins separately through the legacy plugin loader:

- loaded by `PluginLoader.LoadAll(PathManager.Inst.PluginsPath)`
- shown in the piano-roll legacy plugin menu

That path is not the same as the managed `OpenUtau.Api` contract described above.

If you are writing new C# plugins for this repository, prefer the managed APIs documented here.

## Stability Notes

Current stability by surface:

- `Phonemizer` API: mature, but still subject to change
- `INoteBatchEditPlugin` API: newer and narrower in scope

Known limitations of the managed plugin API:

- no general custom UI/widget extension model
- no formal ABI/version negotiation
- no dedicated package manifest format
- no sandboxing or isolation for managed plugins
- note batch edit plugins are piano-roll specific

## Practical Recommendations

For plugin authors:

- keep plugin entry classes public and simple
- use stable IDs
- keep runtime assets next to the plugin assembly
- favor explicit command-based edits over direct document mutation
- treat `OpenUtau.Core` APIs outside `OpenUtau.Api` as host internals that may change
- use repository examples before inventing your own patterns

## Quick Start Checklist

### Phonemizer plugin

1. Reference `OpenUtau.Core`
2. Derive from `Phonemizer`
3. Add `[Phonemizer(...)]`
4. Implement `SetSinger` and `Process`
5. Copy the built assembly into `Plugins`

### Piano-roll batch edit plugin

1. Reference `OpenUtau.Core`
2. Implement `INoteBatchEditPlugin`
3. Implement one or more `INoteBatchEditAction`
4. Use `NoteBatchEditContext` plus `UCommand` for edits
5. Copy the built assembly and required assets into `Plugins`

## Related Source Files

- [Phonemizer.cs](Phonemizer.cs)
- [PhonemizerFactory.cs](PhonemizerFactory.cs)
- [PhonemizerInstaller.cs](PhonemizerInstaller.cs)
- [INoteBatchEditPlugin.cs](INoteBatchEditPlugin.cs)
- [INoteBatchEditAction.cs](INoteBatchEditAction.cs)
- [NoteBatchEditContext.cs](NoteBatchEditContext.cs)
- [BatchEditPluginLoader.cs](BatchEditPluginLoader.cs)
- [PluginNoteBatchEditAdapter.cs](PluginNoteBatchEditAdapter.cs)
- [../DocManager.cs](../DocManager.cs)
