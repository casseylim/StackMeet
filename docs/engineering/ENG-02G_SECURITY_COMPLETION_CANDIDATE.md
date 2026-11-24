# ENG-02G — Security regression and completion candidate (draft)

Base: b763cc0bb17704803d46f6b2b18a619bcade73de. ENG-02A–F latest hosted checks passed on the stacked development branches. This candidate is not owner acceptance or release approval.

## Endpoint inventory and additional negative coverage

The reviewed machine-readable [endpoint matrix](../../tests/security-endpoint-matrix.json) covers 71 controller HTTP actions across 20 controller source files. The Node test compares exact method/template/source identities to route attributes and fails for unclassified changes. It is a source-level inventory, not proof of actual ASP.NET endpoint metadata or every possible routing configuration. The separate SignalR methods, negotiation, static shells and minimal routes are covered by prior tests or identified below, not counted as controller actions.

| Authority class | Actions |
|---|---:|
| admin | 36 |
| public-auth | 4 |
| session | 2 |
| account | 1 |
| account-read | 2 |
| account-manage | 2 |
| public-read | 7 |
| account-score | 1 |
| compatible-read | 7 |
| maintenance | 4 |
| compatible-manage | 4 |
| public-shell | 1 |

The isolated LocalDB/loopback suite probes 59 protected actions anonymously (401), all 36 admin actions with a non-admin account (403), maintenance key (401) and scoped legacy token (401), plus 4 maintenance mutation routes with a competition manager (403). Requests use synthetic fixtures and empty payloads; these are gate-rejection checks, not exhaustive valid-payload authorization proofs. Existing ENG-02E role, ENG-02B/C session and real WebSocket, ENG-02D privacy and ENG-02F header/VM tests remain. Hosted execution pending. Do not run these probes against production.

## Prior validated evidence

| Package | Latest validated commit | Hosted run |
|---|---|---|
| ENG-02A | 7783809 | [45402667050](https://github.com/casseylim/StackMeet/actions/runs/45402667050) |
| ENG-02B | cce04e5 | [45405727668](https://github.com/casseylim/StackMeet/actions/runs/45405727668) |
| ENG-02C1 | 4494294 | [45410058988](https://github.com/casseylim/StackMeet/actions/runs/45410058988) |
| ENG-02C2 | 4b2b253 | [45412479838](https://github.com/casseylim/StackMeet/actions/runs/45412479838) |
| ENG-02D | 5b893a1 | [45415436555 attempt 2](https://github.com/casseylim/StackMeet/actions/runs/45415436555) |
| ENG-02E | 9dc6e08 | [45421159045](https://github.com/casseylim/StackMeet/actions/runs/45421159045) |
| ENG-02F | b763cc0 | [45423674678](https://github.com/casseylim/StackMeet/actions/runs/45423674678) |

## Residual risk and required decisions

| Priority (proposed) | Risk / gap | Acceptance or mitigation |
|---|---|---|
| High | Unlisted results and branding are accessible by direct link; participant names and special divisions remain public | Owner-approved private/unlisted/public modes, consent and historical retention policy |
| High | Browser bearer/admin credentials remain accessible to same-origin scripts; CSP is defense in depth | Real browser validation, DOM audit, separately designed HttpOnly-cookie and CSRF migration |
| High | Authorization checks can race permission changes and writes | Review transactional resource authorization; no claim of atomic revocation |
| High | Release and recovery validation incomplete | Coordinated API/static deployment manifest, IIS preflight, rollback and restore rehearsal |
| Medium | Legacy competition sessions remain until expiry; password rotation does not revoke them | Plan migration and key/session retirement; verify legacy hash formats |
| Medium | SignalR process-local registry, reauthorization before sends, in-flight races | Confirm single-instance topology, load/reconnect and multi-instance design |
| Medium | Alias checks in API writes not global DB uniqueness | Authorized aggregate collision preflight and concurrency validation |
| Medium | External QR provider receives public link; resource CSP report-only | Local QR generation, real browser console/network inspection |
| Medium | Route-prefix middleware and per-action authorization remain | Explicit endpoint metadata/scheme migration and full role/action matrix |
| Medium | Remaining transitive dependency advisories and runtime host inventory | Review advisories and actual IIS runtime in release process |
| Medium | Public profile/career archive policy and historical scoring rules | Explicit owner decision; do not rewrite certified evidence |

## Acceptance gate

Candidate security implementation can be reviewed when ENG-02G CI passes, but not released solely on that evidence. Record owner acceptance of ENG-00 and Phase 1, approval of listed-only profile links and remaining publication policy, real-browser tests for EN/MS/zh-Hans, operator and admin sign-in, token recovery, scoring and print workflows, concurrent and sustained load, dependency/runtime review, production release scope and recovery approval. Do not merge master or deploy without explicit authorization. No schema, scoring, certificate or production changes in ENG-02G.
