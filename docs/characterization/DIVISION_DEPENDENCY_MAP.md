# Division Dependency Map

This is the current dependency map. It deliberately describes the existing distributed implementation before extraction.

```text
Competition settings and state
        |
        +--> normalizeState()
        |       +--> recalculateStackerDivisions()
        |       |       +--> divisionForStacker()
        |       |               +--> ageOnCompetitionDate()
        |       |               +--> findDivisionFor()
        |       +--> generateDivisionNames()
        |       +--> generatedDoublesDivision()
        |       +--> generatedRelayDivision()
        |
        +--> registration/edit/import/SQL sync
        |       +--> divisionForStacker()
        |       +--> recalculateStackerDivisions()
        |
        +--> results/finals/reports/awards/export
                +--> consume stored stacker/team division fields
                +--> apply screen-specific grouping/filtering
```

## Change impact

| Function or field | Known consumers | Risk when changed |
|---|---|---|
| `ageOnCompetitionDate()` | Registration, recalculation, team division, relay division, reports | High: changes age boundaries and every dependent division. |
| `divisionForStacker()` | Registration, import, SQL sync, recalculation, XML-derived state | Critical: changes Individual assignment and persisted output. |
| `findDivisionFor()` | Registration preview and compatibility callers | High: changes normal and Special Individual labels. |
| `recalculateStackerDivisions()` | Normalize, settings changes, competition-age recalculation | Critical: can rewrite all Individual divisions. |
| `generateDivisionNames()` | Settings UI, normalize, import, visible division selectors | High: can hide or add categories without changing participants. |
| `generatedDoublesDivision()` | Doubles creation, normalization, XML/export helpers | Critical: affects team grouping and awards/results. |
| `generatedRelayDivision()` | Relay creation, normalization, XML/export helpers | Critical: affects timed/head-to-head grouping. |
| Stored `division` fields | Results, finals, awards, reports, public results, XML | Critical: downstream screens commonly consume stored values rather than recalculating. |

## Refactoring rule

Any future engine extraction must first preserve these entry points or provide an explicit compatibility adapter. A passing unit test for one calculator is insufficient evidence that downstream consumers remain equivalent.
