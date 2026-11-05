# Gear Mastery

Bannerlord 1.4.8 (+ War Sails) mod: **soft skill requirements** on every weapon, shield, armour piece, bow /
crossbow / sling and horse. Covered heroes CAN equip and ride anything; below an item's requirement it shows red and
they fight with penalties (snap curve). Troops always fight as in vanilla, locks included.

Status and the agreed design: `.manager\progress.md` (the resume file, committed). Round 1 (steps 1-5) and round 2
(steps 6-10: tier table, snap curve + who-filter, MCM, tooltip v2, final review) are done; pushed to `origin/main`.
Not yet playtested in game as a whole (round 2 not deployed while Toni plays). Player docs: `README.md`.

## Design (agreed with Toni, 2026-11-05)

- Requirement (round 2, step 6): `R = round-to-5(table(item.Tierf) x category scale)` for EVERY category (melee,
  shields, armour, bows/crossbows/slings, horses - vanilla Difficulty no longer used). Table points
  {0:0, 1:20, 2:45, 3:75, 4:100, 5:125, 6:150, 7.5:200}, flat outside; scales default 1.0. Vanilla's displayed
  "Tier N" = round(Tierf) clamped 0..6, so "Tier 6" = Tierf >= 5.5.
- Skill: weapons per USAGE (`ItemRequirement.SkillFor(usage)`: bastard sword 1H/2H, javelin Throwing/held skill);
  shields and armour = Athletics; bows Bow, crossbows Crossbow, slings Throwing, horses Riding.
- Penalty curve (round 2, step 7) with a SNAP: skill >= R -> 0. Below, by r = skill / R (= 1 - shortfall),
  piecewise-linear through anchors r->1: Low, r 0.6: Mid, r 0.3: High, held at High below (`Penalties.Amount`;
  ratios `PenaltyMidRatio` / `PenaltyHighRatio`). One Low/Mid/High triple per stat in `GearMasterySettings`:
  melee damage 25/50/75%, swing + handling 12.5/25/37.5%; shield damage taken +25/50/75%; armour foot speed
  20/40/60% per piece by its own r, then weighted by the piece's share of total worn weight; ranged damage 25/50/75%,
  reload/draw 12.5/25/37.5%, inaccuracy +25/50/75%; horse speed and maneuver 25/50/75%. Goal (Toni): an item a tier
  or two above your skill comes out WORSE than the one you meet (unit-tested for horses and swords).
- Requirement table: `ItemRequirements` (module), lazy per game. Dev install writes
  `Configs\GearMastery\item_requirements.csv` at game start.
- Who pays (`AgentGear.Pays`): never troops (non-hero agents keep vanilla, mount lock included). Toggles
  `ApplyToPlayer`, `ApplyToCompanions` (player clan heroes), `ApplyToOtherHeroes`, `ApplyInTournaments` (mission has
  SandBox's `TournamentBehavior`). Horse penalty and mount unlock follow the RIDER: MountDifficulty is zeroed only
  under a covered rider, and a prefix on `Agent.CheckSkillForMounting` lets covered heroes mount any horse. The
  inventory honours the same toggles (`AgentGear.CharacterPays`): the CanUseItem unlock and our tooltip lines only for
  covered heroes; anyone else gets vanilla's lock and vanilla's tooltip.
- Misordered MCM values are clamped, never inverted: anchors run as a running maximum (Mid >= Low, High >= Mid), the
  tier table never falls with tier (`Requirements.FromTable`); a High ratio not below the Mid ratio falls back to
  0.6 / 0.3. R = 0 asks nothing (shortfall 0).
- Inventory tooltip (round 2, step 9, `Patches\ItemTooltipPatch`): "Requires: <Skill> <R> (yours <n>)" red/green per
  usage tab; when short (and the who-toggles cover the character) vanilla's stat rows show the after-penalty value
  with the breakdown ("Swing Damage: 37 (99-62) Cut"), from Core `TooltipStats` (same functions as the models);
  comparison colours compare after values. A compact "Unskilled:" line only for shield damage taken / armour foot speed.
- Thrown usages (javelin thrown; kind Weapon + usage IsRangedWeapon) take the RANGED anchors in battle and tooltip:
  damage + inaccuracy, no swing / ready / handling (`CombatPenalties.IsThrownUsage`).
- Starter cap (Toni, 2026-11-05): every item whose displayed tier <= `StarterGearMaxTier` (default 1), any category,
  has each penalty capped at `StarterGearMaxPenalty` (default 20%) - `CombatPenalties.StarterCap`, passed as
  `req.DisplayedTier` by models and tooltip; armour capped per piece before the weight share (`ArmourPiece.MaxPenalty`).

## Layout

- `src\GearMastery.Core` - netstandard2.0, **no game references**: requirement table maths, shortfall, penalty
  scaling, settings, `SettingsMenu` (every setting's MCM group / label / hint / range and its mapping onto
  `GearMasterySettings` - the MCM adapter, game-free). Unit-tested.
- `src\GearMastery.Module` - net472 module (assembly `GearMastery.dll`, namespace `GearMastery`): `SubModule`,
  `ModLog`, `McmSettings` (the MCM page, fluent builder), `ItemRequirements` (the table + THE live settings object),
  `AgentGear` (item of a blow, shortfall from the model's effective skill - deliberately NOT cached per agent:
  captains / Phalanx / level-ups move it mid-battle), `MountRefreshLogic` (refreshes a horse when mounted);
  `Models\` the decorator models; `Patches\` the Harmony patches (`MountSkillPatch` once at the first game start of any type; inventory `CanUseItemPatch`, `ItemTooltipPatch` once at the first campaign start). Depends on `Bannerlord.Harmony` (not shipped).
- `tests\GearMastery.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml. Build output lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder` and `HarmonyBinFolder` (falls back to
  `Modules\Bannerlord.Harmony` when that has no 0Harmony.dll) and `McmBinFolder` (falls back to
  `Modules\Bannerlord.MBOptionScreen`).

## Commands

```
dotnet build GearMastery.sln -c Release
dotnet test tests\GearMastery.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\GearMastery.Dev, "Gear Mastery (dev)"
```

Release identity in `module\SubModule.xml` is Id `GearMastery`; only deploy.ps1 re-badges it `.Dev`. Never install
the non-dev module into the game's Modules (it would load beside `GearMastery.Dev`).

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\GearMastery\gear_mastery.log`. Settings live in MCM (Mod
Options > Gear Mastery; MCM saves them under `Configs\ModSettings\Global\GearMastery`); every change is logged as
`[settings] X: old -> new (MCM)`. Without MCM: defaults + one `[mcm]` line. A new setting = a property in
`GearMasterySettings` + a line in `SettingsMenu.All` (group, label, hint, range; `Req(...)` when it changes requirements).

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
- Battle and tooltip must agree: every tooltip number comes from the same Core functions the models call
  (`CombatPenalties.ForWielded / DamageMultiplier / InaccuracyMultiplier`, `Penalties.*`), at the same per-usage
  skill. Known gap, by design: the tooltip reads RAW skill (GetSkillValue), battle the model's effective skill.
- `ItemRequirements.Reset()` after any requirement setting change (MCM does it; a generation counter keeps a racing
  lookup from writing a stale R back).
- **MCM is optional**: MCM types only inside method bodies of `McmSettings` (fluent builder, `Action<object>` instance
  methods - never a type deriving from / a field of / a lambda over an MCM type), else the game's `GetTypes()` refuses
  the whole mod without MCM. Check: `dotnet run --project ..\trax_combat_enhancements\tools\AssemblyGuard -c Release --
  module\bin\Win64_Shipping_Client\GearMastery.dll MCMv5`.
