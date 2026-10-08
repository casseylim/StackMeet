# Career Profile administration readiness

All routes below are protected by the existing `/api/admin` middleware in `Program.cs`. There is no weaker controller-only or browser-only policy. A matching configured admin key is the existing maintenance fallback; otherwise a signed current account session must resolve to an active AppUser with matching session version and `IsSystemAdmin=true`. Capability booleans describe effective administrative authority, including the existing admin-key fallback; they do not identify a person.

| Operation | Method and route | Controller/action |
|---|---|---|
| Prove capability without mutation | GET `/api/admin/career-profile/capabilities` | AdminCareerProfileCapabilitiesController.Get |
| Find unlinked candidates | GET `/api/admin/stacker-identities/candidates` | AdminStackerIdentitiesController.Candidates |
| Search linked/unlinked registrations | GET `/api/admin/stacker-identities/stackers` | AdminStackerIdentitiesController.Stackers |
| Review one registration and proposed identity | GET `/api/admin/stacker-identities/stackers/{id}` | AdminStackerIdentitiesController.StackerReview |
| Locate permanent identity | GET `/api/admin/stacker-identities/profiles/{nadiTrackId}` | AdminStackerIdentitiesController.Profile |
| Create PRIVATE identity and link, or link existing PRIVATE identity | POST `/api/admin/stacker-identities/links` | AdminStackerIdentitiesController.Link |
| Publish/unpublish | PUT `/api/admin/stacker-identities/profiles/{nadiTrackId}/publication` | AdminStackerIdentitiesController.Publication |
| Unlink one relationship | POST `/api/admin/stacker-identities/links/{linkId}/unlink` | AdminStackerIdentitiesController.Unlink |

Anonymous requests return 401 when production admin security is configured. Valid ordinary account sessions return 403, invalid/revoked sessions return 401. Legacy competition sessions are insufficient. Publication and unlink have exactly the same global authority as the read-only capability proof. No authorization attribute replaces the prefix gate. Browser sessions are custom signed bearer tokens in sessionStorage, not authentication cookies; no cookie/antiforgery token is required by these APIs. Secrets never appear in capability output. Read-only registration review is privileged and can show proposed identity demographics; they must never be placed in public APIs or operational reports.

The capability GET neither updates LastLoginAt nor creates audit/business records. Middleware may read account metadata to verify session freshness. Use the owner's existing session through the admin page; do not copy tokens into chat or logs. UI visibility is informational; server authorization remains mandatory.

## Operator flow

Sign in at the existing admin page. Career Profiles becomes available only after the capability GET succeeds. Search by numeric competition ID and participant number/name, then review exactly one registration. Search is paginated and includes linked registrations. The proposed identity uses current registration values, defaults PRIVATE, and displays a notice for contact values that are preserved without being shown. Source placeholders block creation in both UI and persistence; do not silently clean or modify production registration data.

Create is intentionally combined with linking so it cannot create an orphan identity. Existing-person linking requires a separate read-only NadiTrackId review and an explicit confirmation. Duplicate match candidates block new creation in this UI; no bulk or duplicate-override control is provided. A public target identity must first be unpublished in a separate reviewed operation before adding a registration; neither UI nor persistence silently exposes that registration or changes other links' visibility.

Every mutation requires a reason and an explicit confirmation showing the selected participant/registration and identity. Publish/unpublish are separate from creation/linking. Unlink submits the exact LinkId, StackerId and NadiTrackId guards, preserves the permanent person and other links, and leaves competition registrations/results intact. Controls allow only one in-flight mutation; refreshed selection and stale-response checks prevent replaying a previously reviewed relationship. Network/conflict failures require a fresh review. Public URLs are shown only when the refreshed identity is PUBLIC.

## Deployment preparation only

Application upload allowlist: `/admin.html`, `/admin.js`, `/admin.css`, `/career-admin.js`, `/StackMeet.Api.dll` (DLL last). No migration or runtime dependency changes. Exclude web.config, appsettings, secrets, uploads and data. The previous DLL-only workflow is locked to source 1e2c021 and cannot deploy this new static-asset release unchanged. A separately reviewed five-file release/preflight is required; do not dispatch that old workflow for this artifact.

Before an authorized deployment, revalidate master/source, fresh FTP DLL/static/config hashes and the retained artifact manifest. Stop ONLY NADITrack; leave the shared pool running. Retain/verify backups for all existing allowlisted files, upload assets and DLL last, then verify every upload and protected config hashes. Start only NADITrack after success. Roll back the old static files and old DLL under the same site-only interlock if startup/smoke fails. Restoring old admin.html removes references to the newly added script; leaving that unreferenced script does not activate any profile and avoids unnecessary deletion.

The prepared rollback DLL is the previously deployed, hash-verified 1e2c021 artifact. Public static rollback files are downloaded read-only during preparation; this does not replace the mandatory fresh FTP preflight. No hosting change, workflow dispatch, production upload, password reset or identity operation is authorized by this preparation step.

After deployment, capability GET is the safe proof of the owner's current global admin boundary. Repeat anonymous/ordinary denial and public profile privacy regressions. Do not create/link/publish/unpublish/unlink a production identity during readiness verification. The provisional Stacker 62 is in an Active competition and has a stored placeholder last name; do not auto-select it for activation. A finalized/publicly listed competition with valid results and owner-approved participant remains a separate prerequisite.
