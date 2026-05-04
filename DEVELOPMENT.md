# DEVELOPMENT.md

How to build, test, and extend the Wallaby Hop plugin.

## Quick start

```powershell
# Clone
git clone git@github.com:Leonardboeker/NC_Hops_Rhino_Plugin.git
cd NC_Hops_Rhino_Plugin

# Build the plugin (outputs to src/.../bin/Debug/net48/WallabyHop.gha)
dotnet build src/DynesticPostProcessor/DynesticPostProcessor.csproj

# Run tests (always before pushing)
dotnet test tests/DynesticPostProcessor.Tests/DynesticPostProcessor.Tests.csproj

# The post-build step copies the .gha to %APPDATA%\Grasshopper\Libraries\
# Close Rhino first if it has the file locked.
```

Build target: **.NET Framework 4.8**, Rhino 8 SDK referenced from `C:\Program Files\Rhino 8\`.

## Repo layout

```
src/DynesticPostProcessor/
  Components/                       Grasshopper-side wrappers (thin)
    Operations/                       drilling, sawing, milling, etc.
    Korpus/                           cabinet generator
    Drawing/                          layout + PDF export
    Export/                           .hop file assembly + analyzer
    Nesting/                          part/sheet nesting
    Utility/                          tool DB, label, layer scan
  Logic/                            Pure NC-string + math layer
    DrillLogic.cs   SawLogic.cs     ContourLogic.cs   EngravingLogic.cs
    PocketLogic.cs  SlotLogic.cs    CircPathLogic.cs  DrillRowLogic.cs
    BlumHingeLogic.cs    FormatCutLogic.cs    FixchipLogic.cs
    CabinetPlanner.cs    MaterialListBuilder.cs    HopAnalyzer.cs
  AutoWire.cs                       slider/value-list auto-wiring
  IconHelper.cs                     embedded PNG loader
  MachineConstants.cs               magic numbers, single source of truth
  NcStrings.cs                      WZB/WZF/WZS tool calls + Bohrung/FreeSlot
  PluginConfig.cs                   env-var / config-file path resolution
  PreviewHelper.cs                  Brep/wire viewport helpers

tests/DynesticPostProcessor.Tests/
  *.cs                              xUnit-style tests with NUnit runner
  *.csproj                          re-includes Logic/*.cs via <Compile Include>
                                    so internals are visible to tests

LOCAL/                              gitignored — machine details, research
.planning/                          gitignored — work plans, research notes
```

## Architecture

### The pure-logic pattern

Every operation component splits cleanly:

```
HopXxxComponent (Components/.../HopXxx.cs)
  - Reads inputs from Grasshopper IGH_DataAccess
  - Validates (toolNr > 0, etc.)
  - Builds Rhino-typed previews (Brep, Line)
  - Calls XxxLogic.Generate(input)              <- pure function
  - Routes result.Lines to Grasshopper output

XxxLogic (Logic/XxxLogic.cs)
  - No Rhino dependency, no Grasshopper dependency
  - Takes plain doubles/structs as input
  - Returns plain List<string> (NC lines) + diagnostic data
  - Fully testable — see XxxLogicTests.cs
```

This split is the reason 119 tests can verify output correctness without ever booting Rhino.

### Component lifecycle

1. `RegisterInputParams` declares parameters (kept in English)
2. `RegisterOutputParams` declares outputs
3. `AddedToDocument` runs `AutoWire.Apply` to wire matching slider/value-list components on canvas drop
4. `SolveInstance` is called every time inputs change — **must be idempotent**
5. Preview overrides (`DrawViewportMeshes`, `DrawViewportWires`) render the cached `_previewVolumes` / `_approachLines`

### MachineConstants vs PluginConfig vs NcStrings

| File | Holds | Changes when |
|---|---|---|
| `MachineConstants.cs` | Default depths, clamp radius, header field strings | Machine setup changes |
| `PluginConfig.cs` | Per-rechner paths (template, tool DB) | Different computer / installation |
| `NcStrings.cs` | NC macro string builders (WZB/WZF/WZS, Bohrung, FreeSlot) | Never (machine protocol) |

If the same magic number appears twice, it belongs in `MachineConstants`. If a path is hardcoded, it belongs in `PluginConfig`.

## Adding a new operation

Vorlage: copy an existing minimal one like `HopDrillComponent` + `DrillLogic`.

1. **Define the input/output structs** in `Logic/NewOpLogic.cs`:
   ```csharp
   internal struct NewOpInput { public double X, Y; public int ToolNr; ... }
   internal struct NewOpResult { public IReadOnlyList<string> Lines; ... }
   ```
2. **Write the pure generator**:
   ```csharp
   internal static NewOpResult Generate(NewOpInput input)
   {
       var lines = new List<string>();
       lines.Add(NcDrill.ToolCall(input.ToolNr));   // or NcSaw, NcFmt
       lines.Add(/* macro string */);
       return new NewOpResult { Lines = lines, ... };
   }
   ```
3. **Add tests** in `tests/.../NewOpLogicTests.cs` covering at minimum:
   - One full-string snapshot (basic case)
   - Each input parameter's effect (one test each)
   - Default fallbacks for zero/negative inputs
   - Edge cases (empty input, invalid combinations)
4. **Add to test csproj** — `<Compile Include="..\..\src\...\Logic\NewOpLogic.cs" />`
5. **Run tests**: `dotnet test` — they should all pass before you touch the component.
6. **Build the component** in `Components/Operations/HopNewOp.cs`:
   - Mint a fresh GUID (use `[guid]::NewGuid()` in PowerShell), record it forever
   - Wire `SolveInstance` → `NewOpLogic.Generate(...)` → `DA.SetDataList(0, result.Lines)`
   - Build preview Breps from the same `result` data the logic returns (so preview and output never diverge)
7. **Sanity-check** in Rhino: drop the component, wire inputs, verify output matches a known-good `.hop`.

## Modifying an existing operation

1. Read the existing logic + tests first.
2. Make your change in the Logic file.
3. **Update or add tests for the new behavior**. If a test fails, that's the protocol changing — never silently update the snapshot without thinking.
4. Run `dotnet test`.
5. Update the corresponding component if the input/output shape changed.

## Component GUIDs registry

| Component | GUID |
|---|---|
| HopDrill | `2a763260-a3c1-4231-8ed0-cd0085267c94` |
| HopDrillRow | `5c0d3e4f-6071-8901-cdef-012345678901` |
| HopSaw | `c8d2f1a3-4b7e-4c9d-a1f5-2e3b6d8c0f14` |
| HopFreeSlot | `6f5e6bd3-18f9-44e5-b90b-33be8ce95bcf` |
| HopGrooveSlot | `4b9c2d3e-5f60-7890-bcde-f01234567890` |
| HopContour | `e2902790-ccf6-4880-b284-80e0110f1e71` |
| HopEngraving | `d3a19f7c-5b2e-4d8a-b6c1-9f0e2a4c7d83` |
| HopRectPocket | `6e2f23b6-557f-46a1-80a7-41feebc7982d` |
| HopCircPocket | `795d39f9-23ad-4499-966e-583a3e17439e` |
| HopCircPath | `7beb0809-a67e-485b-913f-ebae9bd50294` |
| HopBlumHinge | `6d1e4f50-7182-9012-def0-123456789012` |
| HopFormatCut | `3a8b1c2d-4e5f-6789-abcd-ef0123456789` |
| HopFixchip | `7e2f5061-8293-0123-ef01-234567890123` |
| HopKorpus | `a3b7c1d2-e4f5-6789-0abc-def123456789` |
| HopAnalyzer | `9e4f1a2b-c3d5-4e6f-8a7b-0c1d2e3f4a5b` |
| HopLabel | `a7c3e912-5d8f-4b2e-9061-7f42d8b5c130` |
| HopToolDB | `c4e2f851-7b3d-4a9c-b602-3e91f7d8a043` |

These are immutable. Do not change them.

## Pre-commit hook

A pre-commit hook ships in `tools/install-hooks.sh`. Run it once after cloning:

```bash
bash tools/install-hooks.sh
```

The hook blocks commits that:
1. Fail `dotnet test`
2. Contain a HOPS dongle ID pattern (`/\b\d-\d{7}\b/`)
3. Add German umlauts to `.cs` files outside `Logic/` (where machine literals live)

Bypass only with `git commit --no-verify` and only if you know what you're doing.

## CI

`.github/workflows/test.yml` runs `dotnet test` on every push and PR. Test project doesn't reference Rhino DLLs, so CI runs without a Rhino install.

## Releasing (manual, not yet automated)

1. Bump version in `src/DynesticPostProcessor/DynesticInfo.cs` and `manifest.yml`
2. Commit + tag: `git tag v0.x.0 && git push --tags`
3. Build Release: `dotnet build src/.../DynesticPostProcessor.csproj -c Release`
4. Yak package: `cd src/.../bin/Release/net48 && yak build` (needs Rhino installed for `yak` CLI)
5. Upload `.yak` to GitHub release manually. Yak-in-CI is deferred until the Rhino-DLL-on-runner question is solved.

## License & attribution

(Add LICENSE file when you decide on a license. Currently unlicensed = all rights reserved.)
