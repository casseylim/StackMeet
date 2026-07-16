# Global State Schema

## Contract

The global `state` object is created by `loadState()`, normalized by `normalizeState()`, and stored as one JSON document under localStorage key `stackmeet-stacktrack-style-v1`. The current XML format represents most of the same data.

Unless a row says otherwise, a property is persisted in both JSON/localStorage and XML. “Calculated” values may still be stored. Future SQL mappings describe `database/schema.sql`, not a live database.

## Top-level state

| Property | Purpose | Owner module | Future SQL table | Persisted | Calculated | Editable |
|---|---|---|---|---|---|---|
| `settings` | Tournament and operational configuration. | Settings | `competitions`, `competition_stages` | Yes | Partly normalized | Yes |
| `translations` | Language dictionaries/overrides. | Translation | Future translation/config store | Yes | Defaults merged | Yes |
| `leaderboard` | Leaderboard display configuration. | Leaderboard | `leaderboard_settings` | Yes | No | Yes |
| `awards` | Award-place/item plan and overall categories. | Awards | Future award plan tables | Yes | Normalized | Yes |
| `events` | Enabled events by group. | Settings/Events | `competition_events` | Yes | Defaults supplied | Yes |
| `divisionSettings` | Age cutoffs and custom divisions. | Divisions | `division_cutoffs`, `divisions` | Yes | Defaults merged | Yes |
| `divisions` | Effective division names. | Divisions | `divisions` | Yes | Yes | Indirectly |
| `stackers` | Registered individuals. | Stackers | `stackers`, `organizations` | Yes | Some fields | Yes |
| `doubles` | Doubles/Child-Parent teams. | Teams | `teams`, `team_members` | Yes | Some fields | Yes |
| `relays` | Timed Relay teams. | Teams | `teams`, `team_members` | Yes | Some fields | Yes |
| `results` | Prelim/final/SOC result records. | Results/Finals | `results`, `result_attempts` | Yes | Official values derived | Yes |
| `notifications` | Notices and read state. | Notifications | `notifications` | Yes | No | Read state only |
| `users` | Local operator/activity display records. | Users | `users`, `user_sessions` | Yes | No | No current editor |
| `importBatch` (optional) | Applied bundled import identifier. | Storage/Import | Future import audit metadata | JSON only | Automatic | No |

## `settings`

Owner is Settings unless noted. All properties are persisted and editable.

| Property | Type | Purpose | Future SQL mapping | Calculated |
|---|---|---|---|---|
| `name` | string | Tournament display name. | `competitions.name` | No |
| `type` | string | Competition type. | `competitions.competition_type` | No |
| `start` | date string | Start and age-reference date. | `competitions.start_date` | No |
| `end` | date string | End date. | `competitions.end_date` | No |
| `prelims` | `"0"`/`"1"` | Current prelim enable/round setting. | `competition_stages.enabled/round_count` | Normalized |
| `finals` | string | Finals-stage setting. | `competition_stages.enabled/round_count` | No |
| `kbsLogo` | `Yes`/`No` | KBS logo preference. | `competitions.use_kbs_logo` | No |
| `soc` | `Yes`/`No` | SOC enablement. | `competition_stages` (`soc`) | No |
| `prelimTimes` | string | Prelim time-selection mode. | `competitions.prelim_time_mode` | No |
| `paperless` | `Yes`/`No` | Paperless mode. | `competitions.paperless_mode` | No |
| `advanceIndividuals` | number | Individual finalists limit. | Competition/stage configuration | No |
| `advanceDoubles` | number | Normal Doubles finalists limit. | Competition/stage configuration | No |
| `advanceCpDoubles` | number | Child/Parent finalists limit. | Competition/stage configuration | No |
| `advanceRelay` | number | Relay finalists limit. | Competition/stage configuration | No |
| `timeSheetInput` | string | Time-sheet input/printing preference. | Competition configuration | No |
| `language` | string | Active `en`, `ms`, or `zh` UI code. | User/competition preference | Defaulted |

## `translations`

| Property | Purpose | Owner | SQL | Persisted | Calculated | Editable |
|---|---|---|---|---|---|---|
| `ms` | English phrase -> Bahasa Malaysia dictionary. | Translation | Future translations/config | Yes | Defaults + overrides | Yes |
| `zh` | English phrase -> Simplified Chinese dictionary. | Translation | Future translations/config | Yes | Defaults + overrides | Yes |

Every nested property name is an English source phrase and its value is translated text. Keys originate in the two default dictionaries.

## `leaderboard`

Owner: Leaderboard. All are persisted and editable.

| Property | Purpose | Future SQL field | Calculated |
|---|---|---|---|
| `type` | Display/result type. | `leaderboard_settings.display_type` | No |
| `stage` | Selected result stage. | `leaderboard_settings.stage_code` | No |
| `bg` | Background color. | `background_color` | No |
| `color` | Accent/text color. | `accent_color` | No |
| `pause` | Intended rotation interval. | `pause_seconds` | No |
| `limit` | Maximum rows. | `result_limit` | XML numeric conversion |

## `awards`

Owner: Awards. Future SQL requires new award-plan tables; none exists yet.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `individualPlaces` | Places per individual division/event. | Yes | Allowed-limit normalization | Yes |
| `individualItems` | Medal/Trophy by individual place. | Yes | Length/default normalization | Yes |
| `doublesPlaces` | Places per Doubles category. | Yes | Normalized | Yes |
| `doublesItems` | Medal/Trophy by Doubles place. | Yes | Normalized | Yes |
| `relayPlaces` | Places per Relay category. | Yes | Normalized | Yes |
| `relayUnits` | Awards per placing Relay team (4–6). | Yes | Normalized | Yes |
| `relayItems` | Medal/Trophy by Relay place. | Yes | Normalized | Yes |
| `overall` | Overall-category settings. | Yes | Defaults merged | Yes |

`overall` has keys `male`, `female`, `specialMale`, `specialFemale`, and `combined`. Every value contains editable/persisted `limit` (normalized number of places) and `item` (`Medal`/`Trophy`). Calculated award summary rows/totals are not state properties.

## `events`

Owner: Settings/Events. Each editable/persisted property is an enabled event-name array mapped later through `competition_events`.

| Property | Current catalog |
|---|---|
| `Individuals` | `3-3-3`, `3-6-3`, `Cycle` |
| `Doubles` | `Cycle` |
| `Timed Relay` | `3-6-3` |
| `Head To Head` | `3-6-3`, `Cycle` where configured |

## `divisionSettings` and `divisions`

Owner: Divisions.

| Property | Purpose | Future SQL | Persisted | Calculated | Editable |
|---|---|---|---|---|---|
| `divisionSettings.combined` | Combined age cutoffs. | `division_cutoffs` | Yes | No | Yes |
| `divisionSettings.male` | Male age cutoffs. | `division_cutoffs` | Yes | No | Yes |
| `divisionSettings.female` | Female age cutoffs. | `division_cutoffs` | Yes | No | Yes |
| `divisionSettings.special` | Special/SS age cutoffs. | `division_cutoffs` | Yes | No | Yes |
| `divisionSettings.custom` | Custom/import-preserved names. | `divisions` | Yes | Import may augment | Yes |
| `divisions` | Generated plus preserved effective names. | `divisions` | Yes | Yes | Indirectly |

## `stackers[]`

Owner: Stackers, with Division/Import ownership noted. Future core table: `stackers`; organizations map to `organizations`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `id` | Public `1.x` code. | Yes | Generated/imported | No direct edit |
| `name` | Full/display name. | Yes | No | Yes |
| `gender` | Gender for divisions/reports. | Yes | No | Yes |
| `dob` | Date of birth. | Yes | No | Yes |
| `age` | Age on start date. | Yes | Yes | No |
| `special` | Special-stacker flag. | Yes | May be import-inferred | Yes |
| `org` | School/club/organization. | Yes | No | Yes |
| `division` | Effective division. | Yes | Usually | Indirectly |
| `standardDivision` | Imported/preserved standard division. | Yes | Import-derived | Import only |
| `customDivision` | Explicit division override. | Yes | No | Yes |
| `country` | Country. | Yes | No | Yes |
| `region` | State/region. | Yes | No | Yes |
| `email` | Contact email. | Yes | No | Yes |
| `phone` | Contact phone. | Yes | No | Yes |
| `paid` | Payment status. | Yes | No | Yes |
| `checkedIn` | Check-in status. | Yes | Defaulted | Yes |
| `fname` (optional) | Imported first name. | JSON only | Import-derived | Import only |
| `lname` (optional) | Imported last name. | JSON only | Import-derived | Import only |
| `amt` (optional) | Imported fee/amount. | JSON only | No | Import only |
| `doubles` (optional) | Imported Doubles intent. | JSON only | No | Import only |
| `doubles_partner` (optional) | Imported requested partner. | JSON only | No | Import only |
| `relay` (optional) | Imported Relay intent. | JSON only | No | Import only |
| `relay_team` (optional) | Imported requested team. | JSON only | No | Import only |
| `avg_time` (optional) | Imported average 3-6-3. | JSON only | No | Import only |
| `my_ic` (optional) | Malaysia IC import field. | JSON only | No | Import only |
| `my_class` (optional) | Malaysia class import field. | JSON only | No | Import only |

Future SQL fields include public/bib code, display/first/last name, gender, DOB, special, organization/division foreign keys, country/region, contact, paid, and check-in. Import-only preferences require a future registration model. Report-only `d_id`, `r_id`, partner/team names, and assigned team divisions are calculated and are not stacker properties.

## `doubles[]`

Owner: Teams. Future tables: `teams`, `team_members`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `id` | Public `2.x` code. | Yes | Generated | No direct edit |
| `type` | `normal` or `child_parent`. | Yes | Legacy-normalized | Yes |
| `status` | `complete` or `pending`. | Yes | May be inferred | Yes |
| `one` | First member/child stacker ID. | Yes | Legacy-normalized | Yes |
| `two` | Second registered member ID. | Yes | Legacy-normalized | Yes |
| `parentName` | External parent/guardian. | Yes | Legacy-normalized | Yes |
| `customDivision` | Explicit override. | Yes | No | Yes |
| `division` | Effective team division. | Yes | Usually | Indirectly |
| `country` | Team country. | Yes | May derive from members | Yes/derived |

Compatibility inputs accepted during normalization: `stackerOneId`, `stackerTwoId`, `childStackerId`, `parentStackerId`, and `partnerName`. They are not the intended normalized model.

## `relays[]`

Owner: Teams. Future tables: `teams`, `team_members`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `id` | Public `3.x` code. | Yes | Generated | No direct edit |
| `name` | Unique team name. | Yes | No | Yes |
| `coordinator` | Coordinator name. | Yes | No | Yes |
| `email` | Coordinator email. | Yes | No | Yes |
| `phone` | Coordinator phone. | Yes | Legacy `cell` normalized | Yes |
| `customDivision` | Explicit override. | Yes | No | Yes |
| `division` | Effective Relay division. | Yes | Usually | Indirectly |
| `org` | Organization. | Yes | No | Yes |
| `country` | Country. | Yes | May derive from members | Yes/derived |
| `region` | Region/location. | Yes | May derive; legacy `loc` normalized | Yes/derived |
| `members` | Ordered stacker IDs, max six. | Yes | Legacy shapes normalized | Yes |

Completion (`members.length >= 4`) is calculated, not stored.

## `results[]`

Owner: Results/Finals. Future tables: `results`, `result_attempts`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `id` | Result identifier. | Yes | Generated | No direct edit |
| `stage` | `Prelims`, `Finals`, or `SOC`. | Yes | No | Workflow-selected |
| `type` | `Individual`, `Doubles`, or normalized `Timed Relay`. | Yes | Legacy-normalized | Derived/selected |
| `participant` | Public stacker/team ID. | Yes | Entry-resolved | Selected |
| `event` | Event name. | Yes | Workflow-derived | Selected |
| `attempts` | Numeric attempts; `999` carries scratch semantics. | Yes | Parsed | Yes |
| `penalty` | Numeric penalty in the general result path. | Yes | Defaulted | Where exposed |

Best/official time, finalist qualification, final sheet ID/order, placement, ranks, and all-around totals are calculated views, not state fields.

## `notifications[]`

Owner: Notifications; future table: `notifications`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `id` | Notification ID. | Yes | May generate on XML import | No |
| `title` | Message text. | Yes | No | No current editor |
| `time` | Display timestamp. | Yes | No | No |
| `read` | Read/unread state. | Yes | No | Yes |

## `users[]`

Owner: Users; future tables: `users`, `user_sessions`.

| Property | Purpose | Persisted | Calculated | Editable |
|---|---|---|---|---|
| `name` | Operator display name. | Yes | No | No current editor |
| `access` | Access-level label. | Yes | No | No |
| `last` | Last-active display value. | Yes | No | No |
| `platform` | Client platform. | Yes | No | No |
| `browser` | Browser label. | Yes | No | No |

## State outside `state`

Route, edit/delete IDs, flash messages, table/report sort, selected report/team tabs, active prelim/final IDs, editor-open flags, and print orientation are mutable UI-session globals. They are not persisted in localStorage/XML and should remain separate from tournament domain data in future modules.

