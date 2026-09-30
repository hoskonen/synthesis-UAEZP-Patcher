# Changelog

## Unreleased

- Added opt-in, XEZN-specific forwarding through actual winning FWMF-family CELL and WRLD overrides.
- FWMF forwarding can run independently in a late Synthesis group, recognizes compatibility-patch filenames containing a standalone `FWMF` token, and falls back to normal missing-zone assignment only when that separate option is enabled.
- Added separate CELL and WRLD forwarding counts to Dry Run and Apply reports.

## 0.1.0

- Initial Synthesis implementation of the UAEZP encounter-zone workflow.
- Added deterministic, seed-controlled dummy-zone assignment for missing CELL and WRLD `XEZN` links.
- Added optional `Disable Combat Boundary` handling for ECZN records.
- Added Easy and Hard `UAEZP.esp` schema validation.
- Added Dry Run planning and provenance diagnostics.
- Preserved existing encounter-zone links, deleted winners, and unrelated winning-record data through minimal overrides.
