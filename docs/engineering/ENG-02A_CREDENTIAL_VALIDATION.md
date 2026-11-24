# ENG-02A — Stored credential validation (draft)

Base: ab3f02771959805a8fc793b61f3fdea41c352f34. Scope is independent preparatory hardening; Phase 1 acceptance remains a prerequisite to promotion.

PasswordHashService.Verify previously passed decoded stored parameters directly into the key derivation routine. Validate encoded size, positive bounded iteration count, and the current generator's salt and digest lengths before derivation. Preserve PBKDF2-SHA256, constant-time comparison, the 210,000 generation count and valid older iteration counts. No automatic rehash, key rotation, credential modification, schema changes or production access.

The 1,000,000 verification ceiling is a proposed operational bound, not a new hashing recommendation. The 500-character bound matches the account and competition password columns. The current generator emits 16-byte salts and 32-byte digests. Confirm that older/imported production hashes comply using aggregate format metadata only under separate authorised operational review; do not export or log password hashes. Unsupported records require a governed recovery/reset plan, not a silent downgrade. Existing login lockout behaviour is unchanged.

Regression cases are added to the existing CoreIntegrity integration suite; execution on the new commit is pending hosted CI. Tests use only generated credentials. Before acceptance confirm all CI jobs, compatible credential format inventory, and account/competition login paths. Rollback is a reviewed revert; do not release before Phase 1 acceptance or through an old manifest.
