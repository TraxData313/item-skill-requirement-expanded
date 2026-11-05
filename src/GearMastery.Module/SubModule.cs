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
        }
    }
}
