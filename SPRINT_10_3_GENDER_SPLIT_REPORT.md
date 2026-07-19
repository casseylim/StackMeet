# Sprint 10.3 Gender Split Report

## Delivered

Added one Settings option: **Separate Divisions by Gender**.

- **Off** (default): Special Stacker division generation is unchanged. For example, `SS 12 & Under L1` remains shared.
- **On**: only Special Stacker divisions are split with the requested suffixes: `SS 12 & Under L1 M` and `SS 12 & Under L1 F`.

The setting is stored in the existing competition settings state. No SQL schema or API change was made.

## Explicitly unchanged

- Standard division rules
- Age calculation
- Registration workflow
- SQL schema
- Dashboard polling
- Results, Relays, Awards, Reports, and Printing

## Verification

- JavaScript checks passed for source, IIS web-root, and storage scripts.
- Storage smoke tests passed.
- Characterization suite passed all 17 scenarios, including both off and on gender-split cases.
- Release build passed with 0 warnings and 0 errors.
- IIS publish package regenerated.
- Source and published `app.js` and `index.html` SHA-256 hashes match.
