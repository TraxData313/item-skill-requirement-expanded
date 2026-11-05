using System;
using TaleWorlds.MountAndBlade;

namespace GearMastery
{
    /// <summary>
    /// A horse's stats (MountSpeed / MountManeuver, and the rider-based riding bonuses) are computed on the HORSE agent,
    /// but Agent.OnMount only refreshes the RIDER's stats. At spawn the rider is set before the horse is built, so that
    /// is fine; a horse mounted mid-battle (a remount, a loose horse) would keep its riderless values. This behaviour
    /// refreshes the horse when someone mounts it, so our horse penalty (and vanilla's own Riding bonus) follows the
    /// actual rider.
    /// </summary>
    internal sealed class MountRefreshLogic : MissionLogic
    {
        public override void OnAgentMount(Agent agent)
        {
            try
            {
                var mount = agent?.MountAgent;
                if (mount != null && mount.IsActive()) mount.UpdateAgentStats();
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("mission.mount-refresh", e);
            }
        }
    }
}
