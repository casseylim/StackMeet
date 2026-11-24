# ENG-02D — Public publication boundary and identity privacy (draft)

Base: 4b2b253c05fad0e42052aeab733428d2841d34c7. Stacked on the CI-passed ENG-02C2 candidate. No database migration, no stored result or certificate modification, no release authorization.

## Findings and implemented controls

- Public directory already filters listed/non-archived events. Explicit no-store prevents reuse of an obsolete listing.
- Public scoreboards and profile-link bridge used SingleOrDefault on code OR key, although each column is independently unique. Both now return 404 for ambiguous aliases, including when one candidate is archived. Existing unique aliases retain access. Admin create and maintenance create/update check both alias columns within serializable transactions. This protects these API write paths, not out-of-band imports; deadlock/throughput behavior requires validation. No historical aliases were changed.
- Direct results retain their existing public projection and legacy JSON fallback. Exact DOB, contacts, WSSA ID, payment/check-in and raw state/settings are not published by the typed projection. DOB is used internally to determine division. Participant names, gender, location/organization and special division status remain public as previously; these can still be identifying/sensitive, particularly for children. No claim of anonymization.
- Profile link bridge now returns an empty links array for an unlisted non-archived competition, even if a permanent identity is public. Publicly listed events still require IsPublicProfile. This is a proposed privacy decision requiring owner acceptance; direct-link scoreboards still work. Private profiles remain 404; archived direct scoreboards and bridges remain 404. Historical career projection and certified evidence remain unchanged.
- Branding assets retain existing non-archived public HTTP behavior and ETag revalidation. Public results and profile responses already use no-store. Archived publicly listed historical career records retain their existing eligibility rules.

## Proposed owner policy (not approved)

Separate discoverability, direct-link access and permanent identity/career publication. Today IsPubliclyListed is not a private-access flag: an unlisted URL still exposes the public results projection and public branding. Do not treat it as confidentiality or consent. Decide whether to introduce an explicit private/unlisted/listed publication state, participant/minor consent and masking rules, historical exceptions/retention and certificate verification access. A new private state must cover results, assets, links, push, career projections and direct URLs together; a listing toggle alone is insufficient. Do not silently reclassify existing events or alter immutable historical certifications.

## Validation

New isolated loopback scenarios exercise alias collision, create/update conflict, direct unlisted results, participant field allowlist, no-store directory, public/private identity bridge, archive and preserved public career profile. Hosted execution pending. Before acceptance: verify aggregate alias-collision counts in authorized production preflight (no personal-data export), owner-approved publication rules, browser behavior and applicable privacy obligations. Preserve existing production release gates.
