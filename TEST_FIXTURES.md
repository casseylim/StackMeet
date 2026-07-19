# Sprint 5 Test Fixtures

Fixtures are deliberately representative and are not production tournament data.

| Fixture | File | Purpose |
|---|---|---|
| Small Competition | `tests/fixtures/small-competition.json` | Direct test data for IDs, basic divisions, teams, and report metadata. |
| Medium Competition | `tests/fixtures/medium-competition.json` | Mixed division/country/Special/team/result coverage for browser scenarios. |
| Large Competition | `tests/fixtures/large-competition.json` | Scale and public-ID boundary contract for future browser-run tests. |
| Edge Cases | `tests/fixtures/edge-cases.json` | Date, time, duplicate membership, tie, scratch, and DNS boundaries. |

The Node suite creates isolated state from the current demo, then applies fixture-shaped data. It never reads or writes browser production storage. Browser-only XML import and UI lifecycle cases remain documented in the catalog for an automated browser runner in the next expansion.
