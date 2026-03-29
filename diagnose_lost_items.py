#!/usr/bin/env python3
"""
Diagnostic script to trace where AMP items are lost in the filtering pipeline.

Pipeline:
1. Parse TreasureTable.txt -> collect unique StatIds from REL_All_* tables
2. Look up each StatId in merged resolver (vanilla + AMP stats) -> skip if not found
3. Check type == "Armor" or "Weapon" -> skip others
4. ResolveItem() -> DetectPool() -> skip if pool is null
"""

import os
from collections import defaultdict

# --- Paths ---

AMP_BASE = "/mnt/e/SteamLibrary/steamapps/common/Baldurs Gate 3/Data/Public/REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025"
TT_PATH = os.path.join(AMP_BASE, "Stats", "Generated", "TreasureTable.txt")
AMP_STATS_DIR = os.path.join(AMP_BASE, "Stats", "Generated", "Data")

VANILLA_FILES = [
    "/mnt/f/Github/Para_Tool/ParaTool.Core/Resources/Vanilla/Armor.txt",
    "/mnt/f/Github/Para_Tool/ParaTool.Core/Resources/Vanilla/Armor_2.txt",
    "/mnt/f/Github/Para_Tool/ParaTool.Core/Resources/Vanilla/Weapon.txt",
]

# --- Stats Parser (matches C# StatsParser exactly) ---

def parse_stats(text):
    entries = []
    current_name = None
    current_type = None
    current_using = None
    current_data = {}

    for raw_line in text.split('\n'):
        line = raw_line.strip()
        if not line:
            continue

        if line.startswith('new entry '):
            if current_name and current_type:
                entries.append({
                    'name': current_name,
                    'type': current_type,
                    'using': current_using,
                    'data': current_data,
                })
            current_name = extract_quoted(line)
            current_type = None
            current_using = None
            current_data = {}
        elif line.startswith('type '):
            current_type = extract_quoted(line)
        elif line.startswith('using '):
            current_using = extract_quoted(line)
        elif line.startswith('data '):
            key, value = extract_data_pair(line)
            if key is not None:
                current_data[key] = value or ""

    if current_name and current_type:
        entries.append({
            'name': current_name,
            'type': current_type,
            'using': current_using,
            'data': current_data,
        })

    return entries


def extract_quoted(line):
    idx = line.find('"')
    if idx < 0:
        return ""
    rest = line[idx+1:]
    idx2 = rest.find('"')
    if idx2 < 0:
        return rest
    return rest[:idx2]


def extract_data_pair(line):
    idx = line.find('"')
    if idx < 0:
        return None, None
    after = line[idx+1:]
    end_key = after.find('"')
    if end_key < 0:
        return None, None
    key = after[:end_key]

    after_key = after[end_key+1:]
    start_val = after_key.find('"')
    if start_val < 0:
        return key, ""
    after_val_start = after_key[start_val+1:]
    end_val = after_val_start.find('"')
    if end_val < 0:
        return key, after_val_start
    value = after_val_start[:end_val]
    return key, value


# --- TreasureTable Parser (matches C# ParseTreasureTableInfo) ---

def parse_treasure_table(text):
    whitelist = set()
    in_rel = False
    is_all = False

    for raw_line in text.split('\n'):
        line = raw_line.lstrip()

        if line.startswith('new treasuretable "'):
            in_rel = False
            is_all = False
            name = extract_quoted(line)
            if not name.upper().startswith("REL_"):
                continue
            in_rel = True
            rest = name[4:]
            if rest.upper().startswith("ALL_"):
                is_all = True
            continue

        if not in_rel:
            continue

        if not line.startswith('object category "I_'):
            continue

        start = line.index('"I_') + 3
        end = line.index('"', start)
        if end <= start:
            continue
        stat_id = line[start:end]

        if is_all:
            whitelist.add(stat_id)

    return whitelist


# --- Resolver (matches C# StatsResolver) ---

class StatsResolver:
    MAX_DEPTH = 20

    def __init__(self):
        self.entries = {}

    def add(self, entry):
        self.entries[entry['name'].lower()] = entry

    def get(self, name):
        return self.entries.get(name.lower())

    def resolve(self, entry_name, prop):
        depth = 0
        current = entry_name
        while current and depth < self.MAX_DEPTH:
            entry = self.entries.get(current.lower())
            if not entry:
                return None
            for k, v in entry['data'].items():
                if k.lower() == prop.lower():
                    return v
            current = entry.get('using')
            depth += 1
        return None

    def resolve_chain(self, entry_name, prop):
        depth = 0
        current = entry_name
        chain = []
        while current and depth < self.MAX_DEPTH:
            entry = self.entries.get(current.lower())
            if not entry:
                chain.append(f"{current} [NOT FOUND]")
                return None, chain
            chain.append(current)
            for k, v in entry['data'].items():
                if k.lower() == prop.lower():
                    return v, chain
            current = entry.get('using')
            depth += 1
        return None, chain


# --- DetectPool (matches C# DetectPool exactly) ---

def detect_pool(stat_type, slot, armor_type, shield, weapon_props):
    if shield and shield.lower() == "yes":
        return "Shields"

    if stat_type == "Weapon":
        if slot == "Melee Main Weapon":
            return "Weapons_1H" if is_one_handed(weapon_props) else "Weapons"
        elif slot == "Melee Offhand Weapon":
            return "Weapons_1H"
        elif slot == "Ranged Main Weapon":
            return "Weapons_2H"
        elif slot == "Ranged Offhand Weapon":
            return "Weapons_1H"
        else:
            return "Weapons"
    
    # Armor
    if slot == "Breast":
        return "Clothes" if is_cloth_armor(armor_type) else "Armor"
    elif slot == "Helmet":
        return "Hats"
    elif slot == "Cloak":
        return "Cloaks"
    elif slot == "Gloves":
        return "Gloves"
    elif slot == "Boots":
        return "Boots"
    elif slot == "Amulet":
        return "Amulets"
    elif slot == "Ring":
        return "Rings"
    elif slot in ("MusicalInstrument", "Underwear"):
        return None
    elif slot and slot.startswith("Vanity"):
        return None
    else:
        return None


def is_one_handed(weapon_props):
    if not weapon_props:
        return False
    return "Two-Handed" not in weapon_props


def is_cloth_armor(armor_type):
    if not armor_type:
        return True
    return armor_type.lower() in ("none", "cloth")


# === MAIN ===

def main():
    # Step 1: Parse TreasureTable
    print("=" * 80)
    print("STEP 1: Parsing TreasureTable.txt for REL_All_* whitelist")
    print("=" * 80)

    with open(TT_PATH, 'r', encoding='utf-8') as f:
        tt_text = f.read()

    whitelist = parse_treasure_table(tt_text)
    print(f"  Unique StatIds in REL_All_* tables: {len(whitelist)}")

    # Step 2: Parse vanilla stats
    print()
    print("=" * 80)
    print("STEP 2: Parsing vanilla stat files")
    print("=" * 80)

    vanilla_entries = []
    for vf in VANILLA_FILES:
        with open(vf, 'r', encoding='utf-8') as f:
            text = f.read()
        parsed = parse_stats(text)
        print(f"  {os.path.basename(vf)}: {len(parsed)} entries")
        vanilla_entries.extend(parsed)

    vanilla_lookup = {}
    for e in vanilla_entries:
        vanilla_lookup[e['name'].lower()] = e

    print(f"  Total vanilla entries (unique): {len(vanilla_lookup)}")

    # Step 3: Parse AMP stats
    print()
    print("=" * 80)
    print("STEP 3: Parsing AMP stat files")
    print("=" * 80)

    amp_entries = []
    amp_files = sorted(os.listdir(AMP_STATS_DIR))
    for fname in amp_files:
        if not fname.endswith('.txt'):
            continue
        fpath = os.path.join(AMP_STATS_DIR, fname)
        with open(fpath, 'r', encoding='utf-8') as f:
            text = f.read()
        parsed = parse_stats(text)
        print(f"  {fname}: {len(parsed)} entries")
        amp_entries.extend(parsed)

    amp_lookup = {}
    for e in amp_entries:
        amp_lookup[e['name'].lower()] = e

    print(f"  Total AMP entries (unique by name): {len(amp_lookup)}")

    # Step 4: Build merged resolver (vanilla + AMP, same as C#)
    print()
    print("=" * 80)
    print("STEP 4: Building merged resolver")
    print("=" * 80)

    resolver = StatsResolver()

    # Add all vanilla first
    for e in vanilla_entries:
        resolver.add(e)

    # Now add AMP entries with merge logic (matching C# ScanAmpPak)
    merged_count = 0
    new_count = 0
    for mod_entry in amp_entries:
        vanilla = vanilla_lookup.get(mod_entry['name'].lower())
        if vanilla:
            merged_data_proper = dict(vanilla['data'])
            for k, v in mod_entry['data'].items():
                found_key = None
                for mk in merged_data_proper:
                    if mk.lower() == k.lower():
                        found_key = mk
                        break
                if found_key:
                    merged_data_proper[found_key] = v
                else:
                    merged_data_proper[k] = v

            merged_using = mod_entry.get('using')
            if merged_using and merged_using.lower() == mod_entry['name'].lower():
                merged_using = vanilla.get('using')

            merged_entry = {
                'name': mod_entry['name'],
                'type': mod_entry['type'],
                'using': merged_using if merged_using else vanilla.get('using'),
                'data': merged_data_proper,
            }
            resolver.add(merged_entry)
            merged_count += 1
        else:
            resolver.add(mod_entry)
            new_count += 1

    print(f"  Vanilla entries loaded: {len(vanilla_lookup)}")
    print(f"  AMP entries merged with vanilla: {merged_count}")
    print(f"  AMP entries new (no vanilla match): {new_count}")
    print(f"  Total resolver entries: {len(resolver.entries)}")

    # Step 5: Run the pipeline
    print()
    print("=" * 80)
    print("STEP 5: Running filtering pipeline on whitelist")
    print("=" * 80)

    not_found = []
    wrong_type = defaultdict(list)
    pool_null = []
    passed = []

    for stat_id in sorted(whitelist):
        entry = resolver.get(stat_id)

        # Filter 1: not found
        if not entry:
            not_found.append(stat_id)
            continue

        # Filter 2: type check
        if entry['type'] not in ('Armor', 'Weapon'):
            wrong_type[entry['type']].append(stat_id)
            continue

        # Filter 3: resolve and detect pool
        slot, slot_chain = resolver.resolve_chain(stat_id, "Slot")
        armor_type = resolver.resolve(stat_id, "ArmorType")
        shield = resolver.resolve(stat_id, "Shield")
        weapon_props = resolver.resolve(stat_id, "Weapon Properties")

        pool = detect_pool(entry['type'], slot, armor_type, shield, weapon_props)

        if pool is None:
            pool_null.append({
                'stat_id': stat_id,
                'type': entry['type'],
                'slot': slot,
                'armor_type': armor_type,
                'shield': shield,
                'chain': slot_chain,
            })
            continue

        passed.append(stat_id)

    # Report
    total = len(whitelist)
    print()
    print("=" * 80)
    print("RESULTS")
    print("=" * 80)
    print()
    print(f"  Total StatIds in REL_All_*:           {total}")
    print(f"  -------------------------------------------")
    print(f"  Lost: Not found in stats:             {len(not_found)}")
    for t, ids in sorted(wrong_type.items()):
        print(f"  Lost: Type = {t:20s}        {len(ids)}")
    total_wrong_type = sum(len(v) for v in wrong_type.values())
    print(f"  Lost: Wrong type total:               {total_wrong_type}")
    print(f"  Lost: DetectPool returned null:        {len(pool_null)}")
    print(f"  -------------------------------------------")
    total_lost = len(not_found) + total_wrong_type + len(pool_null)
    print(f"  Total lost:                           {total_lost}")
    print(f"  Passed all filters:                   {len(passed)}")
    print(f"  Verification: {len(passed)} + {total_lost} = {len(passed) + total_lost} (expected {total})")

    # Details: Not found
    if not_found:
        print()
        print(f"== NOT FOUND IN ANY STATS ({len(not_found)}) ==")
        for sid in not_found[:80]:
            in_amp = sid.lower() in amp_lookup
            in_van = sid.lower() in vanilla_lookup
            print(f"  {sid}  (amp={in_amp}, vanilla={in_van})")
        if len(not_found) > 80:
            print(f"  ... and {len(not_found) - 80} more")

    # Details: Wrong type
    if wrong_type:
        print()
        print(f"== WRONG TYPE (not Armor/Weapon) ==")
        for t, ids in sorted(wrong_type.items()):
            print(f"\n  Type = \"{t}\" ({len(ids)} items):")
            for sid in ids[:30]:
                print(f"    {sid}")
            if len(ids) > 30:
                print(f"    ... and {len(ids) - 30} more")

    # Details: Pool null
    if pool_null:
        print()
        print(f"== DETECT_POOL RETURNED NULL ({len(pool_null)}) ==")

        by_slot = defaultdict(list)
        for item in pool_null:
            by_slot[str(item['slot'])].append(item)

        for slot_val, items in sorted(by_slot.items(), key=lambda x: -len(x[1])):
            print(f"\n  Slot = \"{slot_val}\" ({len(items)} items):")
            for item in items[:15]:
                chain_str = " -> ".join(item['chain'])
                print(f"    {item['stat_id']} (type={item['type']}, armor_type={item['armor_type']}) chain: {chain_str}")
            if len(items) > 15:
                print(f"    ... and {len(items) - 15} more")

    # Summary
    print()
    print("=" * 80)
    print("SUMMARY")
    print("=" * 80)
    print(f"  Expected in app:  2154")
    print(f"  Computed passed:  {len(passed)}")
    if len(passed) == 2154:
        print(f"  MATCH! The {total - len(passed)} lost items are fully accounted for.")
    else:
        print(f"  MISMATCH: diff = {len(passed) - 2154}")


if __name__ == '__main__':
    main()
