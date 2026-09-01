# Multilanguage v2 operator audit — Phase 3E baseline

## Current system

The authenticated operator UI stores its language in `state.settings.language`. English source text is used as the translation key. `t()` resolves custom competition translations against the built-in Malay and Chinese dictionaries, and `translateChrome()` updates marked DOM chrome. The language settings view edits `data-language-key` inputs and persists the selected dictionary through the existing CompetitionState payload.

The authenticated operator renderer uses the shared localization helpers and dedicated `en`, `ms`, and canonical `zh-Hans` catalogs. This audit records the current final-closure baseline; source-based regression tests remain the authority for newly added UI.

## Coverage classification

| Area | Status | Evidence / boundary |
| --- | --- | --- |
| Main operator chrome and common navigation | CLOSED | Marked HTML chrome and generated navigation/status text use the shared helpers. |
| Competition settings and language editor | CLOSED | Settings, event/division controls, branding, public-results settings, and language setup are covered by the catalogs and chrome translation. |
| Participant, Doubles, Relay screens | CLOSED | Presentation labels and controlled status enums are localized; participant/team domain data remains unchanged. |
| Printing and reports | CLOSED | Phase 3D report/printing paths are localized and guarded by dedicated tests. |
| Login, activation, reset, account pages | CLOSED | Phase 3B dynamic titles, safe fallbacks, and supported locales are retained. |
| System administration | CLOSED | Phase 3B admin UI and dynamic title behavior are retained. |
| Public Results | CLOSED | Phase 3C language resolution, navigation, refresh persistence, and spectator UI are guarded. |

## Language inventory

Supported values are English `en`, Bahasa Malaysia `ms`, and Simplified Chinese canonically represented as `zh-Hans`. The legacy stored value `zh` is accepted and normalized by the new helper to `zh-Hans`; existing app dictionaries remain available under their legacy shape while migration is incremental.

Placeholders, titles, aria labels, generated messages, and controlled display enums are translated through explicit keys or localization markers. Domain-owned values are intentionally excluded.

## Language concepts for later phases

1. Operator UI language: a per-user or browser display preference.
2. Competition public default language: a per-competition display default for public Results.
3. Competition translation overrides: optional competition terminology overrides keyed by stable semantic keys.

The existing persisted competition language remains unchanged for backward compatibility. Public Results language selection should later be a display-only `lang` query parameter (`/{CompetitionID}/Results?lang=en|ms|zh-Hans`); the permanent Results URL remains unchanged.

## Deferred / excluded work

Certificate generation remains paused pending the Syncfusion license. Generated reports/printing and the Public Results portal are closed for this multilingual scope; future certificate/PDF implementation is a separate phase.
