# Gear Mastery

Bannerlord 1.4.8 (+ War Sails) mod: **soft skill requirements** on melee weapons, shields and armour. You CAN equip
anything; if your skill is below the item's requirement, the requirement shows in red and you fight with scaled
penalties. Bows, crossbows and horses keep vanilla's hard lock (untouched).

Status and the agreed design: `.manager\progress.md` (the resume file, committed). Step 1 (scaffold) is done; the
requirement maths, combat penalties and the tooltip come next.

## Design (agreed with Toni, 2026-11-05)

- Requirement `R = round-to-5(slope x item.Tierf)`, clamped >= 0. Slope 12 for weapons and shields (vanilla bows sit
  at D ~ 12 x tier), slope 10 for armour (else heavy body armour would ask 110).
- Skill: melee weapon = its `RelevantSkill` (One Handed / Two Handed / Polearm / Throwing); shields = One Handed;
  armour = Athletics.
- `shortfall = clamp((R - skill) / R, 0, 1)`. Penalties scale linearly with it, maxima are settings:
  - weapons: damage -up to 40%, swing speed -up to 20%, handling -up to 30%;
  - shields: the shield takes up to +50% damage;
  - armour: foot max speed -up to 15%, weighted by the weight share of the under-skilled pieces.
- Applies to every agent (setting: heroes only).
- Inventory tooltip: vanilla `ItemMenuVM.AddSkillRequirement` draws "Requires <skill> <n>" red/green; we add the same
  line for our items plus a penalty line.

## Layout

- `src\GearMastery.Core` - netstandard2.0, **no game references**: requirement table maths, shortfall, penalty
  scaling, settings. Unit-tested.
- `src\GearMastery.Module` - net472 module (assembly `GearMastery.dll`, namespace `GearMastery`): `SubModule`,
  `ModLog`; `Models\` will hold the decorator models.
- `tests\GearMastery.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml. Build output lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder` (and later `HarmonyBinFolder`).

## Commands

```
dotnet build GearMastery.sln -c Release
dotnet test tests\GearMastery.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\GearMastery.Dev, "Gear Mastery (dev)"
```

Release identity in `module\SubModule.xml` is Id `GearMastery`; only deploy.ps1 re-badges it `.Dev`. Never install
the non-dev module into the game's Modules (it would load beside `GearMastery.Dev`).

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\GearMastery\gear_mastery.log`.

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
