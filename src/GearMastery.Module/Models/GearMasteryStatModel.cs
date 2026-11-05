using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GearMastery.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GearMastery.Models
{
    /// <summary>
    /// The stat DECORATOR: subclasses the abstract AgentStatCalculateModel, wraps whatever was registered before
    /// (Sandbox / CustomBattle / War Sails' naval model / trax_combat_enhancements' decorator) as BaseModel and
    /// forwards EVERY abstract and virtual member to it (pattern: trax's TraxAgentStatModel). Our additions:
    ///
    /// - <see cref="UpdateAgentStats"/>, after the base (which assigns every property fresh, so nothing compounds):
    ///   a human's wielded weapon - SwingSpeedMultiplier, ThrustOrRangedReadySpeedMultiplier, HandlingMultiplier
    ///   (melee / thrown) or ReloadSpeed, ThrustOrRangedReadySpeedMultiplier (draw), WeaponInaccuracy (bow / crossbow
    ///   / sling); on foot, MaxSpeedMultiplier by the worn armour. Only for agents the who-filter covers
    ///   (<see cref="AgentGear.Pays"/>: heroes by toggle, never troops). A HORSE agent under a covered RIDER:
    ///   MountDifficulty set to 0 (lifts vanilla's mounting lock - Agent.CheckSkillForMounting, the "not enough
    ///   riding" focus text and the AI's CanAgentRideMount all read it) and MountSpeed / MountManeuver by the rider's
    ///   Riding shortfall. Troops' horses keep vanilla values, lock included.
    /// - <see cref="GetWeaponDamageMultiplier"/>: the one damage hook for melee AND missiles
    ///   (MissionCombatMechanicsHelper.ComputeBlowMagnitudeMelee / ...Missile multiply the base magnitude by it,
    ///   before armour), so no damage-model collision decoding is needed. An ammo blow takes the launcher in hand.
    /// - <see cref="GetWeaponInaccuracy"/>: the engine's callback (Agent.GetWeaponInaccuracy) for a launcher.
    /// - <see cref="CanAgentRideMount"/>: the base's answer, or (covered agents only) yes when only skill said no.
    ///
    /// The tournament AI-level fix is mirrored from trax: TournamentBehavior sets the AI level on the TOP model only
    /// (a non-virtual write to a private field), so we push our field to every earlier stat model.
    /// </summary>
    public sealed class GearMasteryStatModel : AgentStatCalculateModel
    {
        private static readonly Func<AgentStatCalculateModel, float>? ReadAiLevel = BuildAiLevelReader();

        private readonly List<AgentStatCalculateModel> _chain;
        private float _pushedAiLevel = 1f;

        public GearMasteryStatModel(IEnumerable<AgentStatCalculateModel> earlierModels)
        {
            _chain = earlierModels.Where(m => m != null && !ReferenceEquals(m, this)).ToList();
        }

        internal string BaseModelName => BaseModel?.GetType().FullName ?? "(none)";

        internal static bool AiLevelFixAvailable => ReadAiLevel != null;

        // ------------------------------------------------------------------ the tournament fix (as trax)

        private void SyncAiLevel()
        {
            if (ReadAiLevel == null) return;
            try
            {
                float level = ReadAiLevel(this);
                if (Math.Abs(level - _pushedAiLevel) < 1e-6f) return;
                foreach (var model in _chain)
                    model.SetAILevelMultiplier(level);
                _pushedAiLevel = level;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.ai-level-sync", e);
            }
        }

        private static Func<AgentStatCalculateModel, float>? BuildAiLevelReader()
        {
            try
            {
                var field = typeof(AgentStatCalculateModel).GetField("_AILevelMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(float)) return null;
                var method = new DynamicMethod("GearMastery_ReadAILevelMultiplier", typeof(float),
                    new[] { typeof(AgentStatCalculateModel) }, typeof(AgentStatCalculateModel), true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Ret);
                return (Func<AgentStatCalculateModel, float>)method.CreateDelegate(typeof(Func<AgentStatCalculateModel, float>));
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ our hooks

        public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
        {
            SyncAiLevel();
            BaseModel.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);
            // CustomBattle's model sets MountDifficulty only here (not in UpdateAgentStats). Idempotent; no
            // multipliers here - Sandbox's Initialize already routed through our UpdateAgentStats.
            try
            {
                if (agent != null && agent.IsMount && AgentGear.Pays(agent.RiderAgent)) agentDrivenProperties.MountDifficulty = 0f;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.init-mount", e);
            }
        }

        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            SyncAiLevel();
            BaseModel.UpdateAgentStats(agent, agentDrivenProperties);
            try
            {
                if (agent == null) return;
                if (agent.IsHuman) ApplyHuman(agent, agentDrivenProperties);
                else if (agent.IsMount) ApplyMount(agent, agentDrivenProperties);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.update", e);
            }
        }

        private void ApplyHuman(Agent agent, AgentDrivenProperties p)
        {
            if (!AgentGear.Pays(agent)) return;
            var settings = ItemRequirements.Settings;

            var item = AgentGear.WieldedItem(agent, out var usage);
            if (item != null)
            {
                float s = AgentGear.Shortfall(this, agent, item, usage, out var req);
                if (req != null && s > 0f)
                {
                    var m = CombatPenalties.ForWielded(req.Kind, s, settings);
                    if (!m.IsIdentity)
                    {
                        p.SwingSpeedMultiplier *= m.Swing;
                        p.ThrustOrRangedReadySpeedMultiplier *= m.ThrustOrRangedReady;
                        p.HandlingMultiplier *= m.Handling;
                        p.ReloadSpeed *= m.Reload;
                        p.WeaponInaccuracy *= m.Inaccuracy;
                    }
                }
            }

            if (agent.MountAgent == null)
            {
                float armour = AgentGear.ArmourSpeed(this, agent);
                if (armour < 1f) p.MaxSpeedMultiplier *= armour;
            }
        }

        private void ApplyMount(Agent mount, AgentDrivenProperties p)
        {
            // Vanilla's hard lock is lifted only under a rider the who-filter covers (our tier table judges him
            // instead); a troop's or a riderless horse keeps vanilla's MountDifficulty. A covered hero mounting a
            // riderless horse passes through the Agent.CheckSkillForMounting prefix (Patches\MountSkillPatch).
            var rider = mount.RiderAgent;
            if (!AgentGear.Pays(rider)) return;
            p.MountDifficulty = 0f;
            var horse = mount.SpawnEquipment?[EquipmentIndex.Horse].Item;
            if (horse == null) return;
            float s = AgentGear.Shortfall(this, rider!, horse, null, out var req);
            if (req == null || req.Kind != SkillKind.Mount || s <= 0f) return;
            var settings = ItemRequirements.Settings;
            p.MountSpeed *= Penalties.HorseSpeedMultiplier(s, settings);
            p.MountManeuver *= Penalties.HorseManeuverMultiplier(s, settings);
        }

        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float value = BaseModel.GetWeaponDamageMultiplier(agent, weapon);
            try
            {
                if (weapon == null || !AgentGear.Pays(agent)) return value;
                bool ammo = weapon.IsAmmo;
                // Ammo: the launcher in hand and ITS usage; otherwise the blow's own usage (javelin thrown -> Throwing).
                WeaponComponentData? usage = weapon;
                ItemObject? item = ammo ? AgentGear.WieldedItem(agent, out usage) : AgentGear.ItemOf(agent, weapon);
                if (item == null) return value;
                float s = AgentGear.Shortfall(this, agent, item, usage, out var req);
                if (req == null || s <= 0f) return value;
                return value * CombatPenalties.DamageMultiplier(req.Kind, ammo, s, ItemRequirements.Settings);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.damage", e);
                return value;
            }
        }

        public override float GetWeaponInaccuracy(Agent agent, WeaponComponentData weapon, int weaponSkill)
        {
            float value = BaseModel.GetWeaponInaccuracy(agent, weapon, weaponSkill);
            try
            {
                if (weapon == null || !weapon.IsRangedWeapon || !AgentGear.Pays(agent)) return value;
                var item = AgentGear.ItemOf(agent, weapon);
                if (item == null) return value;
                float s = AgentGear.Shortfall(this, agent, item, weapon, out var req);
                if (req == null || req.Kind != SkillKind.Ranged || s <= 0f) return value;
                return value * Penalties.InaccuracyMultiplier(s, ItemRequirements.Settings);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.inaccuracy", e);
                return value;
            }
        }

        public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        {
            bool baseAllows = BaseModel.CanAgentRideMount(agent, targetMount);
            if (baseAllows) return true;
            try
            {
                if (agent == null || targetMount == null || !AgentGear.Pays(agent)) return false; // troops: vanilla
                bool canRide = (agent.GetAgentFlags() & AgentFlag.CanRide) != AgentFlag.None;
                // What vanilla's check compared against (0 once our UpdateAgentStats ran on the horse) - so a refusal
                // with difficulty 0 had another reason and stands.
                float difficulty = targetMount.GetAgentDrivenPropertyValue(DrivenProperty.MountDifficulty);
                int skill = GetEffectiveSkill(agent, DefaultSkills.Riding);
                return CombatPenalties.AllowMount(false, canRide, skill, difficulty);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("stats.ride-mount", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ abstract members (forward)

        public override float GetDifficultyModifier() => BaseModel.GetDifficultyModifier();

        public override float GetEquipmentStealthBonus(Agent agent) => BaseModel.GetEquipmentStealthBonus(agent);

        public override float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon) => BaseModel.GetSneakAttackMultiplier(agent, weapon);

        public override float GetKnockBackResistance(Agent agent) => BaseModel.GetKnockBackResistance(agent);

        public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid) => BaseModel.GetKnockDownResistance(agent, strikeType);

        public override float GetDismountResistance(Agent agent) => BaseModel.GetDismountResistance(agent);

        public override float GetBreatheHoldMaxDuration(Agent agent, float baseBreatheHoldMaxDuration) => BaseModel.GetBreatheHoldMaxDuration(agent, baseBreatheHoldMaxDuration);

        // ------------------------------------------------------------------ virtual members (forward ALL)

        public override void InitializeMissionEquipment(Agent agent) => BaseModel.InitializeMissionEquipment(agent);

        public override void InitializeAgentStatsAfterDeploymentFinished(Agent agent)
        {
            SyncAiLevel();
            BaseModel.InitializeAgentStatsAfterDeploymentFinished(agent);
        }

        public override void InitializeMissionEquipmentAfterDeploymentFinished(Agent agent) => BaseModel.InitializeMissionEquipmentAfterDeploymentFinished(agent);

        public override bool HasHeavyArmor(Agent agent) => BaseModel.HasHeavyArmor(agent);

        public override float GetEffectiveArmorEncumbrance(Agent agent, Equipment equipment) => BaseModel.GetEffectiveArmorEncumbrance(agent, equipment);

        public override float GetEffectiveMaxHealth(Agent agent) => BaseModel.GetEffectiveMaxHealth(agent);

        public override float GetEnvironmentSpeedFactor(Agent agent) => BaseModel.GetEnvironmentSpeedFactor(agent);

        public override float GetDetachmentCostMultiplierOfAgent(Agent agent, IDetachment detachment) => BaseModel.GetDetachmentCostMultiplierOfAgent(agent, detachment);

        public override float GetInteractionDistance(Agent agent) => BaseModel.GetInteractionDistance(agent);

        public override float GetMaxCameraZoom(Agent agent) => BaseModel.GetMaxCameraZoom(agent);

        public override int GetEffectiveSkill(Agent agent, SkillObject skill) => BaseModel.GetEffectiveSkill(agent, skill);

        public override int GetEffectiveSkillForWeapon(Agent agent, WeaponComponentData weapon) => BaseModel.GetEffectiveSkillForWeapon(agent, weapon);

        public override string GetMissionDebugInfoForAgent(Agent agent) => BaseModel.GetMissionDebugInfoForAgent(agent);
    }
}
