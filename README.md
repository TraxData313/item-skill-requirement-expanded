# Gear Mastery

A Mount & Blade II: Bannerlord mod (1.4.8, War Sails compatible) that makes skill requirements **soft**. You can
equip anything. If your skill is below what an item asks for, the requirement shows in red in the inventory and
you fight with the item a bit worse: weaker, slower swings, a shield that breaks sooner, heavier feet in armour,
a horse that answers the reins less well. Nothing is ever locked.

The penalties are mild on purpose. A rich trader can still carry a noble sword for show; it just won't beat a
good standard sword until he has trained for it.

## What asks for what

| Item | Skill | Requirement R |
|---|---|---|
| One-handed, two-handed, polearm, throwing weapons | the weapon's own skill | about **12 x tier** |
| Shields | One Handed | about **12 x tier** |
| Armour (head, body, legs, gloves, cape) | Athletics | about **10 x tier** |
| Bows, crossbows, slings | Bow / Crossbow / Throwing | the **vanilla difficulty** |
| Horses | Riding | the **vanilla difficulty** |

The tier formula rounds to the nearest 5 and is capped at 100. Most body armour asks around 20 Athletics, the best
around 90; helmets about 45; shields up to 75. Arrows, bolts, banners, pack animals and goods ask nothing.

Bows, crossbows, slings and horses keep their vanilla numbers, but they no longer lock you out: you can equip them
in the inventory and mount any horse in battle, and pay the penalty instead.

## How much it costs

`shortfall = (R - your skill) / R`, from 0 (requirement met) to 1 (skill 0). The penalty is
`maximum x shortfall^0.5`, so a moderate gap already costs most of the maximum:

| Item | Penalty at full shortfall (default) |
|---|---|
| Melee / throwing weapon | -15% damage, -10% swing and thrust speed, -15% handling |
| Shield | the shield takes +25% damage |
| Armour | -8% foot speed if ALL your armour is that far out of reach; it is shared by weight, so one light helmet barely matters |
| Bow / crossbow / sling | -15% missile damage, -10% reload and draw speed, +30% inaccuracy |
| Horse | -10% speed, -10% maneuver |

Example: with One Handed 30, a top sword (R 90, 25% more raw damage) loses about 12% damage and 8% swing speed, so
it comes out about **1.01x** a standard sword you fully master (R 30). At skill 90 you get the whole 1.25x.

The inventory tooltip shows "Requires: <skill> <R>" (green when met, red when short) and, when short, the exact
"Unskilled: ..." penalties. The tooltip uses your plain skill; in battle the game's effective skill counts (captain
perks and the like), as vanilla does.

## Settings

`Documents\Mount and Blade II Bannerlord\Configs\GearMastery\settings.json` is created with the defaults the first
time you start a game. Every key has a comment above it. Edit it, then load a save or start a custom battle: it is
read at every game start, no restart needed. A wrong or out-of-range value falls back to its default and is noted
in `gear_mastery.log` in the same folder. Delete the file to get a fresh one.

| Key | Default | Meaning |
|---|---|---|
| `WeaponSlope`, `ShieldSlope`, `ArmourSlope` | 12, 12, 10 | requirement per item tier |
| `WeaponCap`, `ArmourCap` | 100, 100 | highest requirement |
| `PenaltyExponent` | 0.5 | curve shape (1 = linear) |
| `MaxWeaponDamagePenalty`, `MaxSwingSpeedPenalty`, `MaxHandlingPenalty` | 0.15, 0.10, 0.15 | melee / throwing |
| `MaxShieldDamageTakenBonus` | 0.25 | shields |
| `MaxArmourSpeedPenalty` | 0.08 | armour |
| `MaxRangedDamagePenalty`, `MaxReloadSpeedPenalty`, `MaxInaccuracyBonus` | 0.15, 0.10, 0.30 | bows, crossbows, slings |
| `MaxHorseSpeedPenalty`, `MaxHorseManeuverPenalty` | 0.10, 0.10 | horses |
| `HeroesOnly` | false | true = only heroes pay; troops fight as in vanilla |
| `DumpRequirementsCsv` | false | write `item_requirements.csv` with every item's requirement |

Set a penalty to 0 to switch it off.

## Compatibility

- **trax_combat_enhancements** and other mods that change the combat models: Gear Mastery wraps whatever agent-stat
  and damage models were registered before it and only multiplies on top, so both mods' effects stack. Load order
  does not matter for that.
- **War Sails**: its naval models are wrapped the same way.
- Needs **Harmony** (Bannerlord.Harmony) for the inventory part: equipping bows and horses above your skill and the
  tooltip lines. If a game update breaks one of those patches, that part is skipped and logged; battles still work.
- Nothing is saved into your campaign. You can add or remove the mod mid-campaign.

## Known limits

- An arrow's damage penalty follows the launcher in your hands when it lands. Switch to a sword before your arrow
  hits and that arrow pays nothing.
- A throwing weapon used in melee (a javelin as a spear) is judged by its Throwing requirement.
- Horses: anyone who can ride at all can now mount any horse, troops included, also with `HeroesOnly` (then
  without the penalty).
- Items another mod gives a vanilla difficulty on melee weapons, shields or armour stay hard-locked as in vanilla.
- The tooltip text is English only for now.

## Build

```
dotnet build GearMastery.sln -c Release
dotnet test tests\GearMastery.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Set your game path in `Directory.Build.props.user` if it differs from `Directory.Build.props`.

## License

Public domain (The Unlicense), see `LICENSE`.
