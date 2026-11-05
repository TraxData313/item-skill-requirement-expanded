# Gear Mastery

A Mount & Blade II: Bannerlord mod (1.4.8, War Sails compatible): soft skill requirements on melee weapons, shields
and armour. Equip anything you like, but if your skill falls short of the item's requirement, you feel it: weaker,
slower swings, a shield that breaks sooner, heavier feet in armour. The requirement shows in the inventory, red when
you are short of it.

Work in progress: the module loads and does nothing yet.

## Build

```
dotnet build GearMastery.sln -c Release
dotnet test tests\GearMastery.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Set your game path in `Directory.Build.props.user` if it differs from `Directory.Build.props`.

## License

Public domain (The Unlicense), see `LICENSE`.
