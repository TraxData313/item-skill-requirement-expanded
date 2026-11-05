# Manager progress: Gear Mastery

## Task
New separate Bannerlord mod `gear-mastery` (folder `Documents\BannerlordMods\gear-mastery`, module `GearMastery`, deployed as `Modules\GearMastery.Dev`).
Soft skill requirements on melee weapons, shields and armour: you CAN equip, the requirement shows in red, and you get scaled penalties.

### Design (agreed with Toni, 2026-11-05)
- Requirement R = round-to-5(slope × item.Tierf), clamp ≥ 0. Slope 12 for weapons/shields (matches vanilla bows: D ≈ 12×tier, corr 0.93), slope 10 for armour (else heavy body armour asks 110).
- Skill: melee = weapon's RelevantSkill (OneHanded / TwoHanded / Polearm / Throwing). Shields = One Handed (Toni confirmed). Armour = Athletics.
- Stored in the mod's own table, NOT ItemObject.Difficulty (that would trigger vanilla's hard lock in CharacterHelper.CanUseItem).
- **Change 2026-11-05 (Toni): bows, crossbows, slings and HORSES become soft too** — never blocked, only debuffed. Their R = vanilla `item.Difficulty`, skill = `item.RelevantSkill` (Bow / Crossbow / Throwing for slings / Riding). Ammo stays uncovered. Vanilla blocks these in `CharacterHelper.CanUseItem` (inventory) and in missions via mount difficulty (`CanAgentRideMount`, `MissionFocusableObjectInformationProvider`); AI equipment logic may skip them too — steps 3/4 must lift those blocks.
- shortfall = clamp((R − skill)/R, 0, 1); penalty = max × shortfall^p, **p = 0.5** (setting `PenaltyExponent`; concave so a moderate gap bites).
- **Penalties are MILD (Toni 2026-11-05)**: an expensive sword in unskilled hands ≈ a standard sword — enough to want to earn it, not enough to stop a rich trader carrying it for status. Defaults:
  - Weapons: damage −15%, swing speed −10%, handling −15%.
  - Shields: shield takes up to +25% damage.
  - Armour: foot max speed −8%, weighted by weight share of under-skilled pieces (curve applied to the weighted aggregate). (Later: stamina drain synergy with trax.)
  - Bows/crossbows/slings: damage −15%, reload/draw speed −10%, inaccuracy +30%.
  - Horses: speed −10%, maneuver −10%.
  - All maxima are settings (`GearMasterySettings`).
- Calibration (unit-tested): wielder skill 30, mid sword R30 vs top sword R90 at +25% raw → top sword 1.007× the mid one (linear p=1 would give 1.05×); at skill 90 the full 1.25×.
- Inventory tooltip: vanilla ItemMenuVM.AddSkillRequirement draws "Requires <skill> <n>" red/green; add the same for our items + a penalty line.
- Applies to all agents (setting: heroes only).
- Must stack with trax_combat_enhancements: trax registers DECORATOR models (subclass of the abstract AgentStatCalculateModel / AgentApplyDamageModel, wrapping the previously registered model as BaseModel; see trax `src/TraxCombat.Module/SubModule.cs` RegisterModels, `Models/TraxDamageModel.cs`). Do the same.

### References
- Vanilla decompile: `%TEMP%\trax_decomp\` (DefaultItemValueModel, SandboxAgentStatCalculateModel, SandboxAgentApplyDamageModel, ItemMenuVM, CharacterHelper).
- Conventions/template: `Documents\BannerlordMods\my-troops-gear` (recent, has CLAUDE.md, Directory.Build.props, tests, tools), `keep-out-of-battle`.
- Analysis script: scratchpad items.py (bows/horses fit).

## Steps
1. [x] Scaffold repo (copy conventions from my-troops-gear): git init, sln/csproj, SubModule.xml, build + deploy to Modules\GearMastery.Dev, CLAUDE.md, README stub. Empty mod builds & deploys.
2. [x] Requirements core: compute (skill, R) for every item at game start; verify shield RelevantSkill; unit tests for the maths (shortfall, penalties).
3. [ ] Combat penalties: decorator models (weapon damage, swing speed/handling, shield damage, armour speed); settings.
4. [ ] Inventory tooltip: red/green requirement + penalty line.
5. [ ] Review pass (independent), docs, final build/deploy, commit + push (private GitHub repo).

## Log
- 2026-11-05 step 1: scaffold (commit e1b8e2c).
- 2026-11-05 step 2: requirements core. Build 0 errors/0 warnings, 64 tests pass, deployed to Modules\GearMastery.Dev (game not launched).
  - Core (no game refs): `GearMasterySettings.cs` (slopes, caps 100/100, all penalty maxima incl. ranged/horse, `PenaltyExponent` 0.5, `HeroesOnly`, `DumpRequirementsCsv`; `SkillKind` Weapon/Shield/Armour/Ranged/Mount), `Requirements.cs` (`Requirement(kind, tierf, settings)`, `FromDifficulty`, `Shortfall`), `Penalties.cs` (`Curve`, Weapon/Swing/Handling/ShieldDamageTaken/RangedDamage/ReloadSpeed/Inaccuracy/HorseSpeed/HorseManeuver multipliers, `ArmourSpeedMultiplier(IEnumerable<ArmourPiece>)`, `ArmourShortfall`; `Calibration`). Tests: `RequirementsTests.cs`, `PenaltiesTests.cs`.
  - Module: `ItemRequirements.cs` — lazy cache keyed by ItemObject reference (crafted weapons picked up on first query), reset in `OnGameStart`/`OnGameEnd`. `SubModule.OnGameInitializationFinished` writes `Configs\GearMastery\item_requirements.csv` (id,type,kind,skill,tierf,difficulty,weight,R) when loaded from `GearMastery.Dev` or `DumpRequirementsCsv`.
  - Decompile findings: `ItemObject.Tierf` is NOT stored — every read calls `Game.Current.BasicModels.ItemValueModel.CalculateTier(item)` unless the XML has `tier_override` (then that value). Needs a running Game, hence lazy; crafted weapons use 0.6×melee tier + 0.4×crafting tier. Vanilla shield RelevantSkill: `GetRelevantSkillFromWeaponClass(SmallShield/LargeShield)` = **OneHanded** (matches Toni's choice).
  - Classification by `ItemType`: Head/Body/Leg/HandArmor, Cape → Athletics; Shield → OneHanded; OneHanded/TwoHanded/Polearm/Thrown → PrimaryWeapon.RelevantSkill (only 1H/2H/Polearm/Throwing); Bow/Crossbow/Sling → RelevantSkill, R = Difficulty; Horse → Riding, R = Difficulty. Everything else (ammo, banners, goods, pack animals, harness, firearms) → null.
  - Offline XML sanity (SandBoxCore, slope 10/12): body armour median R20, top real ones R90 (khuzait_heavy_armor tier 9.1); helmets median 45, max 75; legs ≤40; gloves ≤45; capes ≤45; shields median 35, max 75 (steel_druzhinnik_kite_shield). Dummy test armours hit the 100 cap.
  - **API for step 3**: `ItemRequirements.Get(item)` → `ItemRequirement {Skill, Value (R), Kind, Tierf, HasRequirement}` or null; `TryGet(item, out skill, out R)`; `Shortfall(character, item)`; `GetSkill(BasicCharacterObject|Agent, skill)` (raw — inside models prefer the model's `GetEffectiveSkill`); `ItemRequirements.Settings`; then `Penalties.*Multiplier(shortfall, settings)` and `Penalties.ArmourSpeedMultiplier(pieces, settings)`. Honour `Settings.HeroesOnly`. Never throw inside models (`ModLog.ErrorOnce`). Settings still defaults only (no file yet). Step 3 also must lift vanilla's hard locks for bows/crossbows/horses (see Design).
