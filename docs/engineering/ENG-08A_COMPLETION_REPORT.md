# ENG-08A dependency security triage

Baseline de24245383a0940d4da1c369bdcd251b49868692. Draft for security and release-engineering acceptance.

## Changes and exposure

The API project now pins three .NET 8-compatible package versions above the advisory thresholds. EF Core and the SQL provider still own the primary dependency paths. The project remains net8.0 and the EF tool manifest retains its existing version.

| Advisory | Baseline path and reachability | Candidate and evidence |
|---|---|---|
| [CVE-2024-43483](https://github.com/advisories/GHSA-qj66-m88j-hmgj) | EF Core SQL Server → EF Core Relational → EF Core → Microsoft.Extensions.Caching.Memory 8.0.0. EF Core is active on normal database request paths. This establishes dependency use, while direct control of the vulnerable cache operation has not been established. | Package 8.0.1 resolves and is included in the publish output. The host traced the published and .NET 8.0.30 shared copy with the same file version (8.0.1024.46610). |
| [CVE-2024-38095](https://github.com/advisories/GHSA-447r-wph3-92pm) | EF Core SQL Server → Microsoft.Data.SqlClient → System.Security.Cryptography.Cng → System.Formats.Asn1 5.0.0. The affected functionality involves X.509 parsing. The dependency graph establishes a possible SQL/provider cryptography path. The test database uses local integrated authentication and did not exercise hosting TLS or external certificate trust. | Package 8.0.1 clears restore findings. Framework-dependent publish omitted the library copy and the candidate host resolved the .NET 8.0.30 shared assembly. |
| [CVE-2024-43485](https://github.com/advisories/GHSA-8g4q-xg66-9fp4) | EF Design → Microsoft.Extensions.DependencyModel, and SQL provider paths through Azure Identity and token packages, converge on System.Text.Json 8.0.4. JSON itself is used directly throughout the API, including state request parsing. The specific affected serializer use and effective IIS assembly are unverified. | Package 8.0.6 clears restore findings. Framework-dependent publish omitted the library copy and the candidate host resolved .NET 8.0.30. |

The candidate replaced resolved vulnerable package versions in StackMeet.Api.csproj. Before/after measurements from the same resolved API graph are seven total advisory rows to four, and three high-severity rows to zero. The remaining matches concern Azure.Identity 1.10.3 and Microsoft.Identity.Client 4.56.0. Plan provider-chain upgrades separately with authentication-mode checks and approved scope. Reachability in hosting is an owner decision based on the hosting runtime and configured authentication paths.

## Build and release evidence

- Release solution restore/build passed. The three existing nullable compiler warnings in historical projection remain.
- Framework-dependent Release publish used the isolated reviewed SDK 10.0.400. The approved compiler host and candidate PDB reported compiler execution runtime 10.0.11. Candidate loopback startup under .NET 8.0.30 returned HTTP 200 from the version endpoint. Host trace selected shared framework assemblies for JSON and ASN.1.
- The existing CoreIntegrity LocalDB harness passed on a generated database and cleaned it up. The offline release suite passed 68 cases. These tests verify configuration and toolchain compatibility in the isolated environment. They do not verify a deployed runtime, external SQL certificates or a new governed package hash.

## Acceptance and rollback

Accept when the resolved graph continues to show zero unapproved high matches, release engineering verifies the effective IIS runtime in its controlled environment, and the owner assigns the four lower-severity advisory rows. Validate actual deployment compatibility with a separately approved new package and rollback evidence. The historical approved manifest is tied to a different source. Run 37912223752 remains ineligible. The approved artifact and release preflight still govern any future promotion.

Rollback is a reviewed revert of the three references in StackMeet.Api.csproj followed by a fresh build and deployment review. Older package resolution reintroduces the three high matches. Servicing the hosting runtime is a separate operational action. Major .NET framework migration remains separate from this patch set.
