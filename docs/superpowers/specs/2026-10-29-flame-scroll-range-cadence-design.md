# Flame Scroll range and cadence

## Problem

Scroll of Flame currently uses a 3-tile target-area diameter. Its two configured
attack rounds are also inconsistent: only the first is guaranteed, while the
second is discarded with a distance-based probability. In crowded melee this
leaves visually nearby monsters outside the area and commonly produces only one
damage event per cast.

## Design

- Increase Flame and its master replacements to a 5-tile target-area diameter
  (2.5-tile radius), covering monsters up to two tiles from the cast centre,
  including diagonal neighbours.
- Configure exactly two guaranteed hits per target, retaining the existing
  500 ms delay between hits.
- Update both fresh Season 6 initialization and existing configurations through
  one mandatory, idempotent update plug-in. Preserve every unrelated skill and
  area-skill setting.

## Verification

- Tests first demonstrate the current 3-tile/one-guaranteed-hit behavior.
- Update-plug-in tests cover base Flame and a master replacement and prove
  unrelated settings are preserved.
- Fresh throwaway in-memory initialization must produce diameter 5, minimum 2,
  maximum 2, and a 500 ms delay.
- No live or local `openmu` database is used during testing.
