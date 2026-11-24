# ENG-02C1 — SignalR subscription authorization (draft)

Base: cce04e537d515826da1e287a4c58d3710eb6250f. Entry/rejoin hardening only. Existing anonymous subscription calls previously accepted syntactically valid competition IDs without checking existence, archive status or authorization.

Anonymous JoinCompetition now requires a unique existing, non-archived, directory-listed competition, accepting public code or key and joining the canonical key. Unlisted direct links still use the existing public HTTP result endpoint and its polling fallback. This is a proposed conservative push-access policy; public HTTP publication remains unchanged and owner review is required. Old cached operator clients fall back to polling for unlisted competitions until updated.

JoinOperatorCompetition validates the existing signed token, account activity/version/admin flag and assigned read role, or the legacy competition scope. The operator sends its token as an invocation argument over the same-origin HTTPS connection, not in a query string; do not enable message/argument logging. Each connection retains at most one group; attempting another join first removes its previous membership. Keep the default sequential hub invocation setting.

This is not ongoing subscription revocation. Existing subscribers can remain until disconnect/rejoin after changes to listing, lifecycle, roles or account/session status. The current broadcast payloads are change metadata, not result rows. Continuous revocation, separate public/internal fanout and rate/resource limits remain ENG-02C2 review items. No change to result publication endpoints, scoring, stored results, database schema or production configuration.

Regression coverage uses actual SignalR JSON handshake and invocation over an isolated LocalDB test host; it exercises unlisted/listed/unknown, operator, scoped legacy, assignment and revoked-token rejoin cases. Hosted execution and browser interoperability are pending. Verify the fallback and coordinated API/browser cache rollout before acceptance.
