# UAEZP Synthesis Patcher

This project is a Synthesis port and planned evolution of the Ultimate Automated Encounter Zones Patcher xEdit script.

## Current status

Milestone 2 discovers and validates the active source plugin, scans winning load-order records, calculates deterministic changes, and either reports or applies that same validated plan.

Run with `DryRun=true` first. Dry Run is strictly read-only and creates no functional Skyrim overrides. With `DryRun=false`, the patcher writes the planned CELL, WRLD, and ECZN overrides and verifies the applied counts and required fields before reporting completion.

## Source plugin requirement

Exactly one supported `UAEZP.esp` must be active. The original mod's Easy and Hard distributions both use this filename, so they cannot coexist in a valid load order. The planner identifies them from their audited dummy-zone schemas:

- Easy minimum levels: `3, 5, 7, 9, 11, 11, 13, 15, 17`
- Hard minimum levels: `10, 15, 20, 25, 30, 35, 40, 45, 50`

Both supported variants must contain exactly nine `DummyEncounterZone0` through `DummyEncounterZone8` records at local FormIDs `000800` through `000808`, with the audited common fields and flags. Missing, duplicate, deleted, or unfamiliar source schemas fail before planning. Replacement encounter zones are not generated.

## Planning and apply behavior

- Winning `CELL` records, including interior and nested exterior cells, are scanned through Mutagen contexts. Missing `XEZN` links are assigned a planned dummy zone.
- Winning `WRLD` records with missing `XEZN` links receive a planned assignment. Worldspaces that already have `XEZN` do not produce planned overrides.
- Winning `ECZN` records lacking `Disable Combat Boundary` receive a planned flag addition.
- Deleted winning records are counted and skipped; nothing is resurrected.
- A non-null but unresolved CELL/WRLD `XEZN` is preserved for xEdit parity and reported as a warning count.
- Apply uses the winning Mutagen record context, so fields forwarded by compatibility patches remain present in the generated override.
- CELL and WRLD changes set only the planned `XEZN`; ECZN changes preserve the existing flag set and OR in `Disable Combat Boundary`.
- Existing `XEZN` values, including unresolved non-null links, are never replaced. Deleted winners remain skipped and are never resurrected.

No cell types, EditorIDs, or origin plugins are excluded.

For exterior CELL overrides, Mutagen's context-based `GetOrAddAsOverride` creates the required WRLD header and exterior block/sub-block path. It does not copy sibling cells or the target CELL's persistent/temporary children. A separately planned WRLD mutation reuses that structural parent and adds its planned `XEZN`.

## Deterministic assignment

`DeterministicRandom` uses SHA-256 algorithm `v1`. Each target is mapped independently from:

1. ASCII domain `UAEZP-DUMMY-ASSIGNMENT`;
2. version byte `1`;
3. signed 32-bit seed, little-endian;
4. invariant-lowercase origin plugin filename, UTF-8 with a 32-bit little-endian byte-length prefix;
5. target local FormID as unsigned 32-bit little-endian.

The first eight digest bytes are interpreted as an unsigned little-endian integer and reduced modulo nine. Candidates are sorted by local FormID first. Thus target enumeration order and unrelated inserted targets do not affect existing assignments.

`First` always selects the first candidate in that stable ordering (`000800:UAEZP.esp` for both supported variants).

## Settings

Synthesis generates settings for:

- `AssignMissingCellEncounterZones` (default `true`)
- `AssignMissingWorldspaceEncounterZones` (default `true`)
- `DisableCombatBoundaries` (default `true`)
- `DummyZoneMode` (`DeterministicRandom` or `First`; default `DeterministicRandom`)
- `Seed` (default `38174`)
- `DryRun` (default `true`)

## Dry-run report

The console/Synthesis log reports the selected Easy or Hard source, algorithm and seed, all nine dummy-zone identities and level data, CELL/WRLD/ECZN scanned and eligibility counts, deleted skips, planned totals, assignment distribution, and a compact top-ten origin-plugin breakdown.

It also attributes pre-existing relevant state to the plugin containing each current winning override—not to the record's origin/master plugin. The provenance section includes resolved and unresolved CELL/WRLD links, top-ten winning-plugin breakdowns, exact matches against the nine validated UAEZP dummy FormKeys, ECZN `Disable Combat Boundary` provenance, and up to 25 deterministic non-base-game CELL examples. These diagnostics do not affect eligibility or planned assignments.

Planning never accesses `state.PatchMod`. Apply consumes the captured winning contexts and exact assignments already stored in the plan; it does not rerun assignment or reconstruct records from their origin/master versions.

## Apply report

With `DryRun=false`, the concise apply report shows planned and applied/verified counts for CELL, WRLD, and ECZN. Any missing context, target mismatch, conflicting pre-existing output `XEZN`, failed field verification, or count mismatch stops the run with a clear error instead of silently skipping a planned mutation.

## Intentional improvements over `UAEZP.pas`

- deterministic per-FormKey assignment instead of an order-dependent shared random stream;
- no WRLD override when a winning worldspace already has `XEZN`;
- deleted winners are skipped;
- context-based minimal overrides instead of deep-copying cells, worldspaces, or nested children;
- strict source schema validation before planning.

Unlike the original xEdit script, the Synthesis patcher does not forward every processed WRLD and does not deep-copy CELL child groups. Re-running against the same load order and settings reproduces the same assignments.

The detailed source audit and future mutation plan are in [docs/UAEZP-XEDIT-AUDIT.md](docs/UAEZP-XEDIT-AUDIT.md).

## Build and test

```bash
dotnet build
dotnet test
```
