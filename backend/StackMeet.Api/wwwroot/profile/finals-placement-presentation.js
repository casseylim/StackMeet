(function (root, factory) {
  'use strict';

  const api = factory();
  if (typeof module === 'object' && module.exports) {
    module.exports = api;
  } else {
    root.NadiTrackFinalsPlacementPresentation = api;
  }
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const PUBLICATION_VERSION = 'sp4m-public-finals-placement-v1';
  const COHORT_POLICY = 'Competition-time division · mixed category · no additional gender filter';
  const SUPPORTED_EVENTS = new Set(['3-3-3', '3-6-3', 'Cycle']);
  const RESULT_STATUSES = new Set(['Valid', 'Scratch', 'Missing', 'Invalid']);
  const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

  function nonBlank(value) {
    return typeof value === 'string' && value.trim().length > 0;
  }

  function positiveNumber(value) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0;
  }

  function validPoint(point) {
    if (!point || typeof point !== 'object') return false;
    if (!nonBlank(point.competitionKey) || !nonBlank(point.competitionName)) return false;
    if (typeof point.competitionDate !== 'string' || !DATE_ONLY.test(point.competitionDate)) return false;
    if (!SUPPORTED_EVENTS.has(point.eventCode)) return false;
    if (!RESULT_STATUSES.has(point.resultStatus)) return false;
    if (typeof point.sharesPlacement !== 'boolean') return false;

    if (point.resultStatus === 'Valid') {
      const placement = Number(point.placement);
      return Number.isInteger(placement)
        && placement > 0
        && positiveNumber(point.officialBestTime);
    }

    return point.placement == null
      && point.officialBestTime == null
      && point.sharesPlacement === false;
  }

  function validatePublication(publication, expectedNadiTrackId) {
    if (!publication || typeof publication !== 'object') return null;
    if (!nonBlank(expectedNadiTrackId)) return null;
    if (publication.publicationVersion !== PUBLICATION_VERSION) return null;
    if (publication.nadiTrackId !== expectedNadiTrackId) return null;
    if (publication.cohortPolicy !== COHORT_POLICY) return null;
    if (!Array.isArray(publication.history)) return null;
    if (!publication.history.every(validPoint)) return null;
    return publication;
  }

  function placementText(point) {
    if (!validPoint(point) || point.resultStatus !== 'Valid') {
      return 'No certified placement for this Finals result';
    }

    return point.sharesPlacement
      ? `Certified shared placement in competition-time division: #${point.placement}`
      : `Certified placement in competition-time division: #${point.placement}`;
  }

  return Object.freeze({
    PUBLICATION_VERSION,
    COHORT_POLICY,
    validatePublication,
    placementText
  });
});
