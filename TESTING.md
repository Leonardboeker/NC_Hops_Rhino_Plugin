# TESTING.md — Blind Spots & the Complete Verification Catalog

> **The lesson this document exists for:** on 2026-07-09, all 152 unit tests
> were green, the live integration test reported 8/8 PASS — and HopBackplot
> had never drawn a single line in the viewport. The numeric outputs were
> correct; the *drawing* never happened, because Grasshopper silently skips
> `DrawViewportWires` on components without geometry parameters. Nobody had
> ever *looked*. **Numeric tests cannot see.** Every verification layer has
> a class of bugs it is structurally blind to. This catalog maps them.

Every failure class below includes at least one example that ACTUALLY
HAPPENED in this project. None of this is hypothetical.

---

## Part 1 — The five verification layers and what each one CANNOT see

```
Layer 0  Static code checks     (compile, unit tests, golden files, CI)
Layer 1  Grasshopper live       (components on a real canvas)
Layer 2  File acceptance        (HOPS/CAMPUS software reads the .hop)
Layer 3  Machine physical       (air cut, single block, scrap material)
Layer 4  Production monitoring  (regression discipline over releases)
```

A bug is only *impossible* when the layer that can see it has actually run.
Green at Layer 0 says nothing about Layers 1–4.

---

## Part 2 — The Blind-Spot Taxonomy (26 failure classes)

### A. Code layer — unit tests green, output still wrong

**A1. The snapshot locks the bug in.**
A snapshot test asserts "output == what the code produced yesterday". If
yesterday's output was wrong, the test *defends the bug*.
- *Happened here:* `SlotLogicTests` asserted the groove macro exactly as
  emitted — including the missing position (C1). Test green, machine file
  useless, for weeks.
- *Countermeasure:* every snapshot needs an external anchor. Ours is now
  `reference-hops/` (204 machine-generated files) — snapshots must match
  the REFERENCE shape, not just themselves. When writing a new emitter:
  first find the macro in a reference file, then write the snapshot.

**A2. The test covers the original, the bug lives in the copy.**
Duplicate implementations drift; tests usually cover only one.
- *Happened here:* the emit logic existed THREE times (Logic modules,
  KorpusPanel methods, Part/Sheet export inline builders). All CRITICAL
  bugs lived in copies two and three — exactly where the 119 tests
  couldn't see.
- *Countermeasure:* one implementation, thin callers. `NcExport.
  AssembleFile`, `NcFmt.F`, `NcDrill/NcSaw` helpers are now the single
  bodies. Grep for duplicated literals before writing a new one.

**A3. Pure logic correct, glue wrong.**
The tested function is fine; the component calls it with the wrong
argument, wrong order, or ignores part of the result.
- *Happened here:* `HopContourComponent` reads its inputs in scrambled
  index order (0,1,2,8,9,10,11,3,...) — works today, but any future
  param reorder corrupts the mapping invisibly.
- *Countermeasure:* golden files that exercise the full component-to-file
  path (they call AssembleFile the way HopExport does), plus Layer-1
  live checks reading real component outputs.

**A4. Number formatting: culture, sci-notation, rounding.**
`(0.1+0.2).ToString()` → "0.30000000000000004". `1e-16` → "1E-16".
German locale → "0,3". Any of these in an NC file = machine alarm.
- *Happened here:* raw `ToString(InvariantCulture)` in six emitters;
  SawLogic's midpoint math is a guaranteed producer of `-1.2E-16` on
  axis-aligned cuts (C3).
- *Countermeasure:* single formatter (`NcFmt.F`, 4-decimal round) +
  `NumericFormattingTests` feeding `3.55e-15` / `0.30000000000000004`
  through EVERY emitter and asserting no `\dE[-+0-9]` pattern.

**A5. Cross-component convention drift.**
Each component individually "correct", but they contradict each other.
- *Happened here:* Side conventions: HopContour +1=Left, HopSaw −1=Left,
  HopCircPath 1=inside — a flipped kerf is a scrapped panel (H5).
  ALSO: after unifying, the remark label still said "Right" for +1 —
  found only in the live test.
- *Countermeasure:* plugin-wide convention documented in README Gotchas;
  when changing a convention, grep ALL components for the concept
  (`side`, `Left`, `Right`) — including label/remark strings, not just
  the math.

**A6. Silent defaults masking wiring errors.**
`if (depth <= 0) depth = 19.0;` turns a broken expression into a real cut.
- *Happened here:* every operation component did this (H1). Saw: 0 → 19mm
  through-cut.
- *Countermeasure:* machine-relevant values error out; only cosmetic
  values may default. Rule: "the plugin never guesses a machining depth."

**A7. Case/whitespace-sensitive parsing of your own output.**
The analyzer scanned for `TIEFE:=` — the saw family emits `Tiefe:=`.
Depth checking was blind for every saw cut, slot, and circular pocket (C6).
Also: `TIEFE` substring-matched inside `ZUTIEFE`.
- *Countermeasure:* IgnoreCase + word-boundary regex + a test per macro
  family. When adding a macro, add its depth param to the analyzer scan
  AND a test proving the analyzer sees it.

### B. Grasshopper integration layer — code correct, canvas broken

**B1. Component draws nothing (the Backplot lesson).**
`GH_Component` derives preview-capability from its parameters. Only
Text/Number/Bool params → `IsPreviewCapable == false` → `DrawViewport*`
NEVER CALLED. No error, no warning — just silence.
- *Happened here:* HopBackplot, discovered only when a human said "zeig mal".
- *Countermeasure:* any component that draws must override
  `IsPreviewCapable => true` explicitly. **Every drawing component gets a
  screenshot in the acceptance run. No exceptions.**

**B2. Degenerate ClippingBox.**
A perfectly flat (zero-thickness) box is "valid" but gets rejected by
preview-bounds scanners and can be clipped by Rhino itself.
- *Happened here:* Backplot's box was flat at Z=0.
- *Countermeasure:* inflate boxes by ±1 in the degenerate axis.

**B3. Data-tree explosion.**
Two sources on one input → two values → the component solves TWICE →
two branches → duplicated operations downstream.
- *Happened here (live test):* slider AND panel both wired to SawKerf →
  the saw emitted every cut twice into two branches; HopJob then produced
  TWO .hop contents; Backplot parsed both.
- *Countermeasure:* acceptance run includes a branch-count check on every
  output (`branch_count == 1` unless deliberately treed). Consider
  runtime warnings when an input has >1 source.

**B4. AutoWire misfires.**
Auto-created sliders with wrong ranges/decimals silently quantize input.
- *Happened here (live test):* auto-sliders snapped 8.2 → 8 and 3.2 → 3
  (integer default). The component was fine; the input was pre-mangled.
- *Countermeasure:* AutoWire specs must set decimals for fractional
  defaults; acceptance run verifies a fractional value survives the
  auto-wired path.

**B5. Stateful components across solves.**
Rising-edge memories (`_lastExport`), cached content, static engine locks:
state that survives solves can starve, double-fire, or leak between
documents.
- *Happened here:* HopExport's rising-edge returned empty `hopContent` on
  every non-edge solve — the documented Export→Analyzer chain worked for
  exactly ONE solve, then starved (fixed with a content cache).
- *Countermeasure:* for every stateful component test the sequence:
  solve → re-solve (no change) → change input → re-solve. All four must
  produce sane outputs.

**B6. Old .gh files after an interface change.**
Changing input count/order breaks saved definitions silently or loudly.
- *Happened here (deliberate):* HopGrooveSlot gained inputs (Ebene,
  ShortenStart/End); HopSaw's Side flipped meaning. Old files need review.
- *Countermeasure:* keep a `bench.gh` canvas file with every component
  wired; open it after each build. Document breaking changes in the
  commit AND the README. GUIDs never change (identity), but IO changes
  still need a migration note.

**B7. Component fails to load at all.**
Duplicate GUID, exception in constructor/RegisterParams, missing icon
resource → component vanishes from the ribbon, sometimes with only a
line in the Rhino command history.
- *Near-miss here:* hand-incremented sequential GUIDs
  (`5c0d3e4f-6071-8901-...`, `6d1e4f50-7182-9012-...`) — one copy-paste
  away from a collision that would silently break saved files.
- *Countermeasure:* random GUIDs only (registry in DEVELOPMENT.md);
  acceptance run counts components in the ribbon (expected: 19).

**B8. The installed plugin is not the built plugin.**
The post-build copy fails silently-ish when Rhino locks the .gha. You then
test WEEKS of changes against a stale binary.
- *Happened here:* the "installed" .gha was from May 4th while the repo
  was two months ahead; the copy had failed on every build since.
- *Countermeasure:* hash-compare `bin/.../WallabyHop.gha` vs
  `%APPDATA%\Grasshopper\Libraries\WallabyHop.gha` as the FIRST step of
  any live test. (One `md5sum` line — cheap insurance.)

### C. File layer — .hop looks right, machine software disagrees

**C1. Encoding & line endings.**
ASCII only (umlauts become `?` or kill the parse), CRLF endings.
`Encoding.ASCII` silently mangles `ü/ß/°` in label vars.
- *Countermeasure:* ASCII enforcement in the writer; round-trip check
  opens with `encoding="ascii", errors="strict"`; label vars need
  validation before injection (open TODO).

**C2. Dialect details you can only learn from reference files.**
`CALL Fixchip_K ( VAL SPX:=` — with spaces, with VAL, with an optional
`/` prefix. Our old output `Fixchip_K (0,60,9.5,0)` appears in ZERO
machine files.
- *Countermeasure:* `reference-hops/` is the format oracle. New macro =
  grep the references first. The nc-hops Python lib (the only independent
  .hop parser in existence) reads every golden file in CI.

**C3. Header lies, VARS tells the truth.**
Machine files carry `;DZ=0` in the header and the real thickness in the
VARS block (`DZ := 19;Dicke Z`). Anything reading only the header is
misled — our analyzer's depth check was silently disabled for exactly
the korpus/nesting files (C4).
- *Countermeasure:* analyzer parses VARS first, header as fallback; a
  test mirrors the real reference file structure.

**C4. Macro parameter semantics ≠ what the name suggests.**
`EBENE` is a 0/1 flag, not a Z height (we wrote 19 into it). `ARAND` IS
the groove position, not a constant edge clearance. `SPZ` is HALF the
plate thickness. None of this is documented anywhere except in the
reference files and the machine's behavior.
- *Countermeasure:* every macro parameter we emit is documented in the
  component description WITH its reference-file evidence. Unknown
  parameter → don't guess, check references → if absent, air-cut test.

**C5. Operation ordering.**
Bucket sort forces WZB→WZF→WZS and merges same-tool blocks. Fixchip
declarations land after machining ops (verified compatible with
references — but any NEW non-WZ line class needs the same verification).
A part that gets cut free before its last operation moves = scrap.
- *Countermeasure:* ordering rules documented in README Gotchas; golden
  file 02 exercises the sort; physical cut-free-last verification is a
  Layer-3 item.

### D. Machine layer — file accepted, result wrong or dangerous

**D1. Coordinate-system convention.** Where is Z=0 — machine bed or plate
top? Everything we emit assumes plate coordinates (top at Z=DZ). Model
at the wrong Z → machine cuts air or full-depth. The analyzer now warns
when the deepest Z never reaches the plate region (M7) — but a warning
is not a guarantee.
**D2. Sign/mirror conventions.** Side/kerf left-right, WKLXY rotation
direction, mirrored parts (SPIEGELN) — a flipped sign machines a mirror
image. Only a physical cut proves the sign.
**D3. Tool table mismatch.** We emit tool NUMBERS; the machine maps them
to physical tools. ToolNr 12 being a saw blade vs an 8mm cutter is
invisible to every software layer. `HopToolDB` reads the .too database —
USE it rather than typing numbers.
**D4. Physical collisions.** Clamp radius (analyzer checks 25mm — the
REAL clamp footprint must be measured), vacuum pods, machine envelope,
tool length vs depth. Only partially modeled.
**D5. Feeds/speeds are implicit.** The plugin writes `_VE,_V*1` — the
machine resolves actual feeds from the tool magazine. A wrong magazine
entry burns or breaks; no file inspection can see it.
**D6. The reference format itself can drift.** HOPS 7.7 → 8.x may change
macro versions (_V5 → _V7 pattern is visible in the references).
Machine software updates require re-validation.

### E. Process & human layer

**E1. Stale files.** Rename `sheet_1` → `sheet_01`: the OLD file stays in
the machine folder and the operator can load it. (Exporters now write
atomically, but never delete abandoned names.)
**E2. Wrong file loaded.** Two similar jobs, operator picks by memory.
Version stamps in `;KOMMENTAR=` would help (open TODO, Phase C7).
**E3. Documentation lies.** README claimed the sheet exporter applies
transforms — it did not (C2). A false doc is worse than no doc: it ends
the reader's suspicion exactly where the bug is.
- *Countermeasure:* README component tables were regenerated FROM CODE;
  any interface change must touch README in the same commit (checked in
  review, not automated yet).
**E4. The demo IS a test.** "Zeig mal" found what 152 tests + a live
integration run missed. Schedule show-me sessions as deliberate
verification, not as courtesy.

---

## Part 3 — The Verification Catalog (what to actually run, layer by layer)

### Layer 0 — automated, runs on every commit (EXISTS ✅)

| Check | Tool | Status |
|---|---|---|
| Compile (no CS errors) | dotnet build | ✅ pre-commit + CI |
| 152 unit tests (logic snapshots vs reference shapes) | dotnet test | ✅ pre-commit + CI |
| 5 golden files, byte-exact (drill/saw+miter/contour/cabinet/nested) | GoldenFileTests | ✅ CI |
| Independent parser round-trip (nc-hops lib) | tools/roundtrip_check.py | ✅ CI |
| No sci-notation from any emitter | NumericFormattingTests | ✅ |
| Dongle-ID / umlaut / test-failure commit block | tools/pre-commit | ✅ local |

### Layer 1 — Grasshopper live acceptance (run after EVERY build that changes components)

**L1.0 — Binary freshness (FIRST, always):** hash-compare built vs
installed .gha. If different → the whole session tests a stale binary.

**L1.1 — Load check:** all 19 components present in the ribbon
(1|Drill … 9|Drawing). Missing component = load failure (B7).

**L1.2 — Drop check per component:** drop on canvas → AutoWire creates
inputs → no exceptions, no red. Verify a FRACTIONAL value (8.2) survives
the auto-wired slider (B4).

**L1.3 — Happy-path output check per operation component:** wire minimal
inputs, read `OperationLines`, compare against the known reference shape
(the live-test T1–T7 scripts document expected strings).

**L1.4 — Error-path check:** depth=0 → red with actionable message;
wrong dict into Korpus config input → named schema error; arc into
HopSaw → refusal. A component that fails SILENTLY on bad input is a bug.

**L1.5 — 👁 VISUAL check of every drawing component (the Backplot rule):**
capture a screenshot of EACH: HopDrill cylinders, HopSaw kerf boxes,
HopGrooveSlot band at ARAND offset, HopFixchip 25mm discs + CLAMP dots,
HopContour slot volume, HopBackplot full plot (colors per tool, sequence
numbers, plunge markers). *Parsing numbers is not seeing.* If the
screenshot shows nothing → IsPreviewCapable / ClippingBox / draw-code bug.

**L1.6 — State sequence check (stateful components):** HopExport/HopJob:
solve → re-solve unchanged → toggle edge → solve. hopContent must never
silently empty (B5).

**L1.7 — Tree discipline check:** every output `branch_count == 1` for
single-item inputs; deliberately double-wire one input and confirm you
NOTICE the duplication downstream (B3).

**L1.8 — Old-file check:** open `bench.gh` (canonical canvas with every
component wired from the previous version) — count red/orange components,
review each against the documented breaking changes (B6).

### Layer 2 — file acceptance (before ANY machine contact)

- **L2.1** Open the exported .hop in HOPS/CAMPUS on the office PC. Does it
  parse? Does the built-in visualization show the part correctly?
- **L2.2** If HOPS has simulation (CAMPUS V7+ material removal): run it,
  watch the order, watch for the part being freed before its last op (C5).
- **L2.3** Diff against a known-good reference file of the same operation
  class (Beyond Compare / git diff). Every unexplained difference is a
  question, not noise.
- **L2.4** Check the file in the machine-folder actually IS the fresh one
  (timestamp, `;NCNAME`) — stale-file trap (E1/E2).

### Layer 3 — machine physical (the only proof that counts)

Standard proveout sequence (industry practice, do not skip steps):

1. **Air cut:** raise part zero (or use a spoiler offset) so the deepest
   cut clears the material. Run the full program. Watch: order, approach
   positions, rapids, tool changes.
2. **Single block through the first minutes:** one command per cycle-start,
   finger on feed-hold, rapid override to minimum.
3. **Scrap material (MDF offcut):** full program, real cutting. Then
   MEASURE: hole positions (±0.1), groove position from edge (ARAND!),
   groove depth (NT), kerf side (H5 — did +1 cut LEFT of the line?),
   pocket dimensions, miter angle.
4. **Specific items for the current release:**
   - [ ] Groove: 2 grooves at different Y → physically at different Y?
   - [ ] Groove: EBENE 0 vs 1 — what changes on the machine? (undocumented!)
   - [ ] Fixchip: does the operator terminal show the clamp blocks as
     skippable (the `/` prefix working)?
   - [ ] Saw side=+1: kerf LEFT of the line looking along cut direction?
   - [ ] Nested sheet (translation-only): two parts land at their nested
     positions, no origin overlap? (C2 — THE critical one)
   - [ ] SPZ = half thickness confirmed correct for the clamps?
   - [ ] Blum cup: 35mm cup at correct edge distance, dowels correct side?
5. **First real part** at reduced feed, then measure before running a batch.

### Layer 4 — ongoing discipline

- Golden suite re-run on every commit (CI does this) — and re-APPROVED
  only with a reference-file or machine justification, never to silence CI.
- After every HOPS/CAMPUS software update on the machine PC: re-run
  Layer 2 + one Layer-3 air cut. Macro dialects drift (D6).
- Every new macro: reference-grep FIRST, snapshot SECOND, air-cut THIRD.
- Every new drawing component: IsPreviewCapable override + screenshot in
  the acceptance run (the Backplot rule).
- Keep `bench.gh` updated as the living old-file compatibility test.
- Quarterly: measure a real clamp, verify FixchipClampRadiusMm; verify
  tool table vs .too file.

---

## Part 4 — Current open verification debt (honest list, updated 2026-07-09 after the first full L0+L1 sweep)

| # | Item | Layer | Risk |
|---|---|---|---|
| 1 | **Air-cut validation of the entire release** (everything since the translation) | 3 | HIGH — nothing has touched the machine since the refactor |
| 2 | Nested-sheet translation on real material (C2 path) | 3 | HIGH |
| 3 | EBENE 0 vs 1 physical meaning | 3 | MED — we emit 0 by default, references use both |
| 4 | Saw side=+1 physical direction | 3 | MED — convention flipped this release |
| 5 | Fixchip SPZ=half-thickness + `/` skippability at the terminal | 3 | MED |
| 6 | HOPS/CAMPUS opens our files (L2.1 never formally done for the new emitters) | 2 | MED |
| 7 | Real clamp footprint vs 25mm constant | 3/4 | LOW-MED |
| 8 | ~~bench.gh canonical canvas~~ ✅ created 2026-07-09 (`bench/bench.gh`) | 1 | done |
| 9 | Label-var content validation (ASCII mangling, E1) | 0 | LOW |
| 10 | Version stamp in ;KOMMENTAR (stale-file defense) | 0 | LOW |
| 11 | ~~HopBackplot visual re-check~~ ✅ 2026-07-09 — draws (paths, colors, plunge markers) | 1 | done |
| 12 | **Contour/Engraving arc-direction + offset-side fix: live re-verify** (circle → all G03M, side=+1 → R−r/2 offset) | 1 | HIGH until verified — fix built, .gha reinstall pending |
| 13 | HopKorpus dict-schema guards never exercised live | 1 | LOW (unit-tested) |
| 14 | HopDrillRow description says default depth 13, auto-slider wires 6 | 1 | cosmetic |

Items 1–5 are one machine session (~2 hours with material). Until then,
every green checkmark in this repo is a Layer-0/1 statement only.

---

## Sweep log

**2026-07-09 — first full Layer-0 + Layer-1 acceptance sweep (via Rhino MCP)**

- **Layer 0:** 166 tests green; 5/5 golden files round-trip clean through the
  independent nc-hops parser.
- **L1.0:** installed .gha == built .gha (hash-verified before any testing).
- **L1.1:** 35 components load — and exposed that the DEVELOPMENT.md GUID
  registry only listed 19. Registry completed the same day.
- **L1.2/1.3:** FreeSlot, CircPocket, CircPath, DrillRow, FormatCut, BlumHinge,
  Contour dropped + happy-pathed; all emit reference-shaped macros
  (_Bohgx_V5 SPY from point, _saege_x_V7, _Topf_V5 negative cup depth, …).
- **L1.4:** depth=0 → hard error with actionable message, output emptied ✓.
- **L1.5:** all preview volumes drew (slot, pocket cylinder, path ring, cup
  circles, row markers, format-cut line); Backplot + StockSim verified earlier
  the same day (StockSim correctly split the stock when a through-cut freed a part).
- **L1.7:** every read output had branch_count 1 ✓.
- **FINDINGS (both invisible to all 166 unit tests — exactly classes A1/A3):**
  1. `HopContour`/`HopEngraving` derived arc direction from `arcSeg.Arc` (the
     struct), which ignores curve reversal → a plain circle emitted
     G03M + G02M: the machine would retrace the first half and never cut the
     second. Fixed: direction from curve-level tangent × to-center cross.
  2. `HopContour` kerf offset used `side * radius` with Rhino's raw sign
     convention → side=+1 (Left) offset OUTWARD on a CCW circle (right).
     The H5 side-unification had fixed SawLogic but missed this glue. Fixed:
     `OffsetOnSide` offsets, MEASURES which side the result landed on, and
     re-offsets if wrong — no trust in the convention.
