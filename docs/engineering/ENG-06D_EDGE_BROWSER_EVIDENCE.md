# ENG-06D — Edge browser, accessibility and print evidence candidate

Base ENG-06C 0bf7320ef209c2b0363e6546c50fe616c39f13de, [CI 46117623708](https://github.com/casseylim/StackMeet/actions/runs/46117623708), all three jobs passed, 151/151 ledger records. New hosted browser execution pending.

## Scope
Use installed Microsoft Edge on Windows runner in an isolated temporary profile, CDP and Node built-ins. Serve unmodified current-checkout wwwroot files on a random loopback port with explicit synthetic API endpoints. Source SHA checked; application external resources restricted by fixture CSP and HTTPS blocked in the page. External QR rendering deliberately excluded. No production credentials, live data, SQL service, real authentication authority, or live SignalR connection. No production calls or writes; only synthetic login POST and rejected hub negotiation. The fixture rejects unexpected API calls.

Expected browser cases: 26 (separate from 151 execution ledger records):
- Nine public All-Around cases: 320, 390, 1280 CSS px × English, Bahasa Malaysia, Simplified Chinese. Render actual public scripts, check locale, accessible language control name and complete Finals-only total 18.000; omit incomplete finalist even though Prelims are complete.
- One public language selection, withdrawal and empty-result recovery case.
- Nine authenticated operator Print Center cases at the same widths/locales, through the actual login/competition picker UI with synthetic server responses; check accessible language control and screenshot.
- Six actual Edge PDF previews, Preliminary portrait and Finals landscape across all three locales. Observe real print action's temporary target/style, simulate afterprint to avoid native modal, verify cleanup, replay captured style/target using CDP printToPDF. Require populated preview and PDF signature.
- One keyboard focus/Tab case.

Record source SHA, browser version, screenshots, PDFs, scroll width, accessible names, cases, findings, exceptions and synthetic API route trace in separate artifact edge-browser-evidence-<run>. Fail on unmet functional assertions, incorrect case count, unexpected API or uncaught JS exception. Horizontal overflow is recorded for review, not an automatic acceptance gate. Existing build, pinned-source offline reproducibility, inventory and API evidence validator remain intact. Browser case counts do not enter API coverage or Node execution ledger.

## Acceptance limitations
Not a full axe/WCAG audit, screen-reader or zoom assessment, full device testing, live login/revocation, real scoring save/reconnect, SQL-backed browser integration, real print dialog/printer, PDF content extraction or page-size measurement, visual review of all screenshots, QR generation, or Excel opening. PDF orientation is requested from the real print action and browser, not independently measured. The operator path covers the Print Center, not every route. Findings must be reviewed before operational acceptance. No new third-party dependencies or application behavior changes. ENG-06E to consolidate verified evidence, defects, residual scope and thresholds. CI pending; no claim of browser pass yet.

Never overwrite production IIS web.config; exclude from deployment/sync/rollback, secure backup and identical pre/post SHA-256. Finals-only Overall/All-Around, separate Prelims and immutable historical certification preserved. No master merge or production deployment without explicit approval.
