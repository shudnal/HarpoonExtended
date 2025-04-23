using HarmonyLib;
using System.Linq;
using UnityEngine;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    internal class TurretBoltHarpoonItem
    {
        public const string itemName = "TurretBoltHarpoon";
        public static int itemHash = itemName.GetStableHashCode();
        public const string itemDropName = "$hrpnext_item_turretboltharpoon";
        public const string itemDropDescription = "$hrpnext_item_turretboltharpoon_description";

        public static GameObject turretBoltHarpoonPrefab;

        private static void CreateTurretBoltHarpoonPrefab()
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab("TurretBoltBone");
            if (prefab == null)
                return;

            var chitinharpoon = CustomPrefabs.InitPrefabClone(ObjectDB.instance.GetItemPrefab("SpearChitin").GetComponent<ItemDrop>().m_itemData.m_shared.m_attack.m_attackProjectile, "Turret_projectileharpoon");

            if (chitinharpoon.GetComponent<Projectile>() is Projectile projectile)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(projectile), chitinharpoon.AddComponent<HarpoonProjectile>());
                UnityEngine.Object.Destroy(projectile);
            }

            LogInfo($"Created prefab {chitinharpoon.name}");

            turretBoltHarpoonPrefab = CustomPrefabs.InitPrefabClone(prefab, itemName);

            ItemDrop item = turretBoltHarpoonPrefab.GetComponent<ItemDrop>();
            item.m_itemData = item.m_itemData.Clone();
            item.m_itemData.m_shared.m_attack.m_attackProjectile = chitinharpoon;
            item.m_itemData.m_shared.m_name = itemDropName;
            item.m_itemData.m_shared.m_description = itemDropDescription;
            item.m_itemData.m_shared.m_icons = ObjectDB.instance.GetItemPrefab("SpearChitin").GetComponent<ItemDrop>().m_itemData.m_shared.m_icons;

            LogInfo($"Created prefab {turretBoltHarpoonPrefab.name}");

            if (Resources.FindObjectsOfTypeAll<Turret>().FirstOrDefault(ws => ws.name == "piece_turret")?.GetComponent<Turret>() is Turret turret)
            {
                turret.m_allowedAmmo.Add(new Turret.AmmoType()
                {
                    m_ammo = item,
                    m_visual = turret.m_allowedAmmo.FirstOrDefault(ammo => ammo.m_visual.name == "Bolt Wood").m_visual
                });
            }
        }

        private static void RegisterTurretBoltHarpoonPrefab()
        {
            ClearPrefabReferences();

            if (!(bool)turretBoltHarpoonPrefab)
                CreateTurretBoltHarpoonPrefab();

            if (!(bool)turretBoltHarpoonPrefab)
                return;

            //PatchTurretBoltHarpoonItem(turretBoltHarpoonPrefab.GetComponent<ItemDrop>()?.m_itemData, inventoryItemUpdate: false);

            if (ObjectDB.instance && !ObjectDB.instance.m_itemByHash.ContainsKey(itemHash))
            {
                ObjectDB.instance.m_items.Add(turretBoltHarpoonPrefab);
                ObjectDB.instance.m_itemByHash.Add(itemHash, turretBoltHarpoonPrefab);
            }

            if (ZNetScene.instance && !ZNetScene.instance.m_namedPrefabs.ContainsKey(itemHash))
            {
                ZNetScene.instance.m_prefabs.Add(turretBoltHarpoonPrefab);
                ZNetScene.instance.m_namedPrefabs.Add(itemHash, turretBoltHarpoonPrefab);
            }

            //SetRecipes();
        }

        private static void ClearPrefabReferences()
        {
            if (ObjectDB.instance && ObjectDB.instance.m_itemByHash.ContainsKey(itemHash))
            {
                ObjectDB.instance.m_items.Remove(ObjectDB.instance.m_itemByHash[itemHash]);
                ObjectDB.instance.m_itemByHash.Remove(itemHash);
            }

            if (ZNetScene.instance && ZNetScene.instance.m_namedPrefabs.ContainsKey(itemHash))
            {
                ZNetScene.instance.m_prefabs.Remove(ZNetScene.instance.m_namedPrefabs[itemHash]);
                ZNetScene.instance.m_namedPrefabs.Remove(itemHash);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_AddPrefab
        {
            private static void Postfix(ObjectDB __instance)
            {
                if (__instance.m_items.Count == 0 || __instance.GetItemPrefab("Wood") == null)
                    return;

                RegisterTurretBoltHarpoonPrefab();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class ObjectDB_CopyOtherDB_AddPrefab
        {
            private static void Postfix(ObjectDB __instance)
            {
                if (__instance.m_items.Count == 0 || __instance.GetItemPrefab("Wood") == null)
                    return;

                RegisterTurretBoltHarpoonPrefab();
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnDestroy))]
        public static class FejdStartup_OnDestroy_AddPrefab
        {
            private static void Prefix()
            {
                ClearPrefabReferences();
            }
        }
    }
}
