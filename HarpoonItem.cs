using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    internal class HarpoonItem
    {
        internal const string itemNameSpearChitin = "SpearChitin";
        internal const string itemDropNameSpearChitin = "$item_spear_chitin";

        internal static GameObject spearChitinPrefab;

        internal static bool HarpoonEquipped(Player player)
        {
            return (player.GetLeftItem()?.m_shared.m_name == itemDropNameSpearChitin) ||
                   (player.GetRightItem()?.m_shared.m_name != itemDropNameSpearChitin);
        }

        internal static void PatchInventory(Inventory inventory)
        {
            if (inventory == null)
                return;

            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            inventory.GetAllItems(itemDropNameSpearChitin, items);

            foreach (ItemDrop.ItemData item in items)
                PatchHarpoonItemData(item);
        }

        internal static void PatchHarpoonItemOnConfigChange()
        {
            PatchHarpoonItemData(spearChitinPrefab?.GetComponent<ItemDrop>()?.m_itemData, inventoryItemUpdate: false);

            PatchInventory(Player.m_localPlayer?.GetInventory());
        }

        private static void PatchHarpoonItemData(ItemDrop.ItemData item, bool inventoryItemUpdate = true)
        {
            if (item == null)
                return;

            item.m_shared.m_maxQuality = Math.Max(Math.Min(maxQuality.Value, 4), 1);
            item.m_shared.m_durabilityPerLevel = Mathf.Clamp(durabilityPerLevel.Value, 50, 500);

            item.m_shared.m_useDurability = !disableDurability.Value;
            item.m_shared.m_useDurabilityDrain = durabilityDrain.Value;
            item.m_shared.m_attack.m_attackStamina = disableStamina.Value ? 0f : attackStamina.Value;
            item.m_shared.m_attack.m_damageMultiplier = disableDamage.Value ? 0f : 1f;
            item.m_shared.m_attack.m_hitFriendly = true;
            item.m_shared.m_attack.m_projectileVel = 30f * projectileVelocityMultiplier.Value;
            item.m_shared.m_attack.m_destroyPreviousProjectile = true;

            if (item.m_shared.m_attack.m_attackProjectile != null && item.m_shared.m_attack.m_attackProjectile.TryGetComponent(out Projectile projectile))
            {
                projectile.m_gravity = 5f * projectileGravityMiltiplier.Value;
                projectile.m_rayRadius = Mathf.Clamp(hitboxSize.Value, 0.0f, 0.5f);
            }

            if (!inventoryItemUpdate || item.m_durability > item.GetMaxDurability())
                item.m_durability = item.GetMaxDurability();
        }

        private static void PatchHarpoonStatusEffect(SE_Harpooned statusEffect)
        {
            if (statusEffect == null) return;

            statusEffect.m_breakDistance = breakDistance.Value;
            statusEffect.m_maxDistance = maxDistance.Value;
            statusEffect.m_staminaDrain = 0.1f * drainStamina.Value;
            statusEffect.m_pullSpeed = pullSpeed.Value;
            statusEffect.m_smoothDistance = smoothDistance.Value;
            statusEffect.m_forcePower = forcePower.Value;
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_HarpoonStat
        {
            public static void PatchHarpoonStatusEffectAndRecipe(ObjectDB __instance)
            {
                spearChitinPrefab = __instance.GetItemPrefab(itemNameSpearChitin);
                if (spearChitinPrefab == null)
                    return;

                ItemDrop item = spearChitinPrefab.GetComponent<ItemDrop>();
                if (item == null)
                    return;

                PatchHarpoonItemData(item.m_itemData);

                foreach (StatusEffect statusEffect in __instance.m_StatusEffects)
                {
                    if (statusEffect.name == statusEffectNameHarpooned && statusEffect is SE_Harpooned)
                    {
                        harpoonedStatusEffect = statusEffect as SE_Harpooned;
                        PatchHarpoonStatusEffect(harpoonedStatusEffect);
                        break;
                    }
                }

                Recipe recipe = __instance.GetRecipe(item.m_itemData);
                if (recipe != null)
                    foreach (Piece.Requirement resource in recipe.m_resources)
                        resource.m_amountPerLevel = (resource.m_resItem.m_itemData.m_shared.m_name != "$item_chitin") ? 0 : 20;
            }
            private static void Postfix(ObjectDB __instance)
            {
                PatchHarpoonStatusEffectAndRecipe(__instance);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class ObjectDB_CopyOtherDB_HarpoonStat
        {
            private static void Postfix(ObjectDB __instance)
            {
                ObjectDB_Awake_HarpoonStat.PatchHarpoonStatusEffectAndRecipe(__instance);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Load))]
        public class Inventory_Load_CircletStats
        {
            public static void Postfix(Inventory __instance)
            {
                PatchInventory(__instance);
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
        public static class ItemDrop_Start_HarpoonStats
        {
            private static void Postfix(ref ItemDrop __instance)
            {
                if (__instance.GetPrefabName(__instance.name) != itemNameSpearChitin)
                    return;

                PatchHarpoonItemData(__instance.m_itemData);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public class Player_OnSpawned_HarpoonStats
        {
            public static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer)
                    return;

                PatchInventory(__instance.GetInventory());
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
        public static class Player_AddKnownItem_HarpoonStats
        {
            private static void Postfix(ref ItemDrop.ItemData item)
            {
                if (item.m_shared.m_name != itemNameSpearChitin)
                    return;

                PatchHarpoonItemData(item);
            }
        }

    }
}
