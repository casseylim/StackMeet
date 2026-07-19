# Certificates

## Purpose
Create accurate, branded recognition documents from approved competition data.

## Observed Behaviour
No certificate-specific screen was located in this read-only pass; certificate capability remains unverified.

## Competition Workflow
Approve results → select certificate scope/template → preview recipient data → generate → print or distribute.

## Business Rules
- Recipients come from approved results/awards, never free-text copies of names.
- Certificate identity includes competition name, date, category/division, recipient, and award/placement.
- Reprinting must be deterministic and auditable.

## Operator Workflow
Choose template and result scope → review recipient list → generate a preview batch → print/export only after confirmation.

## User Experience Observations
Certificate generation benefits from a clear missing-data list (names, country, place, logo/template) before rendering.

## Data Model Recommendations
`CertificateTemplate`, `CertificateRun`, `CertificateRecipient`, immutable source snapshot, and generation status.

## Suggested SQL-native StackMeet Implementation
Use SQL-backed recipient projections and a template renderer; persist the template/version and source-result snapshot with each run.

## Possible Improvements over StackTrack
Support bilingual templates, QR verification links, bulk retry of failed documents, and a preflight proof sheet.
