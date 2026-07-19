# Module Extraction Roadmap

## Scale

- **Complexity:** Low, Medium, High, Very High.
- **Risk:** Low, Medium, High, Critical.
- **Effort:** Engineering days including characterization tests, integration, and review for one engineer familiar with the application.

| Order | Candidate | Complexity | Risk | Effort | Reason |
|---:|---|---|---|---:|---|
| 0 | Characterization-test foundation | High | Low | 5–8 days | Safety for all later work: state, XML, divisions, teams, results, finals, awards, routes, and print checks. |
| 1 | Pure Utilities | Low | Low | 2–3 | Few dependencies; exposes hidden coupling. |
| 2 | State schema/defaults | Medium | Medium | 3–5 | Separates demo data, defaults, persisted state, and UI-session state without changing formats. |
| 3 | StorageRepository/XML serializer | High | Critical | 6–10 | Cross-domain boundary; must preserve storage and XML exactly. |
| 4 | Translation | Medium | Medium | 3–5 | Large but relatively isolated; first preserve phrase behavior. |
| 5 | Settings and Events | Medium | High | 3–5 | Upstream of menus, results, finals, reports, awards, and print. |
| 6 | DivisionService | Very High | Critical | 7–12 | Affects nearly every competition workflow. |
| 7 | StackerService | High | High | 6–9 | Import/deletion spans divisions, teams, and results. |
| 8 | TeamService — Doubles | High | High | 5–8 | Sensitive membership and Child/Parent rules. |
| 9 | TeamService — Relay | High | High | 5–8 | Reuses membership primitives but retains Relay-specific rules. |
| 10 | ResultService primitives | High | Critical | 5–8 | Official outcomes depend on scratch, blank, attempt, penalty, and type semantics. |
| 11 | Prelim workflow | High | Critical | 6–10 | Compact IDs, event availability, saving, and missing times converge. |
| 12 | FinalsService | Very High | Critical | 8–14 | Qualifiers, sheet order, placements, and tie breaks. |
| 13 | AwardService | Medium | High | 4–7 | Must retain planning from configured structure, not registration count. |
| 14 | ReportService | Very High | High | 8–14 | Two pipelines depend on all core domains. |
| 15 | PrintDocumentService | High | High | 6–10 | Generated markup and print CSS must remain identical. |
| 16 | LeaderboardService | Medium | Medium | 3–5 | Can consume stabilized result/participant interfaces. |
| 17 | Route UI modules | Very High | High | 8–15 | Extract after service contracts are stable; retain hashes/templates. |
| 18 | Application shell/router | High | High | 5–8 | Last client extraction after routes own lifecycle/actions. |
| 19 | API/SQL adapter | Very High | Critical | 15–30+ | Separate program covering auth, transactions, concurrency, migration, and deployment. |

## Release gates

### Before storage extraction

- Demo, imported, empty, and legacy state fixtures exist.
- JSON/localStorage and XML round trips are tested.
- Startup import behavior is characterized.

### Before domain extraction

- Division, team, result, final, and award matrices are approved.
- Edge-case tests prove current behavior and known limitations.
- Proposed contracts require no hidden globals.

### Before UI extraction

- Domain services are DOM-free.
- Routes have repeatable smoke checks.
- Print previews have visual references.

### Before hosted storage

- API/authorization contracts are approved.
- XML-to-SQL migration has executable tests.
- Every query is competition-scoped.
- Concurrent-write and recovery policies are defined.

