using System;
using GearMastery.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GearMastery
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("load", ModInfo.DisplayName + " loaded (" + typeof(SubModule).Assembly.GetName().Version + ")");
        }

        // Decorator models (Models\) get registered here in step 3, the way trax_combat_enhancements does it:
        // starter.AddModel<AgentStatCalculateModel>(new GearMasteryStatModel(...)) - the game hands the previously
        // registered model in as BaseModel and every override delegates to it, so trax's decorators and ours stack.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            ItemRequirements.Reset();
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
