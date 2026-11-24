# ENG-02B — Session validation and revocation regression (draft)

Base: 7783809f6b33e2f60e539927d1038200c573d0ff. Stacked on ENG-02A. Preserve existing HMAC-SHA256 signatures, v3 account payload, legacy three-field payload, expiry configuration and DB session-version checks.

Changes: bound incoming token length to 8192 characters, require the generated 43-character SHA256 signature encoding and 32-byte signature, reject malformed UTF-8, invalid account IDs and admin flags, invalid expiry values and malformed nested fields without throwing. Publish the validated session only after all checks succeed. Legacy competition login rejects names over 1000 UTF-16 code units, ensuring the generated token fits the parser budget; valid legacy tokens otherwise retain their format. These are proposed input bounds, not a change in session lifetime or signing key. Existing unusually long legacy tokens may require sign-in with a shorter name.

Tests exercise valid account and legacy round trips, malformed and expired signed payloads using a synthetic key, invalid unsigned inputs, logout-all against actual HTTP gates, fresh session acceptance and account deactivation. Hosted execution is pending. No production access, no database migration, no key rotation or token logging. SessionVersion write concurrency, real-time connection revocation and publication policy are separate review items; this patch does not claim to address them.

Acceptance: all hosted jobs pass; confirm supported legacy and account clients, then integrate only after Phase 1 and ENG-02A acceptance. Revert the coordinated source commit if needed; the existing release manifest remains ineligible.
