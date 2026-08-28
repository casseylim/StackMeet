# Multilanguage v2 Phase 1 audit

## Current system

The authenticated operator UI stores its language in `state.settings.language`. English source text is used as the translation key. `t()` resolves custom competition translations against the built-in Malay and Chinese dictionaries, and `translateChrome()` updates marked DOM chrome. The language settings view edits `data-language-key` inputs and persists the selected dictionary through the existing CompetitionState payload.

The current built-in dictionaries are still retained in `app.js` for compatibility during this incremental foundation phase. Dedicated locale files contain the initial extracted vocabulary and are not yet wired into the legacy renderer.

## Coverage classification

| Area | Status | Evidence / boundary |
| --- | --- | --- |
| Main operator chrome and common navigation | PARTIAL | `t()` and `translateChrome()` cover marked chrome; many generated/report strings remain source English. |
| Competition settings and language editor | PARTIAL | Settings labels and editor chrome are translated; editor currently exposes Malay/legacy Chinese selection behavior. |
| Participant, Doubles, Relay screens | PARTIAL | Shared chrome is translated; participant data and many generated labels remain data/source text. |
| Printing and reports | ENGLISH ONLY | Report and print output contain substantial hardcoded labels and are not routed through the new helper. |
| Login, activation, reset, account pages | ENGLISH ONLY | These screens are separate from the operator dictionary pipeline. |
| System administration | ENGLISH ONLY | No complete language-resource pipeline was found. |
| Public Results | ENGLISH ONLY | Results HTML/JS contains hardcoded status, headers, tabs, disclaimers, and connection messages. |

## Language inventory

Supported values are English `en`, Bahasa Malaysia `ms`, and Simplified Chinese canonically represented as `zh-Hans`. The legacy stored value `zh` is accepted and normalized by the new helper to `zh-Hans`; existing app dictionaries remain available under their legacy shape while migration is incremental.

Placeholders, titles, and aria labels are only translated where their containing markup is explicitly processed or has a language key; this is not a complete guarantee for generated HTML. No speculative claim is made for unmarked strings.

## Language concepts for later phases

1. Operator UI language: a per-user or browser display preference.
2. Competition public default language: a per-competition display default for public Results.
3. Competition translation overrides: optional competition terminology overrides keyed by stable semantic keys.

The existing persisted competition language remains unchanged for backward compatibility. Public Results language selection should later be a display-only `lang` query parameter (`/{CompetitionID}/Results?lang=en|ms|zh-Hans`); the permanent Results URL remains unchanged.

## Confirmed issue to address later

The language editor currently maps non-English editing to the selected legacy dictionary and does not have a dedicated canonical English editor path. Phase 1 records this as a characterization boundary; no user-visible behavior was changed here.
