# UAEZP Synthesis Patcher

UAEZP Synthesis Patcher is a Synthesis port and evolution of the Ultimate Automated Encounter Zones Patcher xEdit workflow for Skyrim Special Edition.

It scans the winning records in the load order and can:

- assign one of UAEZP's nine dummy encounter zones to `CELL` records that currently lack `XEZN`;
- assign a dummy encounter zone to `WRLD` records that currently lack `XEZN`;
- add `Disable Combat Boundary` to `ECZN` records while preserving their existing flags and data.

Existing encounter-zone links are preserved, including non-null links that cannot currently be resolved. Deleted winning records are skipped. Changes are written as minimal overrides based on the winning record, so downstream lighting, water, overhaul, and compatibility changes are retained.

This is an initial release. The core CELL, WRLD, and ECZN behavior has been validated in xEdit, in Synthesis, and in game, but large and unusual load orders may expose edge cases.

## Requirements

- [Synthesis](https://github.com/Mutagen-Modding/Synthesis)
- the original `UAEZP.esp` source plugin from a supported Easy or Hard UAEZP distribution
- exactly one supported UAEZP source variant active in the load order

The two supported distributions both use the filename `UAEZP.esp`. The patcher distinguishes them by validating the nine `DummyEncounterZone0` through `DummyEncounterZone8` records:

- Easy minimum levels: `3, 5, 7, 9, 11, 11, 13, 15, 17`
- Hard minimum levels: `10, 15, 20, 25, 30, 35, 40, 45, 50`

The dummy zones must use local FormIDs `000800` through `000808` and match the expected common schema. Missing, duplicate, deleted, or unfamiliar source records cause a clear validation failure. The patcher does not generate replacement dummy zones.

Do not enable an old generated xEdit output such as `EncounterZonesEasy.esp`, `EncounterZonesHard.esp`, or an equivalently named previous UAEZP patch alongside the Synthesis output. Keep the required source `UAEZP.esp` enabled.

## Installation

Download `UAEZPSynthesisPatcher.synth` and open it with Synthesis. The installer points Synthesis at the executable patcher project in this repository.

For initial testing, add the patcher to its own Synthesis group. A group's name becomes its generated ESP filename, so do **not** name the group `UAEZP.esp`; that would collide with the required source plugin. A suitable group name is:

```text
UAEZP-Patcher
```

which produces:

```text
UAEZP-Patcher.esp
```

## Recommended workflow

1. Add the patcher to a dedicated Synthesis group.
2. Leave **Dry Run** enabled and run the group once.
3. Inspect the report for the detected source variant, validated dummy zones, scan counts, and planned changes.
4. Disable **Dry Run** without changing the other settings.
5. Run the same group again to create the overrides.
6. Optionally inspect the generated plugin in xEdit. For modified CELL and WRLD records, `XEZN` should be the only intentional field addition. For ECZN records, `Disable Combat Boundary` should be the only intentional flag addition.

## Settings

| Setting | Default | Behavior |
| --- | --- | --- |
| **Assign Missing Cell Encounter Zones** | `true` | Assigns a UAEZP dummy zone to winning CELL records whose `XEZN` is null. |
| **Assign Missing Worldspace Encounter Zones** | `true` | Assigns a UAEZP dummy zone to winning WRLD records whose `XEZN` is null. |
| **Disable Combat Boundaries** | `true` | Adds `Disable Combat Boundary` to winning ECZN records that do not already have it. |
| **Dummy Zone Mode** | `DeterministicRandom` | Selects how missing CELL/WRLD links are assigned. `DeterministicRandom` produces a stable per-record selection using **Seed**. `First` always selects `DummyEncounterZone0`. |
| **Seed** | `38174` | Controls `DeterministicRandom` assignment. The same seed, load order, and settings reproduce the same assignments. |
| **Dry Run** | `true` | Performs discovery, validation, planning, and reporting without creating functional Skyrim overrides. Disable only after reviewing the report. |

## Load-order behavior

The patcher operates on the winning records visible to Synthesis at execution time. It does not copy from a record's original master when a later plugin has changed that record.

- Existing CELL and WRLD `XEZN` values are never replaced.
- Non-null unresolved `XEZN` links are treated as existing and preserved.
- Deleted winning records are not overridden or resurrected.
- ECZN flags and unrelated data are preserved when `Disable Combat Boundary` is added.
- Exterior CELL overrides contain only the required WRLD/block/sub-block structure and target CELL header; sibling cells and persistent/temporary child records are not copied.

The generated plugin should normally be late in the Synthesis patching and load-order chain so later generated plugins do not discard its additions. Compatibility ultimately depends on the behavior and ordering of every plugin in a particular load order.

No cell types, EditorIDs, or origin plugins are excluded.

## Deterministic assignment

`DeterministicRandom` maps each target FormKey independently to one of the nine validated dummy zones using SHA-256 algorithm version `v1`, the configured seed, the origin plugin filename, and the target local FormID. Candidate zones are sorted by local FormID before selection.

This means enumeration order and unrelated newly added targets do not reshuffle existing assignments. The same seed with the same load order and settings produces the same result. Changing the seed can change assignments.

Exact assignments are not expected to match the original Pascal script, which used an order-dependent sequential `Random()` stream.

## Reports

Dry Run reports the selected Easy or Hard source, algorithm and seed, all nine dummy-zone identities, CELL/WRLD/ECZN scan and eligibility counts, deleted skips, planned totals, assignment distribution, and compact origin-plugin and provenance diagnostics.

The provenance section is intentionally retained for the initial release because it is useful when diagnosing pre-existing encounter-zone state. Its plugin breakdowns and example lists are capped to avoid per-record log spam.

Apply mode reports planned and applied/verified counts. A missing context, target mismatch, conflicting output `XEZN`, failed field verification, or count mismatch stops the run instead of silently skipping a planned mutation.

## Differences from the original xEdit script

- deterministic per-FormKey assignment instead of sequential `Random()`;
- reproducible assignments controlled by **Seed**;
- existing `XEZN` values are preserved rather than replaced;
- deleted winning records are skipped;
- downstream winning-record changes are preserved;
- minimal Mutagen/Synthesis overrides instead of broad forwarding or deep copies;
- no unnecessary WRLD override when a winning worldspace already has `XEZN`;
- Dry Run validation, planning, provenance, and assignment diagnostics.

## Development

```bash
dotnet build
dotnet test
```
