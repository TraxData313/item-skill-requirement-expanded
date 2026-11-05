using System;
using GearMastery.Core;
using HarmonyLib;
using Helpers;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GearMastery.Patches
{
    /// <summary>
    /// Lifts vanilla's hard skill lock on bows, crossbows, slings and horses in the inventory.
    ///
    /// Postfix on <c>CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement, out TextObject)</c>
    /// (the 2-argument overload calls this one). Vanilla says no when skill &lt; item.Difficulty, OR the item is
    /// gender-locked, a dragon banner piece, or an unrideable horse. When it said no, the item is one we make soft
    /// (kind Ranged / Mount), the character is covered by the who-toggles (<see cref="AgentGear.CharacterPays"/>) and
    /// none of the OTHER reasons hold, the answer becomes yes with no reason.
    ///
    /// Callers (decompile, 1.4.8): SPInventoryVM.CanCharacterUseItem (equip lock + item dimming) and
    /// ItemMenuVM.AddSkillRequirement (the red / green colour - replaced by <see cref="ItemTooltipPatch"/>, which
    /// colours from skill &lt; R itself, so the unlock does not turn every line green).
    /// </summary>
    internal static class CanUseItemPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(CharacterHelper), nameof(CharacterHelper.CanUseItem),
                    new[] { typeof(BasicCharacterObject), typeof(EquipmentElement), typeof(TextObject).MakeByRefType() })
                ?? throw new MissingMethodException("CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement, out TextObject) not found");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(CanUseItemPatch), nameof(Postfix)));
        }

        private static void Postfix(BasicCharacterObject currentCharacter, EquipmentElement itemRosterElement,
            ref TextObject reason, ref bool __result)
        {
            if (__result) return;
            try
            {
                var item = itemRosterElement.Item;
                if (item == null || currentCharacter == null) return;
                // Only for characters the who-filter covers: troops (other mods ask for them too) and heroes whose
                // toggle is off keep vanilla's hard lock, as they keep vanilla's mount lock in battle.
                if (!AgentGear.CharacterPays(currentCharacter)) return;
                var req = ItemRequirements.Get(item);
                if (req == null || (req.Kind != SkillKind.Ranged && req.Kind != SkillKind.Mount)) return;
                if (BlockedForOtherReason(currentCharacter, item)) return;
                __result = true;
                reason = null!;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("CanUseItemPatch", e);
            }
        }

        /// <summary>Vanilla's non-skill reasons, mirrored from CanUseItem (1.4.8).</summary>
        private static bool BlockedForOtherReason(BasicCharacterObject character, ItemObject item)
        {
            bool genderOk = (!character.IsFemale || (item.ItemFlags & ItemFlags.NotUsableByFemale) == 0)
                && (character.IsFemale || (item.ItemFlags & ItemFlags.NotUsableByMale) == 0);
            string id = item.StringId ?? "";
            bool dragonBanner = id == "dragon_banner_center" || id == "dragon_banner_dragonhead" || id == "dragon_banner_handle";
            bool unrideable = item.HasHorseComponent && !item.HorseComponent.IsRideable;
            return !genderOk || dragonBanner || unrideable;
        }
    }
}
