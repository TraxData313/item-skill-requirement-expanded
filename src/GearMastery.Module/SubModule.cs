using System;
using System.Linq;
using GearMastery.Core;
using GearMastery.Models;
using GearMastery.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace GearMastery
{
    public class SubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "trax.bannerlord.gearmastery";

        private static bool _patched;
        private static bool _missionPatched;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("load", ModInfo.DisplayName + " loaded (" + typeof(SubModule).Assembly.GetName().Version + ")");
        }

        // MCM's own module registers its services in OnSubModuleLoad; the main menu is the first safe point for a page.
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            McmSettings.TryRegister("main menu");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            McmSettings.TryRegister("game start"); // no-op once registered (normally at the main menu)
            ItemRequirements.Reset();
            AgentGear.Reset();
            try
            {
                ModLog.Info("load", "game start: " + (game?.GameType?.GetType().Name ?? "unknown game type"));
                RegisterModels(gameStarterObject);
                ApplyMissionPatches();
                if (game?.GameType is Campaign) ApplyInventoryPatches();
            }
            catch (Exception e)
            {
                ModLog.Error("load", "OnGameStart", e);
            }
        }

        /// <summary>
        /// The inventory side (campaign only, once per process): each patch in its own try/catch, so a game update that
        /// renames one target only loses that part. Patched at the first campaign start, never at module load.
        /// </summary>
        private static void ApplyInventoryPatches()
        {
            if (_patched) return;
            _patched = true;
            Patch("CharacterHelper.CanUseItem postfix (bows / crossbows / slings / horses equippable)", h => CanUseItemPatch.Apply(h));
            Patch("ItemMenuVM.RefreshItemTooltips prefix (usage tab -> per-usage requirement skill)", h => ItemTooltipPatch.ApplyUsageIndex(h));
            Patch("ItemMenuVM.AddSkillRequirement prefix (requirement line coloured by skill < R)", h => ItemTooltipPatch.ApplyRequirementLine(h));
            Patch("ItemMenuVM.SetGeneralComponentTooltip postfix (requirement + shield / armour penalty lines)", h => ItemTooltipPatch.ApplyGeneralTooltip(h));
            Patch("ItemMenuVM.RefreshItemTooltips postfix (stat rows after the penalty, compared by after values)", h => ItemTooltipPatch.ApplyStatRows(h));
        }

        /// <summary>The mission side (every game type, once per process): the Riding lock lifted for covered heroes.</summary>
        private static void ApplyMissionPatches()
        {
            if (_missionPatched) return;
            _missionPatched = true;
            Patch("Agent.CheckSkillForMounting prefix (covered heroes may mount any horse; troops keep vanilla)", h => MountSkillPatch.Apply(h));
        }

        private static void Patch(string what, Action<Harmony> apply)
        {
            try
            {
                apply(new Harmony(HarmonyId));
                ModLog.Info("patches", "applied: " + what);
            }
            catch (Exception e)
            {
                ModLog.Error("patches", "NOT applied (carrying on without it): " + what, e);
            }
        }

        /// <summary>
        /// The decorators (trax_combat_enhancements' RegisterModels pattern): each only when the game type already has
        /// that model, so BaseModel is never null. AddModel hands us the previously registered model as BaseModel;
        /// MissionGameModels then picks the last registered.
        /// </summary>
        private static void RegisterModels(IGameStarter starter)
        {
            var existing = starter.Models.ToList();

            try
            {
                var statModels = existing.OfType<AgentStatCalculateModel>().ToList();
                if (statModels.Count > 0)
                {
                    var stats = new GearMasteryStatModel(statModels);
                    starter.AddModel<AgentStatCalculateModel>(stats);
                    ModLog.Info("models", "agent stat decorator registered over " + stats.BaseModelName
                        + " - weapon damage / swing / handling / reload / inaccuracy, armour foot speed, horse speed / maneuver, mount lock lifted (covered heroes only); tournament AI-level fix "
                        + (GearMasteryStatModel.AiLevelFixAvailable ? "active over " + statModels.Count + " base model(s)" : "UNAVAILABLE (field not found)"));
                }
                else
                {
                    ModLog.Info("models", "this game type has no AgentStatCalculateModel - stat decorator not registered");
                }
            }
            catch (Exception e)
            {
                ModLog.Error("models", "stat decorator registration", e);
            }

            try
            {
                if (existing.Any(m => m is AgentApplyDamageModel))
                {
                    var damage = new GearMasteryDamageModel();
                    starter.AddModel<AgentApplyDamageModel>(damage);
                    ModLog.Info("models", "damage decorator registered over " + damage.BaseModelName + " - shield damage taken");
                }
                else
                {
                    ModLog.Info("models", "this game type has no AgentApplyDamageModel - damage decorator not registered");
                }
            }
            catch (Exception e)
            {
                ModLog.Error("models", "damage decorator registration", e);
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                if (mission == null || GameNetwork.IsMultiplayer) return;
                mission.AddMissionBehavior(new MountRefreshLogic());
            }
            catch (Exception e)
            {
                ModLog.Error("mission", "OnMissionBehaviorInitialize", e);
            }
        }

        // Items are registered and the BasicModels (ItemValueModel -> Tierf) are in place by now, campaign and
        // custom battle alike.
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            try
            {
                if (IsDevInstall || ItemRequirements.Settings.DumpRequirementsCsv)
                    ItemRequirements.WriteDump();
            }
            catch (Exception e)
            {
                ModLog.Error("init", "requirement dump", e);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            ItemRequirements.Reset();
            AgentGear.Reset();
        }

        /// <summary>True when loaded from Modules\GearMastery.Dev (tools\deploy.ps1).</summary>
        private static bool IsDevInstall
        {
            get
            {
                try
                {
                    var path = typeof(SubModule).Assembly.Location ?? "";
                    return path.IndexOf(ModInfo.ModuleId + ".Dev", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { return false; }
            }
        }
    }
}
