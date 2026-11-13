# Eveliet and Idearn summon coverage regression

`Issue41SummonCoverage.cs` is a trusted external RimWorldTestFramework adapter.
Build it separately from the runtime mod; it uses only framework and game
references and resolves CE through reflection. No framework Core edits or target
DLL rebuild are required.

The `aya.issue41` scenario checks the full owning-mod operation against installed
1.4–1.7 body XML, missing-owner and missing-body controls, repeat application,
partial precoverage, and preservation of original attack/custom groups. On the
selected native 1.6 engine it also checks all 12 resolved covered records per
installed summon, CE sharp/blunt core armor, a direct CE blunt-hit calculation
with a temporarily uncovered negative control, and the same runtime assertions
after ordinary save/reload. Temporary group mutations are restored in `finally`.
Eveliet's existing zero physical armor is preserved.

Use the framework's standalone-host setup instructions and public CLI. Configure
separate disposable profiles for Eveliet only, Idearn only, both, and Nearmare
only. Each needs Harmony, HAR, Aya Premise Core, CE, and this candidate addon in
that order around the selected race(s), with the candidate after CE. The
Nearmare-only profile must not load either owner; Idearn-only must not load
Nearmare. Select the same installed native DLCs deliberately. The test engine
is 1.6; parsing other versions' XML does not establish native 1.4/1.5/1.7 gameplay.

Set these additional local config fields, replacing the absolute paths:

```json
{
  "fixtureMode": "scenario",
  "parameters": {
    "candidate": "/absolute/candidate-mod-root",
    "evelietSource": "/absolute/installed-eveliet-root",
    "idearnSource": "/absolute/installed-idearn-root"
  },
  "fixtureContract": {
    "id": "aya-issue41-empty",
    "version": "1",
    "intent": "focused summon armor regression",
    "horizonTicks": 10000,
    "capabilities": ["generated-map", "environment", "save-reload"],
    "interventions": [
      "fixed-weather-temperature", "storyteller-disabled",
      "random-diseases-disabled", "random-mental-states-disabled",
      "setup-pawn-world-retirement", "ambient-wildlife-disabled"
    ],
    "invariants": {"population": 0, "mapSize": 100, "outdoorC": 21}
  }
}
```

Both source roots are needed even when one race is inactive: the scenario tests
that its full operation leaves that source XML untouched. Keep candidate XML and
the adapter immutable during a run. Use the framework's selected-root checks or
a unique test-only metadata identity to avoid shadowing an installed Workshop
copy. Do not alter third-party DLLs to bypass an input validation failure.

```sh
rwtf --config /absolute/profile.json build --version 1.6 \
  --adapter /absolute/repo/Tests/Issue41SummonCoverage.cs
rwtf --config /absolute/profile.json run --version 1.6 \
  --case aya.issue41 --ticks 600 --timeout 240 --rendered --tag unique-profile16
rwtf --config /absolute/profile.json validate /absolute/workspace/runs/unique-profile16
```

Inspect semantic checks, loaded package roots, original startup-through-exit
logs, Steam initialization failure, and process/link/input cleanup. A scenario
pass alone is not a successful host validation. In particular, the installed
HAR bundles Harmony: the host may reject it when the CLR resolves that shared
assembly from the separately selected Harmony package. Preserve that diagnostic;
do not claim a clean framework certificate or modify dependencies to silence it.
These cases do not claim normal colony survival, projectile hit probability, or
whole-mod compatibility.

## Results — 2026-11-13

Standalone RimWorld 1.6.4871 rev597 with installed CE 16.7.4.0: all four
scenario cases pass, with 432 passing checks and zero semantic failures
(Eveliet-only 108, Idearn-only 108, both 179, Nearmare-only 37). Each completes
ordinary save/reload. The owner-active raw XML cases apply successfully to all
installed 1.4–1.7 layouts, remain unchanged on repeat application, and preserve
existing groups. Inactive-owner cases leave the XML untouched.

Idearn's resolved core armor equals its pawn's sharp/blunt armor (18/47);
the direct CE blunt-hit calculation blocks the controlled damage. Removing
coverage for the negative control allows that damage, and the group list is
restored afterward. Eveliet retains zero physical armor. All 12 selected
records on each loaded body have coverage exactly once before and after reload.

All four host validations remain **invalid** because HAR's bundled Harmony
resolves from the separately selected Harmony package root. The scenario
results are not clean host-validation certificates. One initial Idearn-only
attempt stopped before the scenario on a `TicksAbs` startup error; it is
retained, and the unchanged retry passes without exempting that error.

Evidence is outside git at
`/Users/darren/codex_workspace/aya-issue41-fix-20261113/`: `summary.json`,
`tested-source-hashes.json`, and each `framework-*/runs/` directory retain the
commands, loaded roots, source/artifact identities, original logs, semantic
checks, case ledger, and host validation. Steam stayed uninitialized; all
process, Mods-inventory, and input cleanup audits pass. Dependencies and normal
profiles were not modified. The test profiles selected native Royalty, Anomaly,
and Odyssey; they do not claim Biotech/Ideology or full-stack validation.
