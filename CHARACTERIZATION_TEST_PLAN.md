# Sprint 5 Characterization Test Plan

## Purpose

Freeze StackMeet's current observable behavior before Repository migration. Tests are behavior-first: IDs in `BEHAVIOR_CATALOG.md` state Given/When/Then outcomes and criticality, while `tests/characterization.test.js` exercises legacy behavior through an isolated Node VM harness.

## Execution

```powershell
& 'C:\Users\clim\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' --check app.js
& 'C:\Users\clim\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' js\storage\storage-smoke.test.js
& 'C:\Users\clim\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' tests\characterization.test.js
```

## Scope and priority

Critical cases protect competition integrity: division assignment, team rules, results, finals, awards, and XML/storage contracts. High cases protect workflow recovery: registration, search/sort, and reports. Medium/Low cases are catalogued for browser visual coverage and are not production changes.

## Coverage approach

- Node VM: IDs, dates/age/divisions, team rules, time parsing, results, final tie-breaks, award quantities, report filters, localStorage key persistence, XML export shape.
- Existing smoke test: disconnected `LocalStorageProvider` load/save/reset contract.
- Browser-runtime follow-up: XML import (`DOMParser`), download interaction, route rendering, print preview, interactive search/edit/delete, and final qualification/lane rendering.

## Regression protocol

1. Run checks before and after any refactor.
2. Report failing behavior IDs grouped Critical → High → Medium → Low.
3. Critical failures block the migration; do not update expected outcomes without tournament-owner approval.
4. Recompute production SHA-256 hashes for `app.js`, `index.html`, and `styles.css`; any difference is a Sprint 5 failure.
