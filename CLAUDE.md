# Gear Mastery

Bannerlord 1.4.8 (+ War Sails) mod: **soft skill requirements** on melee weapons, shields and armour. You CAN equip
anything; if your skill is below the item's requirement, the requirement shows in red and you fight with scaled
penalties. Bows, crossbows and horses become soft too (R = vanilla difficulty).

Status and the agreed design: `.manager\progress.md` (the resume file, committed). All 5 steps are done (scaffold,
requirements core, combat penalties, inventory unlock + tooltip, review + settings file + docs). Not yet playtested
in game; no remote yet. Player docs: `README.md`.

## Design (agreed with Toni, 2026-11-05)

- Requirement `R = round-to-5(slope x item.Tierf)`, clamped >= 0. Slope 12 for weapons and shields (vanilla bows sit
  at D ~ 12 x tier), slope 10 for armour (else heavy body armour would ask 110).
- Skill: melee weapon = its `RelevantSkill` (One Handed / Two Handed / Polearm / Throwing); shields = One Handed;
  armour = Athletics.
- Bows, crossbows, slings and horses are soft too (Toni, 2026-11-05): R = vanilla `item.Difficulty`, skill =
  `item.RelevantSkill` (Bow / Crossbow / Throwing / Riding). Vanilla's hard locks on them get lifted in steps 3/4.
- `shortfall = clamp((R - skill) / R, 0, 1)`; penalty = max x shortfall^0.5 (`PenaltyExponent`). Penalties are MILD
  on purpose (an expensive sword unskilled ~ a standard sword); maxima are settings in `GearMasterySettings`:
  - weapons: damage -15%, swing speed -10%, handling -15%;
  - shields: the shield takes up to +25% damage;
  - armour: foot max speed -8%, weighted by the weight share of the under-skilled pieces;
  - bows/crossbows: damage -15%, reload -10%, inaccuracy +30%; horses: speed and maneuver -10%.
  All tunable in settings.json without recompiling.
- Requirement table: `ItemRequirements` (module), lazy per game. Dev install writes
  `Configs\GearMastery\item_requirements.csv` at game start.
- Applies to every agent (setting: heroes only). The mount unlock (horse `MountDifficulty` zeroed, CanAgentRideMount)
  applies to everyone regardless.
- Inventory tooltip: vanilla `ItemMenuVM.AddSkillRequirement` draws "Requires <skill> <n>" red/green; we add the same
  line for our items plus a penalty line.

## Layout

- `src\GearMastery.Core` - netstandard2.0, **no game references**: requirement table maths, shortfall, penalty
  scaling, settings, `SettingsFile` (settings.json text: commented writer, dependency-free reader). Unit-tested.
- `src\GearMastery.Module` - net472 module (assembly `GearMastery.dll`, namespace `GearMastery`): `SubModule`,
  `ModLog`, `SettingsLoader` (reads settings.json at every game start), `ItemRequirements` (the table),
  `AgentGear` (item of a blow, shortfall from the model's effective skill - deliberately NOT cached per agent:
  captains / Phalanx / level-ups move it mid-battle), `MountRefreshLogic` (refreshes a horse when mounted);
  `Models\` the decorator models; `Patches\` the Harmony patches (inventory only: `CanUseItemPatch`, `ItemTooltipPatch`), applied once at the first campaign start. Depends on `Bannerlord.Harmony` (not shipped).
- `tests\GearMastery.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml. Build output lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder` and `HarmonyBinFolder` (falls back to
  `Modules\Bannerlord.Harmony` when that has no 0Harmony.dll).

## Commands

```
dotnet build GearMastery.sln -c Release
dotnet test tests\GearMastery.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\GearMastery.Dev, "Gear Mastery (dev)"
```

Release identity in `module\SubModule.xml` is Id `GearMastery`; only deploy.ps1 re-badges it `.Dev`. Never install
the non-dev module into the game's Modules (it would load beside `GearMastery.Dev`).

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\GearMastery\gear_mastery.log`; settings (created with
defaults when missing, bad values -> default + `[settings]` log line): `...\Configs\GearMastery\settings.json`.
A new setting = a property in `GearMasterySettings` + a line in `SettingsFile.All` (key, words, range).

## Design rules

- **Decorator models only.** Register with `starter.AddModel<AgentStatCalculateModel>(...)` /
  `AddModel<AgentApplyDamageModel>(...)`: subclass the ABSTRACT game model, take the previously registered model as
  `BaseModel` and delegate every member to it, then adjust. **Never subclass `Default*` / `Sandbox*` models** - that
  would drop whatever another mod (trax_combat_enhancements, War Sails, CustomBattle) registered before us. Pattern:
  `trax_combat_enhancements\src\TraxCombat.Module\SubModule.cs` (`RegisterModels`) and `Models\TraxDamageModel.cs`.
- **The requirement is NOT stored in `ItemObject.Difficulty`.** Difficulty > 0 triggers vanilla's hard lock
  (`CharacterHelper.CanUseItem`). Keep our own table (item id -> skill, R), built at game start.
- Code that runs inside game methods (stat refresh, every hit) must never throw: try/catch + `ModLog.ErrorOnce`,
  fall back to the base model's value.
- Patch nothing at module load: any Harmony patch goes in `OnGameStart`, each in its own try/catch with a log line
  (see my-troops-gear's CLAUDE.md for the 2026-11-03 crash this avoids).
- No saved types: the mod must stay removable mid-campaign.
