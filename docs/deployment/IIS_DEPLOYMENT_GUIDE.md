# IIS deployment guide

## Scope

This guide deploys the existing StackMeet static application unchanged. It does not create an API, connect SQL Server, add ASP.NET Core, or alter competition rules.

## Deploy

1. On the IIS server, create a site (or virtual application) whose physical path is the deployment folder.
2. Copy the runtime files listed in [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md) into that folder, preserving `assets`, `data`, and `js/storage`.
3. Enable **Static Content** and **Default Document** in IIS. Confirm `index.html` is accepted as the default document.
4. Grant the IIS application-pool identity read permission to the deployment folder.
5. Add an HTTPS binding for the production hostname and its certificate. Redirect HTTP to HTTPS using the standard server/site policy.
6. Browse to the root URL and complete the post-deployment smoke test.

## URL placement

The application may be hosted at a site root or a virtual directory because all runtime references are relative. When using a virtual directory, access it with a trailing slash, for example:

```text
https://competition.example/stackmeet/
```

Do not browse directly to a deep hash view before first loading the application. Use the base URL first; application navigation uses hash routes such as `#reports`, which IIS never receives.

## No application `web.config`

No `web.config` is needed or recommended for a standard static IIS deployment. The app has no extensionless routes, no SPA server rewrites, and no application-defined headers or MIME requirements.

If the server does not serve `index.html`, `.js`, `.css`, or `.png`, correct the IIS role-service, default-document, request-filtering, or site-permission configuration. Do not compensate by changing StackMeet code.

## Publish safely

- Deploy to a new folder or take a filesystem backup before replacing a live copy.
- Keep the entire listed runtime set from one tested revision together; do not mix `app.js` from one release with `index.html` or data files from another.
- Verify the deployed bytes are the intended release before switching the IIS site path or binding.
- Do not place XML backups in the public web root. Store them in an access-controlled operational backup location.

## Important operating model

The current application saves competition state in the browser's `localStorage` under `stackmeet-stacktrack-style-v1`. IIS and HTTPS serve the files but do not centralize that data.

Each unique origin and browser profile has its own independent state. In particular, HTTP versus HTTPS, a different hostname, a non-default port, incognito/private windows, and different operator computers should be treated as separate stores. (The normal HTTPS default port 443 is the same origin as an HTTPS URL with no port shown.) A browser storage clear, profile reset, or storage quota error can remove or prevent saving local data.

Use XML export/import for handover and backup. Do not assume a file exported by one operator has been uploaded to the server; it is downloaded to that operator's device.

## Browser support

Support the current stable Microsoft Edge and Google Chrome on Windows. The application relies on modern browser APIs including `structuredClone`, `String.prototype.replaceAll`, Blob object URLs, `DOMParser`, `File.text()`, and CSS print rules. It is not suitable for Internet Explorer or legacy Edge. Test Firefox only if it will be used during the event.
