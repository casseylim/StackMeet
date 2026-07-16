# StackMeet RC1 Online Mode Status Investigation

## Conclusion

The UI reports **Local mode** because the render-time `translateChrome()` function overwrites the correct online status markup with two legacy translation keys. This is a display defect only; the current repository is configured to use the online API.

## 1. Source of the legacy text

The legacy source strings remain in the translation packs:

- `Local mode`
- `Saved in this browser`

More importantly, `app.js` calls the following on every render:

```javascript
document.querySelector(".sidebar-card span").textContent = t("Local mode");
document.querySelector(".sidebar-card strong").textContent = t("Saved in this browser");
```

This runs after the HTML shell loads and replaces the source markup:

```html
<span>Online mode</span>
<strong id="saveStatus" data-state="saved">Saved</strong>
```

It also overwrites the text managed by the new `saveStatus` indicator whenever the application renders.

## 2. Repository provider

`Repository` constructs `new ApiProvider("DEFAULT")`. `ApiProvider` sends:

- `GET /api/state/DEFAULT`
- `POST /api/state/DEFAULT`

The current repository does not construct or select a local-storage provider.

## 3. LocalStorageProvider compilation status

`LocalStorageProvider.js` is absent from all three active frontend locations:

- source `js/storage/`
- API `wwwroot/js/storage/`
- published `publish/wwwroot/js/storage/`

No non-Markdown source/runtime reference to `LocalStorageProvider` was found.

## 4. Hosted and published assets

The source, API `wwwroot`, and published `wwwroot` copies of both `index.html` and `app.js` have matching SHA-256 hashes. Therefore the current publish artifact contains the same legacy render-time overwrite.

## Finding

The **Local mode** display is not evidence of local persistence. It is caused by obsolete presentation text in `translateChrome()` and its translation packs overriding the API-backed save-status markup.

## Scope

Investigation only. No production code was modified.
