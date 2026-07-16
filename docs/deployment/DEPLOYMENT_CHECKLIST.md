# StackMeet deployment checklist

## Readiness decision

**Ready to deploy as a static IIS site.** No production-code change or `web.config` is required for a normal IIS installation with the **Static Content** and **Default Document** role services enabled.

This is a browser-local application. It does not use an API, database, server-side routing, fetch requests, or JavaScript modules.

## Runtime files to upload

Upload exactly this runtime file set, retaining the directory structure:

```text
index.html
styles.css
app.js
assets/
  competition-banner.png
  stackmeet-logo.png
data/
  stacktrack-4257-stackers.js
js/
  storage/LocalStorageProvider.js
  storage/Repository.js
```

Do not upload `database/`, `docs/`, `tests/`, `backups/`, `exports/`, `releases/`, `screenshots/`, `temp/`, or project-management files unless a separate operational process specifically needs them. The application does not read those files at runtime.

## Review results

| Area | Result | Deployment note |
| --- | --- | --- |
| Relative paths | Pass | `index.html` uses relative links for CSS, scripts, data, and PNG assets. Deploy the listed tree together. |
| Asset loading | Pass | Both image assets are local and use relative `assets/...` URLs. |
| JavaScript loading | Pass | Four classic scripts load in dependency order: seed data, storage provider, repository, then `app.js`. No ES modules, imports, build step, or server APIs are used. |
| Print | Pass, test on target printer | Print CSS uses `@media print` and `@page`; browser print dialogs are invoked with `window.print()`. |
| XML import/export | Pass, test on target browser | Export uses `Blob` downloads; import uses the standard file input, `File.text()`, and `DOMParser`. No server upload is involved. |
| HTTPS and localStorage | Pass | `localStorage` works on an HTTPS origin. Its data is isolated by exact scheme, host, and port; HTTP test data will not appear after HTTPS deployment. |
| IIS static hosting | Pass | `index.html` is the default document. No rewrite rules or MIME mappings are needed for the runtime file set. |

## IIS prerequisites

- Install IIS role services: **Static Content** and **Default Document**.
- Bind the production hostname with a valid HTTPS certificate; redirect HTTP to HTTPS at the site or reverse-proxy layer.
- Set the site physical path to the folder containing `index.html`.
- Ensure the application-pool identity has read access to the deployment folder.
- Confirm IIS does not have a restrictive request-filtering rule that blocks `.js`, `.css`, `.png`, or `.xml` downloads.
- Do not enable directory browsing; it is unnecessary.

## `web.config` decision

**Do not add a `web.config` for this deployment.** IIS can serve this static application without one when its normal Static Content and Default Document features are installed. Add configuration only if the server team has non-standard global IIS restrictions; that would be a server-specific operation, not an application requirement.

## Post-deployment smoke test

1. Open `https://<production-host>/` in Microsoft Edge or Google Chrome.
2. Confirm the dashboard renders with the logo and banner; use DevTools Network to confirm no 404s.
3. Add a temporary stacker, refresh the page, and confirm the entry remains.
4. Export XML, verify the browser downloads `stackmeet-data.xml`, then import it into a fresh browser profile.
5. Generate one report and one time sheet; use the browser print preview and print to PDF.
6. Download one CSV/JSON report.
7. Repeat the core test in the event browser/device and confirm the HTTPS padlock is present.

