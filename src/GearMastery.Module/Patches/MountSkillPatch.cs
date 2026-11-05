using System;
using GearMastery.Core;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GearMastery.Patches
{
    /// <summary>
    /// Lifts vanilla's Riding lock for the riders the who-filter covers, on ANY horse (a riderless one included, whose
    /// MountDifficulty stays vanilla so troops stay locked).
    ///
    /// Prefix on <c>Agent.CheckSkillForMounting(Agent mountAgent)</c> (managed, 1.4.8): vanilla answers
    /// CanRide &amp;&amp; effective Riding &gt;= mount's MountDifficulty. Callers: Agent.Mount (the player's and AI's
    /// mount order), Agent.GetInfoTextForBeingNotInteractable ("riding skill not adequate"), and Sandbox /
    /// CustomBattle CanAgentRideMount (AI remount). When the rider is covered and can ride at all, the answer is yes;
    /// everyone else runs vanilla.
    /// </summary>
    internal static class MountSkillPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Agent), nameof(Agent.CheckSkillForMounting), new[] { typeof(Agent) })
                ?? throw new MissingMethodException("Agent.CheckSkillForMounting(Agent) not found");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(MountSkillPatch), nameof(Prefix)));
        }

        private static bool Prefix(Agent __instance, ref bool __result)
        {
            try
            {
                if (__instance == null || !AgentGear.Pays(__instance)) return true;
                if ((__instance.GetAgentFlags() & AgentFlag.CanRide) == AgentFlag.None) return true;
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("MountSkillPatch", e);
                return true;
            }
        }
    }
}
