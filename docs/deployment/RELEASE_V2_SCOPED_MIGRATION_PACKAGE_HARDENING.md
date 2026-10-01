# Release v2 scoped migration package hardening

The preflight artifact from run 36800368618 matched the reviewed hash
`D18A21300B73302DE788A41CD539048D7F2065213F01C5D540F7CF62261D1BFC`,
but contained all 15 repository migrations. The EF command omitted FROM and TO arguments,
so generation started at migration zero. Presence-only checks for the three release IDs
did not reject the 12 older migration blocks. The database stage correctly stopped before
connecting to production because the package exceeded its explicitly authorized scope.

The release manifest now governs both boundaries:

- FROM (exclusive): `20260826074651_AddAccountSessionVersion`
- TO (inclusive): `20260914093000_FinalsRankingGovernanceSp4g`

Generation remains idempotent and uses the restored repository-local EF 8.0.8 tool.
The text-only package validator requires exactly the three existing governed migration IDs,
each with an idempotent history guard and history insert. It rejects earlier IDs, including
the FROM predecessor, unexpected later IDs, missing IDs and altered manifest boundaries.
Executable regression tests exercise the validator with synthetic package text, including
the former 15-migration chain. They never execute SQL or connect to a database.

Production must already contain the complete migration baseline through the FROM boundary.
A separately authorized preflight must generate a new artifact and hash after merge.
Historical-migration repair is outside this release. No application/runtime code, migration
source, model snapshot, configuration or deploy interlock changes are part of this fix.

This hardening phase does not dispatch production workflows, use FTP, execute SQL, connect
to production, deploy files or operate IIS/app pools.
