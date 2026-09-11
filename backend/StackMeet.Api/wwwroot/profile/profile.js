(() => {
  'use strict';

  const allowedId = /^NDT-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{7}$/;
  const byId = id => document.getElementById(id);
  const statusPanel = byId('statusPanel');
  const statusTitle = byId('statusTitle');
  const statusMessage = byId('statusMessage');
  const profilePanel = byId('profilePanel');

  function profileIdFromPath() {
    const parts = window.location.pathname.split('/').filter(Boolean);
    const index = parts.findIndex(part => part.toLowerCase() === 'stackers');
    if (index < 0 || !parts[index + 1]) return null;
    try {
      return decodeURIComponent(parts[index + 1]).trim().toUpperCase();
    } catch {
      return null;
    }
  }

  function setUnavailable() {
    statusPanel.classList.add('is-error');
    statusTitle.textContent = 'Profile not available';
    statusMessage.textContent = 'This public NADITrack profile is unavailable.';
    profilePanel.hidden = true;
  }

  function formatDate(value) {
    if (!value) return '—';
    const date = new Date(`${value}T00:00:00`);
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    }).format(date);
  }

  function formatTime(value) {
    const number = Number(value);
    return Number.isFinite(number) ? `${number.toFixed(3)} s` : '—';
  }

  function locationText(profile) {
    return [profile.club, profile.region, profile.country]
      .map(value => typeof value === 'string' ? value.trim() : '')
      .filter(Boolean)
      .join(' · ');
  }

  function appendText(parent, className, text) {
    const element = document.createElement('p');
    element.className = className;
    element.textContent = text;
    parent.appendChild(element);
    return element;
  }

  function performanceMeta(performance) {
    const penalty = Number(performance.appliedPenalty);
    const timing = Number.isFinite(penalty) && penalty > 0
      ? `Raw ${formatTime(performance.rawBestTime)} + ${penalty.toFixed(3)} s penalty`
      : `Raw ${formatTime(performance.rawBestTime)}`;
    return [performance.stage, timing].filter(Boolean).join(' · ');
  }

  function renderPersonalBests(personalBests) {
    const container = byId('personalBests');
    const empty = byId('noPersonalBests');
    container.replaceChildren();

    if (!Array.isArray(personalBests) || personalBests.length === 0) {
      empty.hidden = false;
      return;
    }

    empty.hidden = true;
    for (const pb of personalBests) {
      const card = document.createElement('article');
      card.className = 'pb-card';

      appendText(card, 'pb-event', pb.eventCode || 'Event');

      const time = document.createElement('strong');
      time.className = 'pb-time';
      time.textContent = formatTime(pb.officialTime);
      card.appendChild(time);

      const penalty = Number(pb.appliedPenalty);
      const penaltyText = Number.isFinite(penalty) && penalty > 0
        ? `Raw ${formatTime(pb.rawBestTime)} + ${penalty.toFixed(3)} s penalty`
        : `Raw ${formatTime(pb.rawBestTime)}`;
      appendText(card, 'pb-meta', penaltyText);

      const source = [
        pb.competitionName,
        formatDate(pb.competitionDate),
        pb.stage
      ].filter(Boolean).join(' · ');
      appendText(card, 'pb-source', source || 'Finalized public result');

      container.appendChild(card);
    }
  }

  function renderTournamentHistory(tournamentHistory) {
    const container = byId('tournamentHistory');
    const empty = byId('noTournamentHistory');
    container.replaceChildren();

    if (!Array.isArray(tournamentHistory) || tournamentHistory.length === 0) {
      empty.hidden = false;
      return;
    }

    empty.hidden = true;
    for (const tournament of tournamentHistory) {
      const card = document.createElement('article');
      card.className = 'tournament-card';

      const header = document.createElement('div');
      header.className = 'tournament-header';

      const heading = document.createElement('div');
      heading.className = 'tournament-heading';
      appendText(heading, 'tournament-date', formatDate(tournament.competitionDate));

      const name = document.createElement('h3');
      name.className = 'tournament-name';
      name.textContent = tournament.competitionName || 'Finalized competition';
      heading.appendChild(name);

      if (tournament.competitionKey) {
        appendText(heading, 'tournament-key', tournament.competitionKey);
      }

      header.appendChild(heading);
      card.appendChild(header);

      const performances = Array.isArray(tournament.performances)
        ? tournament.performances
        : [];

      if (performances.length === 0) {
        appendText(card, 'tournament-empty', 'Appearance recorded · no valid individual times available.');
      } else {
        const grid = document.createElement('div');
        grid.className = 'tournament-performance-grid';

        for (const performance of performances) {
          const item = document.createElement('div');
          item.className = 'tournament-performance';

          appendText(item, 'tournament-event', performance.eventCode || 'Event');

          const time = document.createElement('strong');
          time.className = 'tournament-time';
          time.textContent = formatTime(performance.officialTime);
          item.appendChild(time);

          appendText(item, 'tournament-performance-meta', performanceMeta(performance));
          grid.appendChild(item);
        }

        card.appendChild(grid);
      }

      container.appendChild(card);
    }
  }

  function renderProfile(profile) {
    byId('displayName').textContent = profile.displayName || 'NADITrack Stacker';
    byId('nadiTrackId').textContent = profile.nadiTrackId || '';
    byId('locationLine').textContent = locationText(profile) || profile.country || '';
    byId('competitionCount').textContent = String(profile.competitionCount ?? 0);
    byId('firstCompetitionDate').textContent = formatDate(profile.firstCompetitionDate);
    byId('latestCompetitionDate').textContent = formatDate(profile.latestCompetitionDate);
    renderPersonalBests(profile.personalBests);
    renderTournamentHistory(profile.tournamentHistory);

    document.title = `${profile.displayName || 'Stacker'} · NADITrack`;
    statusPanel.hidden = true;
    profilePanel.hidden = false;
  }

  async function loadProfile() {
    const nadiTrackId = profileIdFromPath();
    if (!nadiTrackId || !allowedId.test(nadiTrackId)) {
      setUnavailable();
      return;
    }

    try {
      const response = await fetch(`/api/public/stackers/${encodeURIComponent(nadiTrackId)}`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
        cache: 'no-store',
        credentials: 'omit'
      });

      if (!response.ok) {
        setUnavailable();
        return;
      }

      const profile = await response.json();
      if (!profile || profile.nadiTrackId !== nadiTrackId) {
        setUnavailable();
        return;
      }

      renderProfile(profile);
    } catch {
      setUnavailable();
    }
  }

  loadProfile();
})();
