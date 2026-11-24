# ENG-02F — Browser security hardening (draft)

Base: 9dc6e0833d89ef72144aec0a27f90449b51c5e48. Stacked on validated ENG-02E. No production deployment or schema change.

## Implemented controls

- Enforce HTML CSP restricting scripts to same-origin sources, disallowing plugin objects, base URL injection and framing, and limiting form submissions to same-origin. No unsafe-inline or unsafe-eval script exception. Existing locally hosted SignalR bundle has a global-object discovery fallback using Function only after checking globalThis; current browser runtime must be verified. Development Swagger middleware remains outside this policy.
- Add a stricter resource policy in report-only mode (no collection endpoint; browser console inspection). Inline style allowance is intentional for existing layout/print, QR image host remains explicitly listed. Do not enforce the wider policy until real browser and WebSocket validation.
- Remove inline print handlers in active app and archived/extracted printing helper; use delegated print action returning before save/render. Escape a previously unescaped formatted dashboard rounds value and translation.
- Constrain branding URLs to the exact public asset endpoint and expected asset type (operator additionally validates selected competition ID); retain known local fallback images.
- Remove activation/reset token from address bar via history replacement, hold it only in page memory and clear after successful submission. Refreshing or closing an unfinished account page may require reopening the original email link. This cannot erase the original request from server/proxy logs or browser systems that already recorded it.
- Set no-store on HTML and non-public API responses including failures. Existing public asset ETag policy and static script caching remain. Bump affected shell script version keys.

## Validation

New isolated loopback header tests and Node source/VM scenarios for branding URL filtering, print delegation, dashboard escaping, token scrubbing and successful consumption. Prior integration tests remain unchanged. Hosted CI and real browser tests pending.

## Residuals and rollout

This is defense in depth, not a comprehensive XSS proof. Existing sessionStorage bearer/admin key contracts remain; migrating to HttpOnly cookies requires dedicated CSRF/session design and owner-approved compatibility plan. Existing third-party QR generation receives the public results URL; use local QR generation in a later package and review unlisted link disclosure. CSP does not prevent all same-origin script or markup injection; wider DOM sink inventory and browser tests still needed. Confirm actual IIS response headers and cache behavior, operator/administrator login, recovery email, SignalR reconnect, all three locales, downloads and printing before release. No historical record/certificate modifications.
