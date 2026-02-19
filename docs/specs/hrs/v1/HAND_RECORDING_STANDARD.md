# Hand Recording Standard (HRS) — NLHE 6-Max — v1.0 (Draft)

## 1. Purpose
Define a canonical, machine-readable format for recording a single No-Limit Hold’em (NLHE) 6-max hand for post-session training and analysis.

This standard supports:
- Full hand replay (state reconstruction at every decision point)
- Consistent database storage + querying
- Model training dataset extraction

Non-goals (v1):
- Real-time table assistance
- Run-it-twice / multiple boards
- Tournament ICM / payout structures

---

## 2. Canonical Format Overview
Hands are stored as:
1) **Raw Source** (optional): original room hand history text / source JSON
2) **Canonical Hand JSON** (required): event-stream representation

The canonical truth is the ordered list of **events**, which can be replayed to reconstruct state.

---

## 3. Definitions

### 3.1 Streets
- PREFLOP
- FLOP
- TURN
- RIVER
- SHOWDOWN (optional event grouping)
- SUMMARY (optional)

### 3.2 Seats & Positions (6-max)
Seat numbers: 1..6  
Button seat determines positions:
- BTN, SB, BB, UTG, HJ, CO (as applicable with empty seats)

Store:
- `button_seat` (int)
- `players[].seat_no` (int)

---

## 4. Card Encoding
- Rank: A,K,Q,J,T,9..2
- Suit: s,h,d,c
- Example: `"As"`, `"Td"`

Board:
- FLOP: 3 cards
- TURN: 1 card
- RIVER: 1 card

---

## 5. Canonical Hand JSON Structure (v1)

### 5.1 Top-level fields
Required:
- `hand_id` (string, unique)
- `game` = "NLHE"
- `max_seats` = 6
- `timestamp_utc` (ISO-8601)
- `stakes` { `small_blind`, `big_blind`, `currency` }
- `button_seat` (int)
- `players` (array)
- `events` (array, ordered)

Optional:
- `table_name`
- `source` { `provider`, `raw_hand_history` }

### 5.2 Players
Each player:
Required:
- `seat_no` (int)
- `player_id` (string, stable anon id)
- `starting_stack` (number)
- `is_hero` (bool)

Optional:
- `display_name`

### 5.3 Events (the core)
Each event:
Required:
- `seq` (int, strictly increasing)
- `street` (enum)
- `type` (enum)
- `actor_seat` (int or null for dealer events)
- `amount` (number or null)
Optional:
- `to_amount` (number) — for RAISE events only
- `cards` (array) — for DEAL_* events
- `pot_after` (number)
- `actor_stack_after` (number)

#### Event types (v1)
Player action events:
- `POST_SB`
- `POST_BB`
- `POST_ANTE`
- `FOLD`
- `CHECK`
- `CALL`
- `BET`
- `RAISE`
- `ALL_IN` (optional; can be represented as BET/RAISE with stack cap)

Dealer / state events:
- `DEAL_HOLE` (hero only unless known)
- `DEAL_FLOP`
- `DEAL_TURN`
- `DEAL_RIVER`
- `SHOWDOWN` (optional)
- `PAYOUT` (optional; can be in outcome section)

---

## 6. Action Semantics (critical)


### 6.1 Amount vs ToAmount
- `amount`: chips committed by the actor **in this single event**
- `to_amount`: total committed by the actor **this street after the action**
  - **Required** for `RAISE`
  - **Optional** for `ALL_IN` (useful when the all-in is effectively a raise-to)

### 6.2 Pot and Stack Tracking
If included, `pot_after` and `actor_stack_after` must match replay calculations.

---

## 7. Validation Rules (v1)
A hand is valid if:
- `seq` values are contiguous or strictly increasing
- No player stack goes below 0
- Pot progression equals sum of committed amounts (minus rake if recorded)
- Street transitions happen only after action completion rules are satisfied
- Board card counts are correct

---

## 8. Database Mapping (v1)
Canonical storage:
- `hands` (hand_id, timestamp, stakes, button_seat, hero_seat, etc.)
- `hand_players` (hand_id, seat_no, player_id, starting_stack, is_hero)
- `hand_events` (hand_id, seq, street, type, actor_seat, amount, to_amount, pot_after, actor_stack_after, payload_json)
- `hand_board` (hand_id, flop1..flop3, turn, river)
- `hand_outcomes` (hand_id, rake, winners_json, hero_net)

Indexes:
- (hand_id)
- (timestamp_utc)
- (is_hero, timestamp_utc)
- (stakes.big_blind, timestamp_utc)

---

## 9. Examples
See `/examples/hands/v1/` for canonical JSON examples.

Required examples for v1:
- Heads-up pot with simple betting
- Multiway pot with fold(s)
- All-in scenario
- Side pot scenario

---

## 10. Versioning
- This document is versioned using semver: vMAJOR.MINOR
- Schema changes that break compatibility bump MAJOR
- Additive fields bump MINOR

