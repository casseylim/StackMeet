# SQL-Native Core Phase 1A API Specification

Phase 1A adds SQL-backed Competition and Stacker master data. It does not alter the browser runtime, CompetitionState API, registration UI, event modules, XML, printing, or business rules.

## Competition

`CompetitionResponse` is returned from all successful reads and creates. It contains `id`, `competitionCode`, `competitionName`, `venue`, `startDate`, `endDate`, `status`, `createdAt`, and `updatedAt`.

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/competitions` | 200 | Returns DTOs ordered by CompetitionCode. |
| GET | `/api/competitions/{id}` | 200 | Returns 404 when absent. |
| POST | `/api/competitions` | 201 | Returns a DTO and `Location` header. |
| PUT | `/api/competitions/{id}` | 204 | Returns 404 when absent. |
| DELETE | `/api/competitions/{id}` | 204 | Returns 404 when absent; 409 if Stackers still reference it. |

`CompetitionRequest` requires non-blank `competitionCode`, `competitionName`, `venue`, and `status`; `endDate` must be on or after `startDate`. Invalid requests return 400. `competitionCode` is trimmed and globally unique; a duplicate returns 409.

## Stacker

`StackerResponse` is used for every successful Stacker operation and never exposes the EF entity. It contains `id`, `competitionId`, `stackerCode`, `wssaId`, `firstName`, `lastName`, `gender`, `birthDate`, `country`, `club`, `isSpecialStacker`, `createdAt`, and `updatedAt`.

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/competitions/{competitionId}/stackers` | 200 | Returns DTOs ordered by StackerCode. |
| GET | `/api/competitions/{competitionId}/stackers/{id}` | 200 | Returns 404 for an unknown Competition or Stacker. |
| POST | `/api/competitions/{competitionId}/stackers` | 201 | Returns a DTO and `Location` header. |
| PUT | `/api/competitions/{competitionId}/stackers/{id}` | 204 | Returns 404 for an unknown Competition or Stacker. |
| DELETE | `/api/competitions/{competitionId}/stackers/{id}` | 204 | Returns 404 for an unknown Competition or Stacker. |

`StackerRequest` requires non-blank `stackerCode`, `firstName`, `lastName`, `gender`, and `country`. Optional text values are trimmed or stored as null. `stackerCode` is unique only within its Competition; a duplicate in the same Competition returns 409, while the same code in another Competition is valid.

## Persistence contract

All creation and update timestamps are assigned with `DateTime.UtcNow`. The migration creates `dbo.Competition` and `dbo.Stacker`, a unique index on `Competition.CompetitionCode`, a composite unique index on `(Stacker.CompetitionId, Stacker.StackerCode)`, and a restrictive foreign key from Stacker to Competition. Therefore a Competition cannot silently delete Stackers and Stackers cannot become orphans.
