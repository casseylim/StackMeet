-- StackMeet database schema
-- Designed first for SQLite/PostgreSQL-style SQL, with clean migration from XML.

CREATE TABLE competitions (
  id INTEGER PRIMARY KEY,
  public_code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  competition_type TEXT NOT NULL CHECK (competition_type IN ('Sanctioned', 'Recreational', 'Online')),
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  kbs_logo_enabled BOOLEAN NOT NULL DEFAULT 0,
  soc_enabled BOOLEAN NOT NULL DEFAULT 1,
  prelim_times_mode TEXT NOT NULL DEFAULT 'best' CHECK (prelim_times_mode IN ('best', 'all')),
  paperless_enabled BOOLEAN NOT NULL DEFAULT 1,
  advance_individuals INTEGER NOT NULL DEFAULT 10,
  advance_doubles INTEGER NOT NULL DEFAULT 6,
  advance_cp_doubles INTEGER NOT NULL DEFAULT 5,
  advance_relay INTEGER NOT NULL DEFAULT 0,
  time_sheet_input_mode TEXT NOT NULL DEFAULT 'blank',
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE event_groups (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE events (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE competition_events (
  competition_id INTEGER NOT NULL,
  event_group_id INTEGER NOT NULL,
  event_id INTEGER NOT NULL,
  enabled BOOLEAN NOT NULL DEFAULT 1,
  PRIMARY KEY (competition_id, event_group_id, event_id),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (event_group_id) REFERENCES event_groups(id),
  FOREIGN KEY (event_id) REFERENCES events(id)
);

CREATE TABLE divisions (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  division_type TEXT NOT NULL DEFAULT 'individual',
  min_age INTEGER,
  max_age INTEGER,
  gender TEXT CHECK (gender IN ('M', 'F', 'C')),
  special_category TEXT,
  sort_order INTEGER NOT NULL DEFAULT 0,
  UNIQUE (competition_id, name),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE division_cutoffs (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  cutoff_group TEXT NOT NULL CHECK (cutoff_group IN ('combined', 'male', 'female', 'special')),
  cutoff_age INTEGER NOT NULL CHECK (cutoff_age >= 0),
  enabled BOOLEAN NOT NULL DEFAULT 1,
  UNIQUE (competition_id, cutoff_group, cutoff_age),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE organizations (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  country TEXT,
  region TEXT,
  UNIQUE (competition_id, name),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE stackers (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  bib_code TEXT NOT NULL,
  first_name TEXT,
  last_name TEXT,
  display_name TEXT NOT NULL,
  native_name TEXT,
  gender TEXT NOT NULL CHECK (gender IN ('M', 'F')),
  birth_date DATE,
  age_on_competition_day INTEGER,
  is_special BOOLEAN NOT NULL DEFAULT 0,
  organization_id INTEGER,
  division_id INTEGER,
  country TEXT,
  region TEXT,
  email TEXT,
  phone TEXT,
  paid BOOLEAN NOT NULL DEFAULT 0,
  checked_in BOOLEAN NOT NULL DEFAULT 0,
  notes TEXT,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (competition_id, bib_code),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (organization_id) REFERENCES organizations(id),
  FOREIGN KEY (division_id) REFERENCES divisions(id)
);

CREATE TABLE teams (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  team_code TEXT NOT NULL,
  team_type TEXT NOT NULL CHECK (team_type IN ('doubles', 'relay', 'hth')),
  name TEXT NOT NULL,
  division_id INTEGER,
  country TEXT,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (competition_id, team_code),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (division_id) REFERENCES divisions(id)
);

CREATE TABLE team_members (
  team_id INTEGER NOT NULL,
  stacker_id INTEGER NOT NULL,
  member_order INTEGER NOT NULL DEFAULT 1,
  role TEXT,
  PRIMARY KEY (team_id, stacker_id),
  FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE CASCADE,
  FOREIGN KEY (stacker_id) REFERENCES stackers(id) ON DELETE CASCADE
);

CREATE TABLE stages (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE competition_stages (
  competition_id INTEGER NOT NULL,
  stage_id INTEGER NOT NULL,
  enabled BOOLEAN NOT NULL DEFAULT 1,
  round_count INTEGER NOT NULL DEFAULT 1 CHECK (round_count >= 0),
  sort_order INTEGER NOT NULL DEFAULT 0,
  notes TEXT,
  PRIMARY KEY (competition_id, stage_id),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (stage_id) REFERENCES stages(id)
);

CREATE TABLE time_sheets (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  sheet_code TEXT NOT NULL,
  stage_id INTEGER NOT NULL,
  event_group_id INTEGER NOT NULL,
  event_id INTEGER NOT NULL,
  division_id INTEGER,
  participant_type TEXT NOT NULL CHECK (participant_type IN ('stacker', 'team')),
  stacker_id INTEGER,
  team_id INTEGER,
  round_number INTEGER NOT NULL DEFAULT 1,
  status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'entered', 'missing', 'scratched')),
  UNIQUE (competition_id, sheet_code),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (stage_id) REFERENCES stages(id),
  FOREIGN KEY (event_group_id) REFERENCES event_groups(id),
  FOREIGN KEY (event_id) REFERENCES events(id),
  FOREIGN KEY (division_id) REFERENCES divisions(id),
  FOREIGN KEY (stacker_id) REFERENCES stackers(id),
  FOREIGN KEY (team_id) REFERENCES teams(id)
);

CREATE TABLE results (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  time_sheet_id INTEGER,
  stage_id INTEGER NOT NULL,
  event_group_id INTEGER NOT NULL,
  event_id INTEGER NOT NULL,
  division_id INTEGER,
  participant_type TEXT NOT NULL CHECK (participant_type IN ('stacker', 'team')),
  stacker_id INTEGER,
  team_id INTEGER,
  penalty_seconds NUMERIC(8,3) NOT NULL DEFAULT 0,
  is_scratch BOOLEAN NOT NULL DEFAULT 0,
  official_time NUMERIC(8,3),
  entered_by_user_id INTEGER,
  entered_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  notes TEXT,
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (time_sheet_id) REFERENCES time_sheets(id),
  FOREIGN KEY (stage_id) REFERENCES stages(id),
  FOREIGN KEY (event_group_id) REFERENCES event_groups(id),
  FOREIGN KEY (event_id) REFERENCES events(id),
  FOREIGN KEY (division_id) REFERENCES divisions(id),
  FOREIGN KEY (stacker_id) REFERENCES stackers(id),
  FOREIGN KEY (team_id) REFERENCES teams(id),
  FOREIGN KEY (entered_by_user_id) REFERENCES users(id)
);

CREATE TABLE result_attempts (
  id INTEGER PRIMARY KEY,
  result_id INTEGER NOT NULL,
  attempt_number INTEGER NOT NULL CHECK (attempt_number BETWEEN 1 AND 3),
  raw_time NUMERIC(8,3),
  is_scratch BOOLEAN NOT NULL DEFAULT 0,
  UNIQUE (result_id, attempt_number),
  FOREIGN KEY (result_id) REFERENCES results(id) ON DELETE CASCADE
);

CREATE TABLE leaderboard_settings (
  competition_id INTEGER PRIMARY KEY,
  display_type TEXT NOT NULL DEFAULT 'Divisional Results',
  stage_id INTEGER,
  background_color TEXT NOT NULL DEFAULT 'Black',
  progress_color TEXT NOT NULL DEFAULT 'Blue',
  pause_seconds INTEGER NOT NULL DEFAULT 8,
  result_limit INTEGER NOT NULL DEFAULT 10,
  logo_path TEXT,
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (stage_id) REFERENCES stages(id)
);

CREATE TABLE competition_access_codes (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  access_level TEXT NOT NULL CHECK (access_level IN ('Data Entry', 'Staff', 'Tournament Director', 'WSSA TD')),
  password_hash TEXT NOT NULL,
  active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (competition_id, access_level),
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE users (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER,
  display_name TEXT NOT NULL,
  access_level TEXT NOT NULL CHECK (access_level IN ('Data Entry', 'Staff', 'Tournament Director', 'WSSA TD')),
  is_active BOOLEAN NOT NULL DEFAULT 1,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE user_sessions (
  id INTEGER PRIMARY KEY,
  user_id INTEGER NOT NULL,
  last_active_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  platform TEXT,
  browser TEXT,
  ip_address TEXT,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE notifications (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  title TEXT NOT NULL,
  body TEXT,
  source TEXT NOT NULL DEFAULT 'system',
  is_read BOOLEAN NOT NULL DEFAULT 0,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE
);

CREATE TABLE paperwork_jobs (
  id INTEGER PRIMARY KEY,
  competition_id INTEGER NOT NULL,
  paperwork_type TEXT NOT NULL,
  options_json TEXT,
  generated_by_user_id INTEGER,
  generated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (competition_id) REFERENCES competitions(id) ON DELETE CASCADE,
  FOREIGN KEY (generated_by_user_id) REFERENCES users(id)
);

CREATE INDEX idx_stackers_competition_division ON stackers(competition_id, division_id);
CREATE INDEX idx_stackers_name ON stackers(display_name);
CREATE INDEX idx_teams_competition_type ON teams(competition_id, team_type);
CREATE INDEX idx_results_comp_stage_event ON results(competition_id, stage_id, event_id);
CREATE INDEX idx_results_stacker ON results(stacker_id);
CREATE INDEX idx_results_team ON results(team_id);
CREATE INDEX idx_time_sheets_status ON time_sheets(competition_id, status);
CREATE INDEX idx_notifications_unread ON notifications(competition_id, is_read);
CREATE INDEX idx_competitions_public_code ON competitions(public_code);
CREATE INDEX idx_access_codes_competition ON competition_access_codes(competition_id, access_level);
CREATE INDEX idx_competition_stages_enabled ON competition_stages(competition_id, enabled);
CREATE INDEX idx_division_cutoffs_competition ON division_cutoffs(competition_id, cutoff_group, cutoff_age);
