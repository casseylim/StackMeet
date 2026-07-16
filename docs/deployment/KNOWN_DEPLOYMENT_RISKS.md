# Known deployment risks

## High: local-only competition data

**Condition:** Competition state is saved in `localStorage`, not on IIS or SQL Server.

**Impact:** Data is not shared between computers, browsers, profiles, hostnames, or HTTP/HTTPS origins. Concurrent operators can create diverging versions of the competition. Browser storage loss can lose unsaved operational history.

**Mitigation:** Use one authoritative browser profile for live entry and frequent timestamped XML exports. Treat handovers as export, import, count verification, then continue.

## High: origin change at go-live

**Condition:** Browser storage is scoped to exact origin.

**Impact:** Test data created under localhost, `file://`, HTTP, a staging hostname, or a different port will not appear at the production HTTPS URL.

**Mitigation:** Import the approved XML into the production HTTPS URL before the competition and verify it there.

## Medium: public access is not operator security

**Condition:** The static app currently has no authentication or authorization.

**Impact:** Anyone who can open the site can operate their own local copy and may see its bundled seed data. This is not a centralized protected competition-control system.

**Mitigation:** Restrict site access at the IIS/network layer if required, and limit the authoritative browser/device to trained operators. Do not treat the current Users screen as security enforcement.

## Medium: browser download and print policies

**Condition:** XML, CSV, JSON, and Excel-compatible exports use browser Blob downloads; printing uses the browser print dialog.

**Impact:** Download blocking, pop-up/download restrictions, printer drivers, page-size defaults, or print scaling may affect operations.

**Mitigation:** Test each file export and each required print layout on the actual event devices and printers before competition day.

## Medium: modern-browser requirement

**Condition:** The app uses modern JavaScript APIs, including `structuredClone`, `replaceAll`, `File.text()`, Blob URLs, and `DOMParser`.

**Impact:** Internet Explorer and legacy Edge are unsupported.

**Mitigation:** Standardize on current stable Edge or Chrome and prevent unapproved browser substitution.

## Low: deployment integrity

**Condition:** Static relative paths require the runtime directory tree to stay intact.

**Impact:** Omitting `data/` or `js/storage/`, or changing directory names/casing, will cause missing seed data or JavaScript errors.

**Mitigation:** Upload only the runtime file set as one unit and complete the production smoke test with browser developer tools open to detect 404s.

## Not a current blocker: IIS configuration

**Condition:** Standard IIS Static Content and Default Document services are available.

**Impact:** None. The static application needs no custom `web.config`.

**Mitigation:** If the server cannot serve a runtime file, correct IIS site configuration and permissions rather than modifying application code.

