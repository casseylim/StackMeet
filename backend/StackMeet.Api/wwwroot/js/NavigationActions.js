(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetNavigationActions = api;
})(typeof window !== "undefined" ? window : null, function () {
  "use strict";

  function navigate(dataset, ports) {
    if (!ports.leaveResultEditors(dataset.route)) return false;
    ports.closeNavigationDrawer();
    if (["finals", "admin"].includes(dataset.reportTab)) ports.setReportTab(dataset.reportTab);
    ports.setRoute(dataset.route);
    ports.render();
    return true;
  }

  // These six actions only build or print existing display data.
  // No repository, save callback, or mutable competition state is provided.
  function dispatchPrint(action, dataset, ports) {
    if (action === "print-current-view") { ports.print(); return true; }
    if (action === "print-stacker-sheet") { ports.stackerSheet(dataset.id); return true; }
    if (action === "paperwork") { ports.paperwork(dataset.type); return true; }
    if (action === "print-paper-preview") { ports.preview(); return true; }
    if (action === "build-bracket") { ports.bracket(); return true; }
    if (action === "print-final-sheet") { ports.finalSheet(); return true; }
    return false;
  }

  return Object.freeze({ navigate, dispatchPrint });
});
