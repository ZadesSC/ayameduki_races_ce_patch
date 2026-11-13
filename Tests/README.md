# Solark compatibility regression

The shipped fix supports both Solark layouts: the legacy
`Solark_Dll.Solark_CompProperties_CommandExplosive` in 1.4–1.6, and
`Aya_Common.Aya_CompProperties_Skill` **with skillID `Solark_b_Skill_a`** in 1.7.
Both receive `Bomb_Solark`. Other Aya skills must remain untouched. A missing
intended skill must still fail rather than silently skip the replacement.

## Execution

Use the host's standalone launcher instructions and an installed
[RimWorldTestFramework](https://github.com/ZadesSC/RimWorldTestFramework).
Discover the installed CLI and selected SDK before building. This regression
uses its public `Scenario`, `Context`, bounded `Wait`/`Step`, checks, and JSON API.
It does not modify framework Core or Runner.

The standard batch Runner rejects CE's 1.6-only metadata on 1.7 and aborts
before scenarios when loading errors exist. For this explicitly requested
mixed-version diagnostic, `SolarkDiagnosticDriver` runs the SDK scenario through
a temporary GameComponent instead. This is in-game functional evidence, not a
standard `rwtf validate` result, performance certificate, or broad CE 1.7
compatibility claim. Every original loading/runtime error remains in the log.

Create an external config, with absolute paths appropriate to the host:

```json
{
  "schemaVersion": 1,
  "workspace": "/absolute/external/framework-output",
  "launcher": "/absolute/standalone/launch.py",
  "sdkSource": "/absolute/RimWorldTestFramework",
  "games": {
    "1.7": {
      "root": "/absolute/standalone/Game",
      "managed": "/absolute/standalone/Game/RimWorld.app/Contents/MacOS",
      "extensionReferenceMode": "runtime"
    }
  },
  "mods": []
}
```

Build using the discovered CLI. An empty build-time mod list means the test
adapter uses reflection instead of target assembly references; the diagnostic
launcher selects the real runtime dependencies separately.

```sh
rwtf --config /absolute/local.json build --version 1.7 \
  --adapter /absolute/ayameduki_races_ce_patch/Tests/SolarkRegression.cs
```

Run the diagnostic with the **same installed rwtf Python environment**:

```sh
/absolute/venv/bin/python /absolute/ayameduki_races_ce_patch/Tests/run_solark.py \
  --output /absolute/external/new-run \
  --game-dir /absolute/standalone/Game \
  --launcher /absolute/standalone/launch.py \
  --workshop '/absolute/Steam/steamapps/workshop/content/294100' \
  --bridge /absolute/external/framework-output/bridge/1.7 \
  --ability
```

The configured standalone Mods catalog must already expose Harmony, HAR, Aya
Premise Core, Solark, Solark EX, and installed CE. The script creates only two
owned temporary links, holds the shared framework host-game lease, uses an
absolute disposable profile and a 240-second process limit, and cleans its
process/links on ordinary exit or timeout. Keep the host alive through cleanup.
The ability wait also has a 600-tick/20-second limit. Existing Mods entries and
normal saves/config are preserved. No Steam installation is launched.

An existing Workshop installation of this addon can remain registered. A
separate **test-only package ID** (`local.solark.candidate`) prevents shadowing
that registration. Only its About metadata is wrapped; Patches, Defs,
Assemblies, and Textures link to the exact repository. CE's metadata, DLLs,
LoadFolders and patches are not altered. Check `loaded` in `semantic.jsonl` for
the actual selected roots and assemblies; the 1.7 engine enables its native
DLCs in this disposable quicktest profile.

The runner preserves startup-through-exit `launcher.log` (1.7 writes here rather
than the requested `game.log`), a plain-text copy, semantic checks and resolved
definitions, patched XML from version cases, commands/PID, source/input hashes,
exit code and cleanup audit. `verdict.json` requires zero failed semantic checks,
Steam initialization failure, and exactly the known upstream CE regular-Solark
error; any additional ERROR rejects the run. The script exits nonzero on a
failed verdict or changed inputs/Mods inventory.

Raw XML cases execute the game's real inner PatchOperationSequence against the
installed 1.4–1.7 source definitions, including unrelated-skill and missing-ID
controls. These are supplemental in-engine patch tests; only the selected 1.7
layout gets full definition loading and ability execution. There is no claim of
full native 1.4–1.6 game testing, save portability, or normal colony survival.
The combat fixture clears a remote area of the disposable map and creates a
player Solark elite and a nonimmune granite-block wall. It calls the real
self-target gizmo action and ticks the game normally through casting and the
explosion; it does not substitute a direct GenExplosion call or alter immunity.

## Actual results — 2026-11-13

Base: `origin/dev` at `6c0bb2243b0ddb47ae40a75f6748ff8d40127701`.
Engine: standalone **1.7.4985 rev1269**. CE: installed Workshop **16.7.4.0**
(1.6), unchanged. Solark/Aya/HAR: installed 1.7 content, with Solark EX enabled.
This mixed setup was explicitly requested because CE 1.7 was unavailable.

Evidence is retained outside git at
`/Users/darren/codex_workspace/solark-selector-20261113/`:

- `before/`: first full-game reproduction and resolved-definition dump.
- `before-sequence/`: full-game reproduction plus precise sequence diagnostics.
- `after/`: initial fixed run; ability succeeds, but a test wall-material error
  makes this **not** the clean acceptance run.
- `final/`: corrected fixture, **39 checks pass, zero semantic failures**, native
  process exits 0. `verdict.json` passes, and all three cleanup checks pass.
- `build-*.log`, `framework/builds/`, `sequence17.cs`, `ability17.cs`: compiler
  attempts and inspected game/mod code. The first adapter build had an ambiguous
  `Scenario` name, corrected to `Rwtf.Scenario` before launching.

Before changing the patch, the in-engine sequence reported **operation 7** as
failed for 1.7; 1.4, 1.5, and 1.6 applied successfully. The real game log separately
reported this addon's elite patch failure and CE's regular-Solark failure.
Earlier inventory/tactical/suppression components, CE stats, and body-size edits
survived. The resolved elite still used `BombSuper`; its armor was sharp
12.61111, blunt 25.22222, heat 0.5. Its tools were autopatched to ToolCE, but
retained original labels and blunt penetration 0.5 rather than the addon's
intended values. All seven CE apparel permissions and all six new damage
multipliers were absent. Heat already equaled the intended value, so its value
alone cannot establish execution of that skipped operation.

After the fix, all four version sequences apply. An unrelated Aya explosion
skill retains BombSuper; changing the intended skillID causes operation 7 to
fail as expected. Loaded 1.7 definitions show:

- The intended skill resolves to `Bomb_Solark`; CE defaults remain damage 550,
  stopping power 2, armor penetration 55.
- Armor sharp 18, blunt 40, heat 0.5, each appearing once.
- Exactly three ToolCE entries: left/right fist power 30, cooldown 1, blunt
  penetration 60; head power 50, cooldown 2, blunt penetration 15.
- All seven CE apparel permissions appear exactly once.
- Bomb_Solark 0, Fragment 0.3, Bomb_Secondary 0.2, Thermobaric 0.3,
  Flame_Secondary 2, PrometheumFlame 2, each appearing once.
- The actual explosion command is enabled, destroys the nonimmune wall
  (**510 → 0 HP**), leaves the caster alive at health **1 → 1**, and enters
  cooldown (**59877** ticks remaining after observation).

The final startup-through-exit log has **one ERROR**: CE's separate outdated
selector on `HAR_Solark_BaseNRPawn` in
`ModPatches/Solark Race/Patches/Solark Race/ThingDefs_Races/AlienRace_Solark.xml`.
There is no addon Solark failure, ability exception, or other ERROR. Remaining
warnings concern inactive catalog metadata, deliberately absent Idhale/Outerm
integration types, Steam being unavailable, and engine shutdown allocations.
This is not an error-free upstream installation. The external CE installation
was not patched to obtain the result. Input hashes and the existing Mods
inventory match before/after; the owned process stopped and links were removed.
