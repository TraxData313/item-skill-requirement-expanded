using System;
using System.Collections.Generic;
using GearMastery.Core;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GearMastery.Patches
{
    /// <summary>
    /// The inventory item tooltip (ItemMenuVM): "Requires: &lt;Skill&gt; &lt;R&gt;" red / green for every item we cover,
    /// plus a red "Unskilled: ..." line when the character is short.
    ///
    /// Vanilla (1.4.8) - SetGeneralComponentTooltip draws, after the weight line, AddSkillRequirement for the target
    /// item and (comparing) the compared item, but only when the target has a RelevantSkill and either item has
    /// Difficulty &gt; 0. AddSkillRequirement colours by CharacterHelper.CanUseItem, which <see cref="CanUseItemPatch"/>
    /// now opens up for bows and horses - so:
    /// - Prefix on AddSkillRequirement: for an item we cover with R &gt; 0, draw our line (colour = skill &lt; R) and skip
    ///   vanilla's. Anything else (e.g. an uncovered modded item with Difficulty) runs vanilla.
    /// - Postfix on SetGeneralComponentTooltip: when vanilla did NOT call AddSkillRequirement (all our tier-based items:
    ///   Difficulty 0), add the requirement line ourselves; then the penalty line. In comparison mode both property
    ///   lists get a row (blank on the side without one) so the two columns stay aligned.
    /// Character = ItemMenuVM._character (the hero whose equipment is open); skill = GetSkillValue, as vanilla.
    /// </summary>
    internal static class ItemTooltipPatch
    {
        // Private ItemMenuVM fields. Set in InitFieldRefs (from Apply, inside the caller's try/catch) - NOT in field
        // initialisers: a failing static constructor would throw TypeInitializationException from the patch methods,
        // outside their own try/catch, i.e. inside the game's tooltip code.
        private static AccessTools.FieldRef<ItemMenuVM, BasicCharacterObject>? CharacterRef;
        private static AccessTools.FieldRef<ItemMenuVM, ItemVM>? TargetRef;
        private static AccessTools.FieldRef<ItemMenuVM, ItemVM>? ComparedRef;

        private static void InitFieldRefs()
        {
            if (CharacterRef != null && TargetRef != null && ComparedRef != null) return;
            CharacterRef = AccessTools.FieldRefAccess<ItemMenuVM, BasicCharacterObject>("_character");
            TargetRef = AccessTools.FieldRefAccess<ItemMenuVM, ItemVM>("_targetItem");
            ComparedRef = AccessTools.FieldRefAccess<ItemMenuVM, ItemVM>("_comparedItem");
        }

        /// <summary>Vanilla's own text and id (ItemMenuVM._requiresText), so it localises like the native line.</summary>
        private static TextObject RequiresText() => new TextObject("{=154a34f8caccfc833238cc89d38861e8}Requires: ");

        public static void ApplyRequirementLine(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ItemMenuVM), "AddSkillRequirement",
                    new[] { typeof(ItemVM), typeof(MBBindingList<ItemMenuTooltipPropertyVM>), typeof(bool) })
                ?? throw new MissingMethodException("ItemMenuVM.AddSkillRequirement(ItemVM, MBBindingList<ItemMenuTooltipPropertyVM>, bool) not found");
            InitFieldRefs();
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(AddSkillRequirementPrefix)));
        }

        /// <summary>Remembers the usage tab being drawn: RefreshItemTooltips(item, compared, alternativeUsageIndex) runs
        /// SetGeneralComponentTooltip (our lines) BEFORE SetWeaponComponentTooltip stores the index in
        /// AlternativeUsageIndex, so the public property can still hold the previous item's tab at that point.</summary>
        public static void ApplyUsageIndex(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ItemMenuVM), "RefreshItemTooltips", new[] { typeof(ItemVM), typeof(ItemVM), typeof(int) })
                ?? throw new MissingMethodException("ItemMenuVM.RefreshItemTooltips(ItemVM, ItemVM, int) not found");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(RefreshItemTooltipsPrefix)));
        }

        // Usage tab of the tooltip being built (UI thread only). 0 = primary usage, vanilla's default.
        private static int _usageIndex;

        private static void RefreshItemTooltipsPrefix(int alternativeUsageIndex) => _usageIndex = alternativeUsageIndex;

        public static void ApplyGeneralTooltip(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ItemMenuVM), "SetGeneralComponentTooltip", Type.EmptyTypes)
                ?? throw new MissingMethodException("ItemMenuVM.SetGeneralComponentTooltip() not found");
            InitFieldRefs(); // a renamed field fails here (logged by the caller), not in every tooltip
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(SetGeneralComponentTooltipPostfix)));
        }

        private static bool AddSkillRequirementPrefix(ItemMenuVM __instance, ItemVM itemVm,
            MBBindingList<ItemMenuTooltipPropertyVM> itemProperties, bool isComparison)
        {
            try
            {
                var req = Covered(itemVm);
                if (req == null || itemProperties == null || CharacterRef == null) return true;
                itemProperties.Add(RequirementLine(req, SkillOf(__instance, req, isComparison), CharacterRef(__instance), isComparison));
                return false;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemTooltipPatch.AddSkillRequirement", e);
                return true;
            }
        }

        private static void SetGeneralComponentTooltipPostfix(ItemMenuVM __instance)
        {
            try
            {
                if (CharacterRef == null || TargetRef == null || ComparedRef == null) return;
                var target = TargetRef(__instance);
                var targetItem = target?.ItemRosterElement.EquipmentElement.Item;
                if (targetItem == null) return;
                var compared = ComparedRef(__instance);
                bool comparing = __instance.IsComparing && compared?.ItemRosterElement.EquipmentElement.Item != null;
                var comparedItem = comparing ? compared!.ItemRosterElement.EquipmentElement.Item : null;
                var character = CharacterRef(__instance);

                var targetReq = Covered(target);
                var comparedReq = comparing ? Covered(compared) : null;
                if (targetReq == null && comparedReq == null) return;

                // Same condition as vanilla's call to AddSkillRequirement (our prefix already drew those lines).
                bool vanillaDrew = targetItem.RelevantSkill != null
                    && (targetItem.Difficulty > 0 || (comparing && comparedItem!.Difficulty > 0));
                if (!vanillaDrew)
                {
                    __instance.TargetItemProperties.Add(targetReq != null ? RequirementLine(targetReq, SkillOf(__instance, targetReq, false), character, false) : Blank());
                    if (comparing)
                        __instance.ComparedItemProperties.Add(comparedReq != null ? RequirementLine(comparedReq, SkillOf(__instance, comparedReq, true), character, true) : Blank());
                }

                string? targetPenalty = targetReq != null ? PenaltyText(targetReq, SkillOf(__instance, targetReq, false), character) : null;
                string? comparedPenalty = comparing && comparedReq != null ? PenaltyText(comparedReq, SkillOf(__instance, comparedReq, true), character) : null;
                if (targetPenalty == null && comparedPenalty == null) return;
                __instance.TargetItemProperties.Add(targetPenalty != null
                    ? new ItemMenuTooltipPropertyVM(UnskilledText().ToString(), targetPenalty, 0, UIColors.NegativeIndicator)
                    : Blank());
                if (comparing)
                    __instance.ComparedItemProperties.Add(comparedPenalty != null
                        ? new ItemMenuTooltipPropertyVM("", comparedPenalty, 0, UIColors.NegativeIndicator)
                        : Blank());
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemTooltipPatch.SetGeneralComponentTooltip", e);
            }
        }

        /// <summary>Our requirement for this tooltip item, only when it asks something (R &gt; 0).</summary>
        private static ItemRequirement? Covered(ItemVM? itemVm)
        {
            var item = itemVm?.ItemRosterElement.EquipmentElement.Item;
            var req = ItemRequirements.Get(item);
            return req != null && req.HasRequirement && req.Skill != null ? req : null;
        }

        /// <summary>
        /// The skill for the usage tab on show: the target item's tab index; for the compared item, its usage of the same
        /// kind (WeaponDescriptionId, as vanilla pairs them in GetComparedWeapon), else its primary skill.
        /// </summary>
        private static SkillObject SkillOf(ItemMenuVM vm, ItemRequirement req, bool compared)
        {
            if (req.UsageCount == 0) return req.Skill;
            if (!compared) return req.SkillForUsage(_usageIndex);
            var targetItem = TargetRef?.Invoke(vm)?.ItemRosterElement.EquipmentElement.Item;
            var weapons = targetItem?.Weapons;
            var usage = weapons != null && _usageIndex >= 0 && _usageIndex < weapons.Count ? weapons[_usageIndex] : null;
            return req.SkillFor(usage);
        }

        /// <summary>Vanilla's look: "Requires: " (target side only) + "&lt;Skill&gt; &lt;R&gt;", green when met or no
        /// character, red when short.</summary>
        private static ItemMenuTooltipPropertyVM RequirementLine(ItemRequirement req, SkillObject skill, BasicCharacterObject? character, bool isComparison)
        {
            bool met = character == null
                || !TooltipPenalties.IsShort(req.Value, ItemRequirements.GetSkill(character, skill));
            string definition = isComparison ? "" : RequiresText().ToString();
            string value = skill.Name + " " + req.Value;
            return new ItemMenuTooltipPropertyVM(definition, value, 0, met ? UIColors.PositiveIndicator : UIColors.NegativeIndicator);
        }

        private static ItemMenuTooltipPropertyVM Blank() => new ItemMenuTooltipPropertyVM("", "", 0);

        /// <summary>"-12% damage, -8% swing speed, -12% handling", or null when not short / nothing rounds above 0%.</summary>
        private static string? PenaltyText(ItemRequirement req, SkillObject skillObject, BasicCharacterObject? character)
        {
            if (character == null) return null;
            int skill = ItemRequirements.GetSkill(character, skillObject);
            if (!TooltipPenalties.IsShort(req.Value, skill)) return null;
            var parts = TooltipPenalties.For(req.Kind, Requirements.Shortfall(req.Value, skill), ItemRequirements.Settings);
            if (parts.Count == 0) return null;
            var texts = new List<string>(parts.Count);
            foreach (var part in parts)
            {
                var t = PartText(part.Stat);
                t.SetTextVariable("VALUE", part.SignedPercent);
                texts.Add(t.ToString());
            }
            return string.Join(", ", texts);
        }

        private static TextObject UnskilledText() => new TextObject("{=gm_unskilled}Unskilled: ");

        // New TextObject each time: SetTextVariable mutates it.
        private static TextObject PartText(PenaltyStat stat) => stat switch
        {
            PenaltyStat.Damage => new TextObject("{=gm_pen_damage}{VALUE}% damage"),
            PenaltyStat.SwingSpeed => new TextObject("{=gm_pen_swing}{VALUE}% swing speed"),
            PenaltyStat.Handling => new TextObject("{=gm_pen_handling}{VALUE}% handling"),
            PenaltyStat.ShieldDamageTaken => new TextObject("{=gm_pen_shield}{VALUE}% shield damage taken"),
            PenaltyStat.FootSpeedUpTo => new TextObject("{=gm_pen_armour}up to {VALUE}% foot speed (shared by armour weight)"),
            PenaltyStat.ReloadSpeed => new TextObject("{=gm_pen_reload}{VALUE}% reload and draw speed"),
            PenaltyStat.Inaccuracy => new TextObject("{=gm_pen_aim}{VALUE}% inaccuracy"),
            PenaltyStat.HorseSpeed => new TextObject("{=gm_pen_horse_speed}{VALUE}% speed"),
            PenaltyStat.HorseManeuver => new TextObject("{=gm_pen_horse_maneuver}{VALUE}% maneuver"),
            _ => new TextObject("{VALUE}%"),
        };
    }
}
