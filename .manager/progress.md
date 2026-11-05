# Manager progress: Gear Mastery

## Task
New separate Bannerlord mod `gear-mastery` (folder `Documents\BannerlordMods\gear-mastery`, module `GearMastery`, deployed as `Modules\GearMastery.Dev`).
Soft skill requirements on melee weapons, shields and armour: you CAN equip, the requirement shows in red, and you get scaled penalties.

### Design (agreed with Toni, 2026-11-05)
- Requirement R = round-to-5(slope × item.Tierf), clamp ≥ 0. Slope 12 for weapons/shields (matches vanilla bows: D ≈ 12×tier, corr 0.93), slope 10 for armour (else heavy body armour asks 110).
- Skill: melee = weapon's RelevantSkill (OneHanded / TwoHanded / Polearm / Throwing). Shields = One Handed (Toni confirmed). Armour = Athletics.
- Stored in the mod's own table, NOT ItemObject.Difficulty (that would trigger vanilla's hard lock in CharacterHelper.CanUseItem). Bows, crossbows, horses keep vanilla hard lock.
- shortfall = clamp((R − skill)/R, 0, 1).
  - Weapons: damage −up to 40%, swing speed −up to 20%, handling −up to 30%.
  - Shields: shield takes up to +50% damage.
  - Armour: foot max speed −up to 15%, weighted by weight share of under-skilled pieces. (Later: stamina drain synergy with trax.)
  - All maxima are settings.
- Inventory tooltip: vanilla ItemMenuVM.AddSkillRequirement draws "Requires <skill> <n>" red/green; add the same for our items + a penalty line.
- Applies to all agents (setting: heroes only).
- Must stack with trax_combat_enhancements: trax registers DECORATOR models (subclass of the abstract AgentStatCalculateModel / AgentApplyDamageModel, wrapping the previously registered model as BaseModel; see trax `src/TraxCombat.Module/SubModule.cs` RegisterModels, `Models/TraxDamageModel.cs`). Do the same.

### References
- Vanilla decompile: `%TEMP%\trax_decomp\` (DefaultItemValueModel, SandboxAgentStatCalculateModel, SandboxAgentApplyDamageModel, ItemMenuVM, CharacterHelper).
- Conventions/template: `Documents\BannerlordMods\my-troops-gear` (recent, has CLAUDE.md, Directory.Build.props, tests, tools), `keep-out-of-battle`.
- Analysis script: scratchpad items.py (bows/horses fit).

## Steps
1. [ ] Scaffold repo (copy conventions from my-troops-gear): git init, sln/csproj, SubModule.xml, build + deploy to Modules\GearMastery.Dev, CLAUDE.md, README stub. Empty mod builds & deploys.
2. [ ] Requirements core: compute (skill, R) for every item at game start; verify shield RelevantSkill; unit tests for the maths (shortfall, penalties).
3. [ ] Combat penalties: decorator models (weapon damage, swing speed/handling, shield damage, armour speed); settings.
4. [ ] Inventory tooltip: red/green requirement + penalty line.
5. [ ] Review pass (independent), docs, final build/deploy, commit + push (private GitHub repo).

## Log
