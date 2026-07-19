# Sprint 11.0 — Competition Reporting Enhancement

## Scope

Sprint 11 extends the current report framework. It does not introduce a report engine, storage format, API, schema, or calculation path.

## Reused

- `js/reports/FinalsReportEngine.js` remains the report DTO, participant metadata, filter, classification, ranking, and placement source.
- `js/results/BestResultEngine.js` remains the only Best Time and scratch-classification source.
- The existing Finals report document renderer, print target, and CSV exporter now also render Preliminary definitions.
- Existing qualification snapshots identify Preliminary qualifiers; existing final sheets identify Finals finalists.

## Enhanced

- A Stage selector provides Final Results and Preliminary Results through the same report controls and layout.
- Overall Results and all-events Division Results use the StackTrack-style horizontal event board: `3-3-3`, `3-6-3`, `Cycle`, and `All Around` display side by side when present in the data.
- Results by Division and Event now group first by Division and then by Event, with each event list ranked fastest time first.
- Results by Division can render compact division sections that share pages when small and continue naturally when large.
- Results by Event, Missing Results, and Scratch Report work for either stage.
- Gap is always the competitor time minus the relevant champion time; it is never calculated from the previous place.
- Qualified Preliminary competitors and applicable Finals finalists receive the existing highlight treatment.
- Individual preliminary timesheets show Organization with Country on the existing participant-information line, retaining the two-timesheets-per-A4 portrait layout.
- The event filter is populated from competition data rather than a hard-coded event list.

## Intentionally unchanged

- Registration, entries, relay management, qualification rules and snapshots, BestResultEngine business rules, SQL/APIs/XML, and Timesheet print structure.
- Existing Top Performance, Organization Championship, Qualification Snapshot, Tie Resolution, and Medal Summary business rules.
- The legacy Competition builder remains available and was not rebuilt.
