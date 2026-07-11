-- Starter lookup data.

INSERT INTO event_groups (id, code, name, sort_order) VALUES
  (1, 'individuals', 'Individuals', 1),
  (2, 'doubles', 'Doubles', 2),
  (3, 'timed_relay', 'Timed Relay', 3),
  (4, 'head_to_head', 'Head To Head', 4);

INSERT INTO events (id, code, name, sort_order) VALUES
  (1, '3-3-3', '3-3-3', 1),
  (2, '3-6-3', '3-6-3', 2),
  (3, 'cycle', 'Cycle', 3);

INSERT INTO stages (id, code, name, sort_order) VALUES
  (1, 'prelims', 'Prelims', 1),
  (2, 'finals', 'Finals', 2),
  (3, 'soc', 'Stack Of Champions', 3),
  (4, 'hth', 'Head To Head', 4);
