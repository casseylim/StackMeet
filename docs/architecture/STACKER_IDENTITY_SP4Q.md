# NADITrack Stacker Identity v1 — SP-4Q

## Identity-Linked Team Career History Foundation

SP-4Q adds an internal, read-only career model for finalized Sport Stacking Doubles and Timed Relay performances. It deliberately stops before public API/profile activation.

### Identity boundary

A team career fact can be associated with a permanent NADITrack Stacker only through an existing reviewed `StackerIdentityLink` for that competition.

SP-4Q does not match or infer identity from:

- athlete or teammate names;
- WSSA ID;
- date of birth;
- gender;
- email or phone;
- an external Child/Parent partner name.

If one permanent identity has multiple reviewed Stacker links in the same competition, SP-4Q fails closed rather than choosing a participant implicitly.

### Team membership authority

Team membership continues to live in `CompetitionState`, while team results live in `CompetitionResult`.

SP-4Q does not implement a second parser. It extends and reuses `CompetitionTeamResultIntegrityService`, the same server-owned boundary introduced by the team-result integrity hardening.

That boundary means:

1. a Doubles/Timed Relay SQL result can only be saved against a competition-ready team;
2. explicit registered team members must resolve to Stackers in the same competition;
3. while SQL results reference a team, the team cannot be removed, invalidated, or have its registered membership reassigned;
4. finalized/archived competition results cannot be changed through the result API.

Therefore SP-4Q can derive read-only team career facts without adding a persistence table or database migration.

### Child/Parent behavior

A Child/Parent Doubles team may contain one registered Stacker plus an external parent/partner display name.

SP-4Q:

- links the performance only to the registered Stacker identity;
- records `HasExternalPartner=true`;
- never copies the external partner name into the career model;
- never attempts to resolve that external name to a permanent identity.

### Internal read model

Each career point contains only:

- competition key/name/date;
- normalized participant type (`Doubles` or `Timed Relay`);
- team code;
- stage;
- event;
- factual result status;
- factual best time and penalty where valid;
- registered-member count;
- external-partner boolean;
- state/results/result revisions;
- a SHA-256 registered-membership fingerprint.

The membership SHA binds the career fact to the validated competition-time team composition without exposing teammate identifiers.

### Result semantics

SP-4Q publishes no team placement, medal, podium, award, or record-holder interpretation.

For the internal factual result:

- penalty >= 999 => Scratch;
- valid attempts are > 0 and < 999;
- finite positive penalty contributes to the official best time;
- empty attempt list => Missing;
- all-999 attempts => Scratch;
- malformed/other unusable attempts => Invalid.

Timed Relay is normalized through the existing `CompetitionResultRules`, including the legacy `Relay` alias, and remains restricted to 3-6-3.

### Eligibility

Only competitions that are both:

- publicly listed; and
- Closed, Archived, or otherwise archived

can contribute to the SP-4Q read model.

Private/malformed permanent identities retain the existing public not-found boundary.

### Fail-closed cases

SP-4Q contributes no career fact when:

- team state is missing or malformed;
- team IDs/membership are invalid;
- the reviewed linked Stacker is not a validated member of the result team;
- stage/event/type is unsupported;
- legacy alias rows create more than one normalized logical team result;
- same-competition identity linkage is ambiguous.

### Validation

SP-4Q adds:

- `StackerTeamCareerReadModelTests` LocalDB integration harness;
- static privacy/activation guards;
- required CI execution of the LocalDB harness.

Coverage includes:

- normal Doubles;
- Child/Parent with external partner;
- Timed Relay;
- legacy `Relay` alias;
- finalized/public filtering;
- unrelated team exclusion;
- external-name non-linkage;
- registered-membership fingerprint provenance;
- metadata/profile mutation immunity;
- server rejection of team membership drift while results exist;
- ambiguous identity-link fail-closed behavior.

### Deliberately unchanged

SP-4Q does **not** add:

- a public controller/API field;
- permanent profile UI;
- startup/DI activation;
- placement, medal, award, podium or record claims;
- a new database table or migration;
- production deployment;
- production database writes;
- any `web.config` change.

A later separately-reviewed phase can define the public team-career publication contract after this internal foundation is proven.
