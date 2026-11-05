using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ItemSkillRequirementExpanded
{
    /// <summary>
    /// A horse's stats (MountSpeed / MountManeuver, MountDifficulty and the rider-based riding bonuses) are computed on
    /// the HORSE agent, but Agent.OnMount / OnDismount only refresh the RIDER's stats. At spawn the rider is set before
    /// the horse is built, so that is fine; otherwise the horse would keep the previous rider's values. This behaviour
    /// refreshes the horse when someone mounts it (our penalty and vanilla's Riding bonus follow the actual rider) and
    /// when its rider dismounts (a horse a covered hero rode gets vanilla's MountDifficulty back, so a troop picking
    /// it up stays locked as in vanilla). It also drops the per-mission tournament cache at mission end.
    /// </summary>
    internal sealed class MountRefreshLogic : MissionLogic
    {
        // rider -> the horse he sits on (OnAgentDismount does not say which horse he left)
        private readonly Dictionary<Agent, Agent> _ridden = new Dictionary<Agent, Agent>();

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                var mount = agent?.MountAgent;
                if (mount != null) _ridden[agent!] = mount;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("mission.mount-track", e);
            }
        }

        public override void OnAgentMount(Agent agent)
        {
            try
            {
                var mount = agent?.MountAgent;
                if (mount == null) return;
                _ridden[agent!] = mount;
                if (mount.IsActive()) mount.UpdateAgentStats();
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("mission.mount-refresh", e);
            }
        }

        public override void OnAgentDismount(Agent agent)
        {
            try
            {
                if (agent == null || !_ridden.TryGetValue(agent, out var mount)) return;
                _ridden.Remove(agent);
                if (mount != null && mount.IsActive()) mount.UpdateAgentStats();
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("mission.dismount-refresh", e);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (affectedAgent != null) _ridden.Remove(affectedAgent);
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            _ridden.Clear();
            AgentGear.ForgetMission(Mission);
        }
    }
}
