CREATE TABLE IF NOT EXISTS hands (
  hand_id TEXT PRIMARY KEY,
  timestamp_utc TEXT NOT NULL,
  game TEXT NOT NULL,
  max_seats INTEGER NOT NULL,
  button_seat INTEGER NOT NULL,
  small_blind REAL NOT NULL,
  big_blind REAL NOT NULL,
  currency TEXT NOT NULL,
  raw_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS hand_players (
  hand_id TEXT NOT NULL,
  seat_no INTEGER NOT NULL,
  player_id TEXT NOT NULL,
  display_name TEXT NULL,
  starting_stack REAL NOT NULL,
  is_hero INTEGER NOT NULL,
  PRIMARY KEY (hand_id, seat_no),
  FOREIGN KEY (hand_id) REFERENCES hands(hand_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS hand_events (
  hand_id TEXT NOT NULL,
  seq INTEGER NOT NULL,
  street TEXT NOT NULL,
  type TEXT NOT NULL,
  actor_seat INTEGER NULL,
  amount REAL NULL,
  to_amount REAL NULL,
  cards_json TEXT NULL,
  pot_after REAL NULL,
  actor_stack_after REAL NULL,
  note TEXT NULL,
  PRIMARY KEY (hand_id, seq),
  FOREIGN KEY (hand_id) REFERENCES hands(hand_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_hands_timestamp ON hands(timestamp_utc);
CREATE INDEX IF NOT EXISTS idx_hand_events_hand_street ON hand_events(hand_id, street);
