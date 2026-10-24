"""Render the approved reward plan as a transactional psql maintenance script.

This generator never connects to a database. The resulting SQL previews with a
rollback unless psql is explicitly given apply=true and expected_database.
"""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
LEVELS = {"SILVER MEDAL": 5, "GOLD MEDAL": 6, "HEART OF LOVE": 3,
          "FIRECRACKER": 2, "BOX OF HEAVEN": 7}
LEVELS.update({f"BOX OF KUNDUN +{tier}": tier + 7 for tier in range(1, 6)})


def reward_groups(items, rules):
    """Expand category weights into complete groups, preserving item-level odds."""
    result = []
    for box, source_level in LEVELS.items():
        excellent = box.startswith("BOX OF KUNDUN")
        weights = {"armor": .45, "weapons": .45, "shields": .10} if excellent else rules["weights"][box]
        for category, chance in weights.items():
            pool = rules["jewels"].get(box, items[box].get(category, [])) if category == "jewels" else items[box].get(category, [])
            if category == "rings":
                pool = [[13, number] for number in (8, 9, 21, 22, 23, 24)]
            minimum, maximum = (0, 0) if excellent or category not in ("armor", "weapons", "shields") else ((9, 11) if box == "HEART OF LOVE" else (7, 9))
            if category == "rings":
                minimum, maximum = 0, 4
            parts = [(category, pool, chance, minimum, maximum, None)]
            if category == "fruits":
                parts = [(f"fruit-{entry[2]}", [entry[:2]], chance / len(pool), entry[2], entry[2], None) for entry in pool]
            elif category == "weapons" and not excellent:
                bows = [entry for entry in pool if entry[0] == 4]
                other = [entry for entry in pool if entry[0] != 4]
                parts = [(name, subset, chance * len(subset) / len(pool), minimum, maximum, skill)
                         for name, subset, skill in (("bows", bows, .9), ("weapons", other, .5)) if subset]
            for label, selected, weight, low, high, skill in parts:
                if category != "zen" and not selected:
                    raise ValueError(f"Empty reward category: {box}/{category}")
                result.append({"id": f"24b00000-0000-4000-8000-{len(result):012d}",
                               "description": f"Approved boxes: {box} / {label}",
                               "level": source_level, "chance": weight,
                               "type": 2 if excellent else (5 if category == "zen" else 3),
                               "minimum": low, "maximum": high,
                               "effect": 1 if box in ("FIRECRACKER", "HEART OF LOVE") else (2 if box in ("SILVER MEDAL", "GOLD MEDAL", "BOX OF HEAVEN") else 0),
                               "money": rules["zen"].get(box, 0) if category == "zen" else 0,
                               "second": [.30, .25, .20, .15, .10][source_level - 8] if excellent else None,
                               "skill": .5 if category == "shields" and not excellent else skill,
                               "jewelry": category == "rings", "items": selected})
        total = sum(group["chance"] for group in result if group["level"] == source_level)
        if abs(total - 1) > 1e-12:
            raise ValueError(f"Reward chances do not total 100%: {box}: {total}")
    return result


def render():
    """Return SQL containing only the reviewed manifests and template."""
    items = json.loads((ROOT / "box-reward-items.json").read_text(encoding="utf-8"))
    rules = json.loads((ROOT / "box-reward-rules.json").read_text(encoding="utf-8"))
    bonuses = [{**entry, "level": LEVELS[entry["bonus"]],
                "id": f"24b00001-0000-4000-8000-{index:012d}"}
               for index, entry in enumerate(rules["eligibleMonsters"])]
    payload = json.dumps({"rewards": reward_groups(items, rules), "bonuses": bonuses,
                          "excludedItems": rules["excludedItems"], "excludedWrapperLevels": rules["excludedWrapperLevels"],
                          "totalChance": rules["totalBonusChance"]}, separators=(",", ":"))
    return (ROOT / "box-rewards.template.sql").read_text(encoding="utf-8").replace("__APPROVED_PLAN__", payload)


if __name__ == "__main__":
    print(render())
