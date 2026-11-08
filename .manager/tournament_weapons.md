# Tournament starting weapons: inventory and facts (research, 2026-11-08)

Read-only research for the "fit tournament weapons to the participant's skill" idea. No mod code changed.

Sources and how they were read:
- Vanilla code: Toni's partial decompile `%TEMP%\trax_decomp\full_SandBox`, `full_TaleWorlds.CampaignSystem`,
  `TaleWorlds.Core`, `TaleWorlds.MountAndBlade`. The key class was re-decompiled from the INSTALLED v1.4.8
  `Modules\SandBox\bin\Win64_Shipping_Client\SandBox.dll` with `ilspycmd 9.1` (global dotnet tool) and matches
  (`bin\Win64_Shipping_Client\Version.xml` = v1.4.8). `DefaultTournamentModel.GetParticipantArmor` was checked the same way.
- XML: `Modules\SandBoxCore\ModuleData\spcultures.xml` (culture -> template lists), `spnpccharacters.xml` (templates),
  `Modules\NavalDLC\ModuleData\naval_characters.xml` + `XSLT\NavalDLC_SandBoxCore_SPCultures.xslt` (Nord lists),
  items under `SandBoxCore\ModuleData\items\*.xml`, `NavalDLC\ModuleData\naval_weapons.xml`, crafting pieces /
  `weapon_descriptions.xml` / `crafting_templates.xml` (+ NavalDLC XSLTs) for crafted-item usages.
- Tierf / tier / R / skills: the mod's own dump
  `Documents\Mount and Blade II Bannerlord\Configs\ItemSkillRequirementExpanded\item_requirements.csv`
  (written 2026-11-08 10:57 by the dev install, current load order; log says "values now all defaults").
  Spot-checked against the Core formula (e.g. Tierf 3.314 -> 75 + 25 x 0.314 = 82.85 -> R 85). All R below = defaults.
- Scripts: scratchpad `tourn.py` (inventory), `usages.py` (crafted usages), `pool.py` / `low.py` (replacement pool),
  `pen.py` (penalty maths, a copy of `Penalties.Amount` + `CombatPenalties.StarterCap`).

---

## 1. How vanilla picks tournament loadouts

Campaign tournaments are ONLY the melee "fight" type: `DefaultTournamentModel.CreateTournament` returns
`FightTournamentGame` (MaxTeamSize 4, MaxTeamNumberPerMatch 4, 16 participants). `TournamentJoustingMissionController`
and `TournamentArcheryMissionController` exist in SandBox but nothing in the campaign opens them. NavalDLC's
`NavalDLCTournamentModel` delegates everything relevant to the base model.

Flow (SandBox `SandBox.Tournaments.MissionLogics.TournamentFightMissionController`):
1. `StartMatch(match, isLastRound)` (public) -> `PrepareForMatch()` (public) -> spawns.
   `SkipMatch(match)` (public; matches simulated without spawning) also calls `PrepareForMatch()` then `Simulate()`
   (simulation reads `participant.MatchEquipment` via `GetSimulationAttackPower`).
2. `PrepareForMatch()`:
   ```
   teamWeaponEquipmentList = GetTeamWeaponEquipmentList(_match.Teams.First().Participants.Count());
   foreach team: num = 0; foreach participant in team.Participants:
       participant.MatchEquipment = teamWeaponEquipmentList[num].Clone();
       AddRandomClothes(_culture, participant); num++;
   ```
3. `GetTeamWeaponEquipmentList(teamSize)` (private): culture = `PlayerEncounter.EncounterSettlement.Culture` (the TOWN's
   culture, not the owner's). List = `culture.TournamentTeamTemplatesFor{One|Two|Four}Participant` by team size;
   ONE template character is picked at random (`MBRandom.RandomInt(list.Count)`) for the whole match; fallback when
   the list is empty: `tournament_template_empire_{one|two|four}_participant_set_v1`. Each of its `BattleEquipments`
   (one EquipmentRoster per team slot) is copied (`FillFrom`) into the list.
4. So: one random template per MATCH (new pick every match, i.e. every round); team member #i of EVERY team gets
   roster #i of that template (teams are mirror images, positional). Team size 1, 2 or 4 (random per round,
   `TournamentBehavior.CreateTournamentTree`; final is always 1v1). Position in team = order of
   `TournamentMatch.AddParticipant` (no sorting by anything).
5. `AddRandomClothes` copies slots 5..9 (Head, Body, Leg, Gloves, Cape) from
   `TournamentModel.GetParticipantArmor(character)`: in a tournament that is `participant.RandomBattleEquipment` (the
   hero's own battle armour); `gear_practice_dummy_<culture>` only when `CampaignMission.Current.Mode != Tournament`
   (arena practice). Weapons (slots 0..3), Horse (10) and HorseHarness (11) come only from the template.
   (Not verified: whether the very first `PrepareForMatch`, which runs before `SetMissionMode(Tournament)`, sees the
   practice-dummy branch. Armour is out of scope here anyway.)
6. `SpawnAgentWithRandomItems(participant, team, frame)` (private): `new AgentBuildData(new SimpleAgentOrigin(character,...))
   .Equipment(participant.MatchEquipment)...`; `Mission.SpawnAgent`; `WieldInitialWeapons`. The agent's gear is
   exactly `participant.MatchEquipment`; the character's own weapons are never used.
7. Mounted vs foot: no separate rounds. A roster with a Horse slot spawns that member mounted; mixed foot/horse teams
   are normal (e.g. Vlandia four v1: sword+shield, billhook, crossbow, lancer).

**Is anyone's skill considered? No.** Not the player's, not NPC heroes', not troops'. The only random inputs are
the template pick and the team size.

Who participates (`FightTournamentGame.GetParticipantCharacters`): lords/ladies/wanderers in town, player-party
heroes, then troops (culture basic / elite basic troops, garrison troops) to fill 16. Troops never pay penalties
in the mod (`AgentGear.Pays`), so only hero participants are affected by any of this.

Arena PRACTICE (`SandBox.Missions.MissionLogics.Arena.ArenaPracticeFightMissionController`) is separate: NPC
weapons from `weapon_practice_stage_{1..3}_<culture>` (stage by spawn index), the player spawns in civilian gear and
takes weapons from the racks; there is no `TournamentBehavior`, so `ApplyInTournaments` does not cover it (it counts
as a normal mission: `ApplyToPlayer`). Not inventoried here.

### Installed mods
No other installed mod touches tournament loadouts: `tournament_template` / `tournament_team_templates` appear only in
SandBoxCore and NavalDLC ModuleData (all 40+ Modules folders and the 2 Workshop folders grepped); no non-vanilla
module DLL contains `TournamentFightMissionController`, `MatchEquipment`, `GetParticipantArmor` or
`GetTeamWeaponEquipmentList`. (TraxCombatEnhancements only does a tournament AI-level fix, per our own log.)

### Template lists per culture (count of templates by team size 1 / 2 / 4)

| Culture | 1 | 2 | 4 | Source |
|---|---|---|---|---|
| empire | 1 | 3 | 3 | spcultures.xml |
| aserai | 1 | 3 | 4 | spcultures.xml |
| sturgia | 2 | 3 | 3 | spcultures.xml |
| vlandia | 3 | 4 | 4 | spcultures.xml |
| battania | 2 | 5 | 3 | spcultures.xml |
| khuzait | 3 | 3 | 3 | spcultures.xml |
| nord | 3 | 3 | 4 | NavalDLC XSLT + naval_characters.xml |

63 templates, 159 rosters. All template characters exist (none missing).

---

## 2. Every tournament starting item

R = mod requirement (defaults). Skill = the skill per usage (`ItemRequirement.SkillFor`), CSV "skills" column;
"own-culture raw" = raw skill that meets R with the same-culture bonus (ceil((R-5)/1.2)).
"Uses" = template-roster slots it appears in (out of 159 rosters x slots). Blunt = sparring item.

### One-handed (skill OneHanded)

| id | name | class | Tierf | Tier | R | culture | sets (culture/team size) | uses |
|---|---|---|---|---|---|---|---|---|
| vlandia_sword_1_t2_blunt | Sparring Arming Sword | OneHandedSword | 3.846 | 4 | 95 | vlandia | vlandia 1/2/4 | 24 |
| empire_sword_1_t2_blunt | Sparing Spatha | OneHandedSword | 3.834 | 4 | 95 | empire | empire 1/2/4 | 19 |
| khuzait_sword_1_t2_blunt | Sparring Saber | OneHandedSword | 3.698 | 4 | 90 | khuzait | khuzait 1/2/4 | 17 |
| sturgia_axe_2_t2_blunt | Sparring Galloglaich Axe | OneHandedAxe | 3.488 | 3 | 85 | sturgia | sturgia 1/2/4 | 14 |
| nord_1haxe_blunt | Nord Etched Axe | OneHandedAxe | 3.224 | 3 | 80 | nord | nord 1/2/4 | 10 |
| aserai_sword_2_t2_blunt | Sparring Scimitar | OneHandedSword | 3.567 | 4 | 90 | aserai | aserai 2/4 | 8 |
| aserai_axe_2_t2_blunt | Sparring Bamboo Handled Axe | OneHandedAxe | 3.314 | 3 | 85 | aserai | aserai 2/4 | 8 |
| sturgia_sword_1_t2_blunt | Sparring Blade | OneHandedSword | 3.132 | 3 | 80 | sturgia | sturgia 2/4 | 6 |
| aserai_sword_1_t2_blunt | Sparring Flyssa | OneHandedSword | 3.847 | 4 | 95 | aserai | aserai 1/2/4 | 5 |
| battania_sword_1_t2_blunt | Sparring Broadsword | OneHandedSword | 4.461 | 4 | 110 | battania | battania 2/4 | 5 |
| nord_1hsword_blunt | Embroidered Onehanded Sword | OneHandedSword | 3.878 | 4 | 95 | nord | nord 2/4 | 4 |
| aserai_sword_1_t2 | Iron Flyssa (SHARP) | OneHandedSword | 2.612 | 3 | 65 | aserai | aserai 4 (v3) | 1 |

### Two-handed (skill TwoHanded)

| id | name | class | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|---|
| battania_2hsword_1_t2_blunt | Sparring Twohander | TwoHandedSword | 5.195 | 5 | 130 | battania | battania 1/2/4 | 13 |
| battania_2haxe_1_t2_blunt | Sparring Two Handed Axe | TwoHandedAxe | 4.424 | 4 | 110 | battania | battania 1/2/4 | 6 |
| nord_2haxe_blunt | Nord Hollow Two Handed Axe | TwoHandedAxe | 4.751 | 5 | 120 | nord | nord 1/2/4 | 4 |

### Polearms (skill Polearm on every usage); usages derived from pieces (see section 3)

| id | name | usages | length | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|---|---|
| eastern_spear_1_t2_blunt | Sparring Jagged Spear | 1H polearm, 2H polearm | ~221 | 1.906 | 2 | 45 | aserai | aserai 2/4 (mounted), khuzait 2/4 | 18 |
| vlandia_lance_1_t3_blunt | Sparring Lance | 1H, 2H, **Couchable, Bracing** | ~296 | 2.417 | 2 | 60 | vlandia | vlandia 1/2/4 (all mounted rosters) | 7 |
| nord_atgeir_polearm_blunt | Sparring Atgeir | 2H polearm only | ~251 | 5.358 | 5 | 135 | nord | nord 1/2/4 (four v4 = all 4 members) | 7 |
| empire_lance_1_t3_blunt | Sparring Lance | 1H, 2H, Couchable | ~274 | 1.839 | 2 | 40 | empire | empire 4 (mounted) | 5 |
| nord_polearm_blunt | Decorated Sparring Spear | 2H polearm (thrust only) | ~229 | 3.062 | 3 | 75 | nord | nord 2/4 | 4 |
| billhook_polearm_t2_blunt | Sparring Billhook | 2H polearm (swing only) | ~207 | 4.978 | 5 | 125 | vlandia | vlandia 1/2/4 | 3 |
| northern_spear_1_t2_blunt | Sparring Spear | 1H, 2H | ~273 | 0.905 | 1 | 20 | sturgia | sturgia 2/4 | 3 |
| khuzait_polearm_1_t4_blunt | Sparring Glaive | 1H, 2H | ~218 | 5.613 | 6 | 140 | khuzait | khuzait 1 (v2, mounted) | 1 |
| empire_menavlion_1_t3_blunt | Sparring Menavlion | 1H, 2H | ~?* | 5.794 | 6 | 145 | empire | empire 4 (v2, member 2, carried with a sword) | 1 |

Length = handle length x scale + blade length (rough, from piece XML; *menavlion handle 15 + banner piece, not summed).

### Throwing (thrown usage Throwing; held usage in brackets)

| id | name | usages | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|---|
| eastern_javelin_1_t2_blunt | Sparring Javelin | Javelin [Polearm] | 2.3 | 2 | 55 | khuzait | aserai 1/2/4, khuzait 1/2/4 | 14 |
| western_javelin_1_t2_blunt | Sparring Blunt Javelin | Javelin [Polearm] | 0.991 | 1 | 20 | - | empire 2/4 | 6 |
| nord_throwing_axe_blunt | Nord Sparring Throwing Axe | ThrowingAxe [OneHanded] | 3.872 | 4 | 95 | nord | nord 2/4 | 3 |
| northern_throwing_axe_1_t1_blunt | Sparring Throwing Axe | ThrowingAxe [OneHanded] | 3.365 | 3 | 85 | sturgia | sturgia 1/2 | 2 |
| nord_javelin_blunt | God's Ray | Javelin [Polearm] | 2.586 | 3 | 65 | nord | nord 2/4 | 2 |

### Bows (Bow) and crossbows (Crossbow)

| id | name | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|
| mountain_hunting_bow | Mountain Hunting Bow | 0.79 | 1 | 15 | battania | battania 2/4, nord 2/4, sturgia 2/4 | 15 |
| steppe_bow | Steppe Bow | 0.71 | 1 | 15 | khuzait | aserai 2/4, khuzait 1/2/4 | 13 |
| glen_ranger_bow | Longbow | 1.65 | 2 | 35 | battania | empire 2/4 | 3 |
| crossbow_a_blunt | Sparring Crossbow | 3.54 | 4 | 90 | vlandia | vlandia 2/4 | 9 |

Ammo (no requirement in the mod): tournament_arrows (30), tournament_bolts (9), default_arrows (1: khuzait four v1
member 3 gets SHARP default arrows; vanilla quirk).

### Shields (skill Athletics in this mod)

| id | name | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|
| bound_desert_round_sparring_shield | Desert Round Sparring Shield | 0.031 | 0 | 0 | aserai | aserai 1/2/4 | 14 |
| western_riders_kite_sparring_shield | Horseman Sparring Kite Shield | 0.994 | 1 | 20 | - | sturgia 2/4 | 10 |
| northern_round_sparring_shield | Sparring Round Shield | 1.681 | 2 | 35 | - | nord 1/2/4 | 10 |
| small_heater_shield | Small Heater Shield | 1.932 | 2 | 45 | vlandia | vlandia 1/2/4, sturgia 1 | 8 |
| horsemans_heater_shield | Small Cavalry Heater Shield | 1.847 | 2 | 40 | vlandia | vlandia 1/2/4 | 8 |
| western_kite_sparring_shield | Kite Sparring Shield | 1.42 | 1 | 30 | - | empire 1/2/4 | 7 |
| battania_targe_b_sparring | Sparring Targe | 2.42 | 2 | 60 | battania | battania 2/4 | 5 |
| eastern_wicker_sparring_shield | Wicker Square Tournament Shield | 1.576 | 2 | 35 | khuzait | khuzait 2/4 | 3 |
| bound_horsemans_kite_shield | Cavalry Kite Shield | 2.574 | 3 | 60 | - | sturgia 4 (mounted) | 3 |
| bound_desert_round_shield | Desert Round Shield | 2.161 | 2 | 50 | aserai | aserai 4 (v3) | 1 |

### Horses (skill Riding); harness `light_harness` everywhere (no requirement)

| id | name | Tierf | Tier | R | culture | sets | uses |
|---|---|---|---|---|---|---|---|
| khuzait_horse_tournament | Steppe Horse | 0 | 0 | 0 | khuzait | khuzait 1/2/4 | 15 |
| aserai_horse_tournament | Desert Horse | 0.16 | 0 | 5 | aserai | aserai 2/4 | 10 |
| vlandia_horse_tournament | Saddle Horse | 1.35 | 1 | 30 | vlandia | vlandia 1/2/4 | 7 |
| empire_horse_tournament | Midlands Palfrey | 0 | 0 | 0 | empire | empire 4 | 5 |
| sturgia_horse_tournament | Sturgian Trotter | 0 | 0 | 0 | sturgia | sturgia 2/4 | 4 |

Battania and Nord templates have no horses.

### Counts per class (distinct items)
1H 12, 2H 3, polearm 9, throwing 5, bow 3, crossbow 1, shield 10, horse 5 (48 with a requirement) + 3 ammo + 1 harness.

### Worst offenders (R, displayed tier)
empire_menavlion 145 (T6), khuzait glaive 140 (T6), nord_atgeir 135 (T5), battania 2H sword 130 (T5),
billhook 125 (T5), nord 2H axe 120 (T5), battania sparring broadsword 110 (T4), battania 2H axe 110 (T4),
then a wall of 1H sparring swords at 90-95 (T4), crossbow_a_blunt 90, nord throwing axe 95.

**Systemic finding:** the SPARRING versions rate HIGHER than their sharp originals (Iron Broadsword
battania_2hsword_1_t2 Tierf 4.23 vs its blunt copy 5.195; Iron Flyssa 2.612 vs Sparring Flyssa 3.847;
crossbow_a 2.9 vs crossbow_a_blunt 3.54). Vanilla's tier calculation evidently values blunt damage highly, so nearly
every tournament melee weapon lands at Tier 3-6 although they are training weapons. Only bows, a few shields, the
horses, northern_spear_1_t2_blunt and western_javelin_1_t2_blunt are Tier 0-1. With defaults, a hero needs roughly
80-95 OneHanded for ANY tournament 1H weapon, 110-130 TwoHanded for any 2H, and 125-145 Polearm for the Nord/Vlandian
long polearms. This is the mod's maths on vanilla Tierf, not a bug in the dump (spot-checked).

---

## 3. The pike Toni got

No tournament template contains a true pike (crafting template `Pike`, usage `TwoHandedPolearm_Pike`). The long
polearms are all `TwoHandedPolearm` crafts. Usages were derived the way vanilla does it
(`TaleWorlds.Core.Crafting` item build: a WeaponDescription of the template is added only when every used piece is
in its `AvailablePieces`, NavalDLC XSLT additions included); results agree with the CSV skill columns.

Only ONE tournament item can brace: **vlandia_lance_1_t3_blunt "Sparring Lance"** (spear_handle_23 250 cm + blade
46 cm, usages 1H / 2H / Couchable / Bracing; Tierf 2.417, Tier 2, R 60). It appears only in Vlandian MOUNTED rosters
(four v1 m4, v2 m4, v3 m3+m4; one v3; two v4 both). Other long "pike-like" candidates, none of which has a Bracing
usage: nord_atgeir_polearm_blunt (R 135; Nord four v4 gives it to all 4 members; Nord one v2), empire_lance (R 40,
couch only), northern_spear (R 20), nord_polearm_blunt (R 75), billhook (R 125, swing-only). Which item Toni actually
held is NOT verified (no screenshots since 2026-11-05 in the F12 folder, nothing in the mod log); if he braced, it was
the Vlandian Sparring Lance, otherwise most likely the Nord Sparring Atgeir.

Penalty with defaults (snap curve, mid 0.6 / high 0.3; damage 25/50/75%, swing/thrust/handling 12.5/25/37.5%;
starter cap only for displayed tier <= 1). Damage multiplier / swing (= thrust speed = handling) multiplier:

| item (R) | Polearm 10 | 15 | 20 | own culture at 20 (eff. 29) |
|---|---|---|---|---|
| vlandia_lance (60) | x0.25 / x0.625 | x0.25 / x0.625 | x0.28 / x0.64 | x0.40 |
| nord_atgeir (135) | x0.25 / x0.625 | same | same | x0.25 |
| billhook (125), glaive (140), menavlion (145) | x0.25 / x0.625 | same | same | x0.25 |
| nord_polearm (75) | x0.25 / x0.625 | x0.25 | x0.25 | x0.32 |
| empire_lance (40) | x0.25 | x0.31 | x0.42 | x0.58 |
| eastern_spear (45) | x0.25 | x0.28 | x0.37 | x0.53 |
| northern_spear (20, Tier 1 -> cap 20%) | x0.80 | x0.80 | x1.00 | x1.00 |

So a Polearm 10-20 hero hits for about a quarter of normal damage and thrusts ~37% slower with any of the long
tournament polearms.

Vanilla makes bracing worse on top (TaleWorlds.MountAndBlade `MissionCombatMechanicsHelper.ComputeBlowMagnitudeMelee`):
- A passive attack (brace / couch) does **0 damage when neither attacker nor victim is mounted** (`baseMagnitude = 0f`
  unless attacker has a mount, victim has a mount, or victim is a mount). Bracing against a foot opponent in the
  arena never does damage, at any skill.
- Against horses, braced magnitude comes from relative speed (`CalculateBaseBlowMagnitudeForPassiveUsage`) and then
  `CalculatePassiveAttackDamage` (Sandbox model: Polearm.Braced perk bonuses); after that the mod's
  `GetWeaponDamageMultiplier` (x0.25) is applied to the passive blow as to any melee blow (same function, line after
  the non-tip-thrust multiplier).
- A thrust that does not land with the tip is multiplied by `NonTipThrustHitDamageMultiplier` (managed parameter).
- Vanilla's own skill effect is tiny: PolearmDamage +0.07% per skill point (Polearm 15 = +1.05%).
- All tournament weapons are blunt (except the one Iron Flyssa and the khuzait default_arrows).
- Not verified: whether TraxCombatEnhancements' stamina lowers damage further for a tired low-Athletics hero.

---

## 4. Replacement pool per class (Tier 0-1 = Tierf < 1.5; from the CSV)

Starter cap reminder: anything at displayed tier <= 1 caps EVERY penalty at 20% (defaults), so a Tier 0-1 swap
means "at worst -20%" whatever the skill. Items marked non-merch are not sold in shops. "Blunt" = sparring/wooden.
Many T0 rows in the CSV are siege projectiles, torches, flails, shackles, horse whips: exclude those.

| class in tournaments | tournament R range | Tier 0-1 candidates (id, Tier, R) | blunt/sparring T0-1? |
|---|---|---|---|
| 1H sword | 80-110 | **wooden_sword_t1** (T1, R10, non-merch), **wooden_sword_t2** (T1, R25, non-merch), pirate_sword_1 (T1, R30, sharp) | yes (wooden swords) |
| 1H axe | 80-85 | peasant_hatchet_1_t1 (T0, R10), nord_hatchett_3_t1 (T1, R10, nord), peasant_sickle_1_t1 (T1, R15), naval_one_handed_axe_t1 (T1, R15, nord), peasant_pickaxe_1_t1 (T1, R25) | no - all sharp |
| 1H mace (alt for 1H) | - | **peasant_hammer_2_t1** Wooden Hammer (T0, R0), peasant_hammer_1_t1 (T0, R0), empire/vlandia_mace_1_t2 (T1, R25), battania/aserai_mace (T1, R30) | maces are blunt by nature |
| 2H sword | 130 | **wooden_2hsword_t1** Wooden Twohander (T1, R15; bastard: 1H/2H usages, OneHanded;TwoHanded skills) | yes |
| 2H axe | 110-120 | none at T0-1; lowest peasant_2haxe_1_t1 Hoe (T2, R40), simple_sparth_axe_t2 (T2, R55) | no |
| 2H mace (alt for 2H) | - | peasant_maul_t1_2 Maul (T1, R15), peasant_maul_t1 Makeshift Maul (T1, R20, non-merch) | blunt by nature |
| Polearm (spear) | 20-145 | **northern_spear_1_t2_blunt** Sparring Spear (T1, R20, already a tournament item), western_spear_2_t2 (T0, R5), peasant_pitchfork_1/2_t1 (T0, R0/5), northern_spear_1_t2 (T1, R10), imperial_spear_t2 (T1, R15), nord_spear_1_t2 (T1, R20), western_spear_1_t2 (T1, R30), bo_staff (T0, R0, non-merch, special) | yes (Sparring Spear) |
| Polearm couch (mounted lance) | 40-60 | empire_lance_1_t3 Light Cavalry Lance (T1, R30, couchable, sharp); no blunt couchable at T0-1 (empire_lance_1_t3_blunt is T2, R40) | no |
| Polearm brace | 60 | simple_pike_t2 Simple Pike (T1, R10, Pike + Bracing), eastern_spear_3_t3 (T1, R30, bracing); sharp | no |
| Javelin | 20-65 | **western_javelin_1_t2_blunt** Sparring Blunt Javelin (T1, R20, already a tournament item), western_javelin_1_t2 (T1, R15), northern_javelin_1_t2 (T1, R15) | yes |
| Throwing axe | 85-95 | none at T0-1 (lowest nord_throwing_axe_1_t2 T3, R65); throwing knives celtic_throwing_dagger (T0, R0), empire_throwingknife_t5 (T1, R15), desert_throwing_knife (T1, R20) | no |
| Bow | 15-35 | training_longbow Practice Longbow (T0, R10, non-merch), steppe_bow (T1, R15), hunting_bow (T1, R15), mountain_hunting_bow (T1, R15), training_bow (T1, R20, non-merch), highland_ranger_bow (T1, R20) | practice bows |
| Crossbow | 90 | **none at T0-1 in the whole game**; lowest crossbow_h Light Peasant Crossbow (T2, R35), crossbow_i (T2, R40), crossbow_a (T3, R70) | no |
| Shield | 0-60 | **old_kite_sparring_shield** (T0, R0), **bound_desert_round_sparring_shield** (T0, R0), western_riders_kite_sparring_shield (T1, R20), western_kite_sparring_shield (T1, R30), sturgia_old_shield_b (T1, R15), battered_horsemans_kite_shield (T1, R20), old_horsemans_kite_shield (T1, R25) | yes |
| Horse | 0-30 | all tournament horses except vlandia (R30) are already R0-5; khuzait/empire/sturgia/battania_horse_tournament, camel_tournament (R0) | yes (tournament horses) |

Gaps with no Tier 0-1 item of the same class: 2H axe, throwing axe, crossbow, blunt couchable lance (sharp only),
blunt 1H axe. Each needs a class-adjacent swap (2H axe -> 2H mace/wooden twohander; throwing axe -> blunt javelin /
throwing knife; crossbow -> T2 peasant crossbow (R35) or a bow + arrows), or a new mod-shipped item (e.g. a blunt
Tier-1 crossbow / throwing axe in our own ModuleData), or a different rule (see below).

Not checked: damage type of each candidate (blunt vs cut) beyond its name; wooden_sword_* / wooden_2hsword_t1 are
named wooden but their damage type was not read from the pieces.

Alternative the manager may weigh (fact, not a recommendation): because blunt tournament items are inflated by the
tier formula, an R override for tournament-only items (e.g. treat items used by `tournament_template_*` as their
sharp counterpart's tier, or apply the starter cap to them) would fix the whole table without swapping gear.

---

## 5. Hook points

Best: **Harmony postfix on `TournamentFightMissionController.PrepareForMatch()`** (public, SandBox.dll,
namespace `SandBox.Tournaments.MissionLogics`).
- At that point every participant has its OWN `MatchEquipment` (`.Clone()` per participant), so per-participant edits
  never leak into teammates, the other teams or the template character.
- It runs for both played (`StartMatch`) and simulated (`SkipMatch`) matches, so simulation strength
  (`GetSimulationAttackPower(…, MatchEquipment)`) stays consistent with what is fought.
- Access: `_match` is a private field (`AccessTools.Field`), or prefix `StartMatch(TournamentMatch match, bool)` /
  `SkipMatch(TournamentMatch match)` and keep the arg. Each `TournamentParticipant` has `.Character`
  (`CharacterObject`, `.IsHero`, `.HeroObject`) and settable `.MatchEquipment`.
- Edit via `participant.MatchEquipment[EquipmentIndex] = new EquipmentElement(item)` or
  `AddEquipmentToSlotWithoutAgent`. Slots: Item0..Item3 = weapons (0..3), Horse 10, HorseHarness 11.

Fallbacks: prefix on private `SpawnAgentWithRandomItems(TournamentParticipant, Team, MatrixFrame)` (played matches
only, misses simulation); patching `GetTeamWeaponEquipmentList` is per TEAM SLOT, not per participant (useless for
this). A `TournamentModel` decorator cannot do it: `GetParticipantArmor` only feeds slots 5..9.

Risks / details:
- Who: swap only for characters our penalties cover (`IsHero` + `AgentGear.CharacterPays`-style toggles +
  `ApplyInTournaments`). Troops never pay, so changing their gear would only alter vanilla balance.
- Teams are mirror images by position; a per-participant downgrade breaks that symmetry (intended, but note it).
- Ammo pairs: a bow keeps `tournament_arrows`; a crossbow replacement keeps `tournament_bolts`; a crossbow -> bow swap
  must also replace the bolts slot. Javelins often fill TWO slots (two stacks, e.g. aserai four v2 m3): replace both.
- Throwing axes / javelins are also the sidearm in some rosters (held usage OneHanded / Polearm): the "skill" checked
  depends on the usage, so choose by the skill of the usage the AI will actually use.
- Mounted rosters: lances are the only couch weapon; swapping to a non-couchable spear changes the role. Horse swap =
  slot 10 only; keep `light_harness`.
- `WieldInitialWeapons` picks the initial weapon from slot order; keep weapon kinds in the same slots (main weapon
  Item0, shield Item1 where vanilla had it) so the AI wields as before.
- Equipment indices are plain single-player `EquipmentIndex` (no multiplayer-style indices are involved);
  `AgentBuildData.Equipment(participant.MatchEquipment)` is the agent's whole loadout, so AI agents built from
  characters need nothing else.
- Requirement lookups (`ItemRequirements.Get`) need a running Game: fine inside a mission.
- The player's skill-fitting choice should use the same effective skill as battle (raw skill + culture bonus);
  `GetEffectiveSkill` needs an agent, which does not exist yet at `PrepareForMatch` - use raw hero skill there
  (the known tooltip-vs-battle gap applies).
- A hero's own armour (slots 5..9) is still penalised by Athletics in tournaments; not touched by any of this.
