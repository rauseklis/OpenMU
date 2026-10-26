"""Expand the reviewed Star pool with equal overall shares for seven classes."""


def star_reward_groups(classes, start_index):
    """Keep 60/35/5 globally without assigning unusable shields to Summoner/RF."""
    result = []
    shield_classes = sum(entry['shield'] is not None for entry in classes)
    shield_share = .05 * len(classes) / shield_classes
    for entry in classes:
        shield = entry['shield']
        weapon_share = .40 - (shield_share if shield is not None else 0)
        selections = [('armor', entry['armor'], .60, 6, 9, None)]
        weapons = entry['weapons']
        for label, skill, predicate in (
                ('books', 1, lambda item: item[0] == 5 and item[1] in (21, 22, 23)),
                ('bows', .9, lambda item: item[0] == 4),
                ('weapons', .5, lambda item: item[0] != 4 and not (item[0] == 5 and item[1] in (21, 22, 23)))):
            selected = [item for item in weapons if predicate(item)]
            if selected:
                selections.append((label, selected, weapon_share * len(selected) / len(weapons), 6, 9, skill))
        if shield is not None:
            level = 8 if shield in (11, 16) else 7
            selections.append(('shields', [[6, shield]], shield_share, level, level, 0))
        for category, items, chance, low, high, skill in selections:
            result.append(dict(id=f'24b00000-0000-4000-8000-{start_index + len(result):012d}',
                               description=f'Approved Star: {entry["name"]} / {category}',
                               level=1, chance=chance / len(classes), type=3,
                               minimum=low, maximum=high, effect=1, money=0,
                               second=None, skill=skill, jewelry=False, items=items,
                               luck=0 if category == 'shields' else None,
                               suppressAdd=category == 'shields'))
    return result
