# ENG-03B — State management: persistence projection boundary (draft)

Base: 203dd1c376d5d84cd244959d397f5043369181c7 (ENG-03A CI-passed). First small production refactor under ENG-03.

## Implemented

- Move three existing pure state operations (withoutLegacyStackers, legacyStateForSave, mergeConcurrentState) into static Repository methods. Keep app.js compatibility functions so existing callers and the save queue continue unchanged.
- Keep runtime roster/results, legacy JSON storage and additive team/notification/audit merge responsibilities explicit. No merge precedence, cloning, state format, result revision, ETag, deletion generation, async save order or provider transport behavior change.
- Reuse the already-loaded Repository script rather than introduce a new runtime dependency; bump Repository and app cache keys together. Shell script order remains the ENG-03A reviewed baseline.
- Retain all existing asynchronous deletion and retry scenarios; wire their extracted-function VM harness to the actual Repository. Static guard checks both wrapper delegation and projection logic, including SQL-owned results exclusion. Add direct projection/merge regression tests for input preservation, clone isolation, tombstones, ordering, local precedence, SQL slices and legacy missing-array behavior.

## Limitations and next steps

This is a narrow persistence-boundary extraction, not a full application state store migration. Mutable state, save queue, editor ownership, normalization and sync coordination remain in app.js. The merge policy and in-flight failure behavior intentionally remain unchanged; no claim of resolving concurrent permission or cross-competition transitions. Hosted CI and real-browser validation pending. ENG-03C will characterize and extract action families in stages; read-only print actions identified in ENG-03A require separate correction. Preserve scoring, historical results and certificate contracts.

## Mandatory production deployment guard (owner instruction)

As recorded on PR #93, never overwrite, upload, regenerate or delete the existing live IIS web.config. Exclude it from approved upload/sync/rollback manifests; securely back up and compare SHA-256 before and after deployment. Stop if preservation or verification cannot be guaranteed. Any change requires separate explicit owner approval. Do not log secrets. Deploy the coordinated changed static files and API only after release authorization and rollback validation. No deployment, schema change or master merge in this package.
