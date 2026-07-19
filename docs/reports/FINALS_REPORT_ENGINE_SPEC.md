# Finals Report Engine

Pipeline: competition state → Finals stage → participant/category/gender/filter selection → shared classification/ranking → DTO rows → screen, print, and CSV. `js/reports/FinalsReportEngine.js` owns result classification, tie keys/ranks, final placement rows, All-Around rows, category/gender filters, and organization credits. Renderers do not reimplement ranking.

Qualification snapshots are created only by the explicit **Generate Draft Qualification Snapshots** action. Reports never regenerate them. Approved snapshots are immutable; a confirmed regeneration preserves them and marks them Superseded before creating new Draft records.
