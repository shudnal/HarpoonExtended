using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;

namespace HarpoonExtended
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class HarpoonExtended : BaseUnityPlugin
    {
        const string pluginID = "shudnal.HarpoonExtended";
        const string pluginName = "Harpoon Extended";
        const string pluginVersion = "1.1.10";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> loggingEnabled;
        private static ConfigEntry<bool> deepLoggingEnabled;

        private static ConfigEntry<bool> messagesEnabled;
        private static ConfigEntry<bool> targetMessagesEnabled;

        private static ConfigEntry<float> timeBeforeStop;
        private static ConfigEntry<bool> applySlowFall;
        private static ConfigEntry<bool> attachedShipStamina;
        private static ConfigEntry<bool> pullUnderWater;
        private static ConfigEntry<bool> removeSlowFallWithoutHarpoon;
        private static ConfigEntry<float> removeSlowFallonGroundThreshold;

        private static ConfigEntry<bool> targetPulling;
        private static ConfigEntry<float> pullSpeedMultiplier;
        private static ConfigEntry<float> maxBodyMassToPull;
        private static ConfigEntry<float> containerInventoryWeightMassFactor;

        private static ConfigEntry<bool> targetCreatures;
        private static ConfigEntry<bool> targetShip;
        private static ConfigEntry<bool> targetTreeLog;
        private static ConfigEntry<bool> targetTreeBase;
        private static ConfigEntry<bool> targetFish;
        private static ConfigEntry<bool> targetPiece;
        private static ConfigEntry<bool> targetDestructibles;
        private static ConfigEntry<bool> targetLeviathan;
        private static ConfigEntry<bool> targetItems;
        private static ConfigEntry<bool> targetBosses;
        private static ConfigEntry<bool> targetGround;

        private static ConfigEntry<float> breakDistance;
        private static ConfigEntry<float> maxDistance;
        private static ConfigEntry<float> drainStamina;
        private static ConfigEntry<float> minDistanceShip;
        private static ConfigEntry<float> minDistanceCreature;
        private static ConfigEntry<float> minDistanceItem;
        private static ConfigEntry<float> minDistancePullToTarget;
        private static ConfigEntry<float> minDistancePullToPlayer;

        private static ConfigEntry<float> pullSpeed;
        private static ConfigEntry<float> smoothDistance;
        private static ConfigEntry<float> pullForceMultiplier;
        private static ConfigEntry<float> forcePower;
        private static ConfigEntry<bool> useForce;
        private static ConfigEntry<bool> alwaysPullTo;
        private static ConfigEntry<float> maximumVelocity;

        private static ConfigEntry<float> projectileGravityMiltiplier;
        private static ConfigEntry<float> hitboxSize;
        private static ConfigEntry<float> projectileVelocityMultiplier;

        private static ConfigEntry<int> maxQuality;
        private static ConfigEntry<float> durabilityPerLevel;
        private static ConfigEntry<bool> disableDurability;
        private static ConfigEntry<float> durabilityDrain;
        private static ConfigEntry<float> attackStamina;
        private static ConfigEntry<bool> disableDamage;
        private static ConfigEntry<bool> disableStamina;

        private static ConfigEntry<KeyboardShortcut> shortcutPull;
        private static ConfigEntry<KeyboardShortcut> shortcutPullTo;
        private static ConfigEntry<KeyboardShortcut> shortcutRelease;
        private static ConfigEntry<KeyboardShortcut> shortcutStop;

        internal static int s_rayMaskSolidsAndItem = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle", "item");
        internal static int s_rayMaskSolids;

        internal static int m_slowFallHash = "SlowFall".GetStableHashCode();
        internal const string prefabNameSpearChitin = "SpearChitin";
        internal const string itemDropNameSpearChitin = "$item_spear_chitin";
        internal const string statusEffectNameHarpooned = "Harpooned";

        internal static HarpoonExtended instance;

        public static float m_pullSpeed = 1000f;

        public static float m_smoothDistance = 2f;

        public static float m_maxLineSlack = 0.3f;

        public static float m_breakDistance = 15f;

        public static float m_minDistance = 2f;

        public static float m_maxDistance = 50f;

        public static float m_staminaDrain = 0.1f;

        public static float m_staminaDrainInterval = 0.1f;

        public static bool m_broken;

        public static float m_time;

        public static Player m_attacker;

        public static float targetDistance = 999999f;

        public static LineConnect m_line;

        public static float m_drainStaminaTimer;

        public static GameObject harpooned;

        public static Rigidbody objectRbody;
        public static Rigidbody attackerRbody;

        public static GameObject targetHarpooned;

        public static string targetName;

        public static bool noUpForce;

        public static ZNetView m_nview;

        public static Ship m_ship;

        public static Character m_character;

        public static float objectMass;

        public static LineRenderer m_lineRenderer;

        public static bool isPullingTo;

        public static bool castSlowFall = false;
        public static bool slowFallCasted = false;
        public static float onGroundTimer = 0f;

        public static SE_Harpooned harpoonedStatusEffect;

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value && !data.ToString().IsNullOrWhiteSpace())
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("1 - General", "NexusID", 2528, "Nexus mod ID for updates", false);

            modEnabled = config("1 - General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("1 - General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("1 - General", "Logging enabled", defaultValue: false, "Enable logging for debug events. [Not Synced with Server]", false);

            targetCreatures = config("2 - Targets", "Creatures (override)", defaultValue: true, "Enable pulling creatures. Overrides vanilla behaviour. Restart required after change.");
            targetShip = config("2 - Targets", "Ship", defaultValue: true, "Enable pulling ships.");
            targetTreeLog = config("2 - Targets", "Tree log", defaultValue: true, "Enable pulling logs.");
            targetTreeBase = config("2 - Targets", "Trees", defaultValue: true, "Enable pulling to trees.");
            targetFish = config("2 - Targets", "Fish", defaultValue: true, "Enable pulling fish.");
            targetPiece = config("2 - Targets", "Buildings", defaultValue: true, "Enable pulling to buildings.");
            targetDestructibles = config("2 - Targets", "Destructibles", defaultValue: true, "Enable pulling to destructibles.");
            targetLeviathan = config("2 - Targets", "Leviathan", defaultValue: false, "Enable pulling a Leviathan. Use with caution. Can cause deadly effect");
            targetItems = config("2 - Targets", "Items", defaultValue: true, "Enable pulling an items. Fish considered as item.");
            targetBosses = config("2 - Targets", "Bosses", defaultValue: false, "Enable pulling a boss.");
            targetGround = config("2 - Targets", "Any target", defaultValue: false, "Track any hitpoint. Every hit collision. Terrain included.");

            messagesEnabled = config("6 - Misc", "Enabled harpooning messages", defaultValue: true, "Enable localized notification of current state. [Not Synced with Server]", false);
            timeBeforeStop = config("6 - Misc", "Time before harpoon can be dropped", defaultValue: 1.0f, "Time in seconds the harpoon should exists before it can be released. To prevent spam mistakes. [Not Synced with Server]", false);
            applySlowFall = config("6 - Misc", "Apply Feather Fall while harpooning around", defaultValue: true, "Apply Feather Fall while using the harpoon to prevent fall damage");
            drainStamina = config("6 - Misc", "Stamina drain multiplier", defaultValue: 1.0f, "Stamina drain for target pulling.");
            attachedShipStamina = config("6 - Misc", "No stamina usage while attached to ship", defaultValue: true, "Disable stamina usage while attached to ship");
            pullUnderWater = config("6 - Misc", "Pull to underwater", defaultValue: true, "Pull to underwater terrain.");
            removeSlowFallWithoutHarpoon = config("6 - Misc", "Remove Feather Fall without harpoon", defaultValue: false, "Remove Feather Fall if harpoon is not equipped.");
            removeSlowFallonGroundThreshold = config("6 - Misc", "Remove Feather Fall after seconds on ground", defaultValue: 2f, "Remove Feather Fall if a player stays without harpoon line on the ground for set amount of seconds."); 

            targetPulling = config("3 - Pull", "Enable pulling", defaultValue: true, "Enable active pulling harpooned target or yourself. Hold Use button to retrieve line or Crouch + Use buttons to cast line.");
            pullSpeedMultiplier = config("3 - Pull", "Harpoon line casting and retrieving speed multiplier", defaultValue: 1.0f, "Speed of line casting and retrieving");
            maxBodyMassToPull = config("3 - Pull", "Maximum mass you can pull", defaultValue: 1000.0f, "Objects with mass more than set will not be pulled but instead you will be pulled to them." +
                                                                                                   "\nLeviathan mass is 1000, Lox 60, Serpent 30. Ships excluded from this restriction." +
                                                                                                   "\nContainers and items weight depends on \"Pulled container inventory weight mass factor\"");
            containerInventoryWeightMassFactor = config("3 - Pull", "Pulled container inventory weight mass factor", defaultValue: 0.1f, "If pulled object contains inventory, like CargoCrate, the inventory total weight will be multiplied by that factor." +
                                                                                                                                     "\nThis calculation also applies to pulling items but this effect is mostly negligible except some heavy stack");

            breakDistance = config("4 - Line", "Break distance", defaultValue: 15f, "Line will break if distance between you and target will be more than target line length + break distance.");
            maxDistance = config("4 - Line", "Max distance", defaultValue: 50f, "Max distance. Balanced is 100. Big numbers (>200) will work but may cause unwanted net code effects.");
            minDistanceShip = config("4 - Line", "Min distance (Ship)", defaultValue: 5f, "Minimal distance where the line broke to avoid unwanted collisions (Ships)");
            minDistanceCreature = config("4 - Line", "Min distance (Creature)", defaultValue: 0.5f, "Minimal distance where the line broke to avoid unwanted collisions (living creatures)");
            minDistanceItem = config("4 - Line", "Min distance (Item)", defaultValue: 0.1f, "Minimal distance where the line broke to avoid unwanted collisions (items)");
            minDistancePullToTarget = config("4 - Line", "Min distance (pull to target)", defaultValue: 1f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling player to general target)");
            minDistancePullToPlayer = config("4 - Line", "Min distance (pull to player)", defaultValue: 2f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling general target to player)");
       
            maxQuality = config("5 - Item", "Max quality", defaultValue: 4, "Maximum quality level");
            durabilityPerLevel = config("5 - Item", "Durability per level", defaultValue: 100f, "Durability added per level");
            durabilityDrain = config("5 - Item", "Durability drain on attack", defaultValue: 1f, "Durability drain on usage");
            attackStamina = config("5 - Item", "Stamina drain on attack", defaultValue: 15f, "Stamina drain on usage");
            disableDurability = config("5 - Item", "Disable harpoon durability usage", defaultValue: false, "Make harpoon to not use durability. Restart required after change.");
            disableDamage = config("5 - Item", "Disable harpoon damage", defaultValue: false, "Make harpoon to deal no damage. Handy to ride a deathsquito without killing it. Or even birds. Restart required after change.");
            disableStamina = config("5 - Item", "Disable harpoon stamina usage", defaultValue: false, "Make harpoon to not use stamina. Restart required after change.");
            projectileGravityMiltiplier = config("5 - Item", "Projectile gravity multiplier", defaultValue: 1.0f, "Multiplier of gravity affecting harpoon projectile");
            projectileVelocityMultiplier = config("5 - Item", "Projectile velocity multiplier", defaultValue: 1.0f, "Basically speed of initial harpoon flight");

            shortcutPull = config("7 - Shortcuts", "Pull", defaultValue: new KeyboardShortcut(KeyCode.T), "Pull target closer if applicable [Not Synced with Server]", false);
            shortcutPullTo = config("7 - Shortcuts", "Pull To Target mode", defaultValue: new KeyboardShortcut(KeyCode.LeftShift), "Hold why harpoon is flying to make you always pull to target [Not Synced with Server]", false);
            shortcutRelease = config("7 - Shortcuts", "Release", defaultValue: new KeyboardShortcut(KeyCode.T, new KeyCode[1] { KeyCode.LeftControl }), "Release line [Not Synced with Server]", false);
            shortcutStop = config("7 - Shortcuts", "Stop harpooning", defaultValue: new KeyboardShortcut(KeyCode.T, new KeyCode[2] { KeyCode.LeftShift, KeyCode.LeftControl }), "Stop harpooning [Not Synced with Server]", false);

            pullSpeed = config("8 - Debug", "Pull speed", defaultValue: 1000f, "[Math] Pull speed of static line. Used in velocity math. No actual need to mess with it.");
            pullForceMultiplier = config("8 - Debug", "Pull force multiplier", defaultValue: 1f, "[Math] Pull force multiplier. Depends on moved body mass. No actual need to mess with it.");
            smoothDistance = config("8 - Debug", "Smooth distance", defaultValue: 2f, "[Math] Makes the applied force smoother. No actual need to mess with it.");
            forcePower = config("8 - Debug", "Force power", defaultValue: 1f, "[Math] Power (exponentiation part) of the actual force. No actual need to mess with it.");
            useForce = config("8 - Debug", "Use force", defaultValue: true, "[Math] If true - pull physics use force applied to moved body. If false - uses velocity calculation. No actual need to mess with it.");
            targetMessagesEnabled = config("8 - Debug", "Enabled harpooning target message for all objects", defaultValue: false, "Enable unlocalized target name for any object you hit. [Not Synced with Server]", false);
            deepLoggingEnabled = config("8 - Debug", "Logging deep stats", defaultValue: false, "Enable deep logging to debug physics events. [Not Synced with Server]", false);
            hitboxSize = config("8 - Debug", "Hitbox size", defaultValue: 0.0f, "Hitbox size. 0.0 min - 0.5 max. You can try to change it if you have difficulties with aiming small targets");
            alwaysPullTo = config("8 - Debug", "Always pull to", defaultValue: false, "Always pull to target regardress hotkey");
            maximumVelocity = config("8 - Debug", "Maximum velocity", defaultValue: 10f, "Maximum velocity imparted to player rigidbody by harpoon pulling");
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private static bool KeyPressStopHarpoon()
        {
            return shortcutStop.Value.IsDown() || ZInput.GetButton("Block") || ZInput.GetButton("JoyBlock");
        }

        private static bool KeyPressReleaseHarpoon()
        {
            return (KeyPressPullHarpoon() && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch"))) || shortcutRelease.Value.IsPressed();
        }

        private static bool KeyPressPullHarpoon()
        {
            return ZInput.GetButton("Use") || ZInput.GetButton("JoyUse") || shortcutPull.Value.IsPressed();
        }

        private static bool KeyPressPullTo()
        {
            return ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace") || shortcutPullTo.Value.IsPressed();
        }

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
        public static class SE_Harpooned_UpdateStatusEffect_HarpoonPull
        {
            [HarmonyPriority(Priority.First)]
            private static void Postfix(SE_Harpooned __instance, float dt, Character ___m_attacker, Character ___m_character, ref float ___m_baseDistance)
            {
                if (!modEnabled.Value) return;

                if (!targetPulling.Value) return;

                if (___m_attacker != Player.m_localPlayer) return;

                if (___m_character.IsPlayer()) return;

                if ((KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
                {
                    if (!___m_character.IsOwner())
                        ___m_character.m_nview.ClaimOwnership();

                    if (KeyPressReleaseHarpoon())
                        ___m_baseDistance += 4f * dt * pullSpeedMultiplier.Value;
                    else if (KeyPressPullHarpoon())
                        ___m_baseDistance -= 2f * dt * pullSpeedMultiplier.Value;

                    ___m_baseDistance = Mathf.Max(___m_baseDistance, 2f);
                }
            }
        }

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.IsDone))]
        public static class SE_Harpooned_IsDone_HarpoonPull
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(SE_Harpooned __instance, Character ___m_attacker, Character ___m_character, ref bool __result)
            {
                if (!modEnabled.Value) return true;

                if (__instance == null || ___m_attacker == null || ___m_character == null || ___m_attacker != Player.m_localPlayer) return true;

                if (!targetPulling.Value) return true;

                if (KeyPressStopHarpoon())
                {
                    ___m_attacker.Message(MessageHud.MessageType.Center, ___m_character + Localization.instance.Localize(" $msg_harpoon_released"));
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateCrouch))]
        public static class Player_UpdateCrouch_DisableCrouchOnHarpooning
        {
            private static void Prefix(Player __instance, ref bool ___m_crouchToggled)
            {
                if (!modEnabled.Value)
                    return;

                if (Player.m_localPlayer != __instance)
                    return;

                if (harpooned != null && ___m_crouchToggled)
                    ___m_crouchToggled = false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
        public static class Player_FixedUpdate_SlowFallControl
        {
            private static void RemoveSlowFall(SEMan seman, string log)
            {
                slowFallCasted = false;
                
                if (seman.HaveStatusEffect(m_slowFallHash))
                    seman.RemoveStatusEffect(m_slowFallHash, quiet: true);
                
                LogInfo(log);
            }

            private static void CheckSlowFallStatus(Player player)
            {
                if (harpooned != null)
                    return;

                SEMan seman = player.GetSEMan();

                if (removeSlowFallonGroundThreshold.Value > 0f && onGroundTimer >= removeSlowFallonGroundThreshold.Value)
                {
                    RemoveSlowFall(seman, "Remove slow fall on ground");
                    return;
                }

                if (player.IsAttached())
                {
                    RemoveSlowFall(seman, "Remove slow fall on attached");
                    return;
                }

                if (player.IsSwimming())
                {
                    RemoveSlowFall(seman, "Remove slow fall on swimming");
                    return;
                }

                if (player.IsDebugFlying())
                {
                    RemoveSlowFall(seman, "Remove slow fall on flying");
                    return;
                }

                if (removeSlowFallWithoutHarpoon.Value 
                    && (player.GetLeftItem() == null || player.GetLeftItem().m_shared.m_name != itemDropNameSpearChitin) 
                    && (player.GetRightItem() == null || player.GetRightItem().m_shared.m_name != itemDropNameSpearChitin))
                {
                    RemoveSlowFall(seman, "Remove slow fall without harpoon");
                    return;
                }
            }

            private static void Postfix(Player __instance, SEMan ___m_seman)
            {
                if (!modEnabled.Value)
                    return;

                if (Player.m_localPlayer != __instance)
                    return;

                if (castSlowFall)
                {
                    if (!___m_seman.HaveStatusEffect(m_slowFallHash))
                    {
                        slowFallCasted = true;
                        ___m_seman.AddStatusEffect(m_slowFallHash);
                        LogInfo("Cast slow fall");
                    }

                    castSlowFall = false;
                    onGroundTimer = 0f;
                }

                if (slowFallCasted)
                {
                    if (__instance.IsOnGround())
                        onGroundTimer += Time.deltaTime;

                    CheckSlowFallStatus(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        public static class Player_TeleportTo_HarpoonStat
        {
            private static void Postfix(Player __instance)
            {
                if (!modEnabled.Value) return;

                if (m_attacker == __instance && harpooned != null)
                    DestroyHarpooned("Teleport initiated");
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_HarpoonStat
        {
            public static void PatchHarpoonStatusEffectAndRecipe(ObjectDB __instance)
            {
                if (!modEnabled.Value) return;

                GameObject prefab = __instance.GetItemPrefab(prefabNameSpearChitin);
                if (prefab == null)
                    return;

                ItemDrop item = prefab.GetComponent<ItemDrop>();
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

        private static void PatchHarpoonStatusEffect(SE_Harpooned statusEffect)
        {
            if (statusEffect == null) return;

            statusEffect.m_breakDistance = breakDistance.Value;
            statusEffect.m_maxDistance  = maxDistance.Value;
            statusEffect.m_staminaDrain = 0.1f * drainStamina.Value;
            statusEffect.m_pullSpeed = pullSpeed.Value;
            statusEffect.m_smoothDistance = smoothDistance.Value;
            statusEffect.m_forcePower = forcePower.Value;
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Awake))]
        public static class Projectile_Awake_HarpoonStats
        {
            private static void Postfix(Projectile __instance, ZNetView ___m_nview, ref float ___m_gravity, ref float ___m_rayRadius, ref int ___s_rayMaskSolids)
            {
                if (!modEnabled.Value) return;

                if (!__instance.name.StartsWith("projectile_chitinharpoon")) return;

                if (!___m_nview.IsOwner()) return;

                ___m_gravity *= projectileGravityMiltiplier.Value;
                ___m_rayRadius = Mathf.Clamp(hitboxSize.Value, 0.0f, 0.5f);

                if (targetItems.Value)
                {
                    if (s_rayMaskSolids == 0)
                        s_rayMaskSolids =___s_rayMaskSolids;

                    ___s_rayMaskSolids = s_rayMaskSolidsAndItem;
                }
                    
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        public static class Projectile_Setup_HarpoonStats
        {
            private static void Postfix(Projectile __instance, ZNetView ___m_nview, ref HitData.DamageTypes ___m_damage, ref Vector3 ___m_vel)
            {
                if (!modEnabled.Value) return;

                if (!__instance.name.StartsWith("projectile_chitinharpoon")) return;

                if (!___m_nview.IsOwner()) return;

                if (disableDamage.Value)
                    ___m_damage.Modify(0f);

                ___m_vel *= projectileVelocityMultiplier.Value;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
        public static class ZNetScene_Destroy_CheckParentDestroy
        {
            private static void Prefix(ZNetScene __instance, GameObject go)
            {
                if (!modEnabled.Value) return;

                if (harpooned != null && go.transform == harpooned.transform.parent)
                    DestroyHarpooned("Parent destroyed");
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Load))]
        public class Inventory_Load_CircletStats
        {
            public static void Postfix(Inventory __instance)
            {
                if (!modEnabled.Value)
                    return;

                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
                __instance.GetAllItems(itemDropNameSpearChitin, items);

                foreach (ItemDrop.ItemData item in items)
                {
                    PatchHarpoonItemData(item);
                    PatchHarpoonStatusEffect(item.m_shared.m_attackStatusEffect as SE_Harpooned);
                }
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
        public static class ItemDrop_Start_HarpoonStats
        {
            private static void Postfix(ref ItemDrop __instance)
            {
                if (!modEnabled.Value) return;

                if (__instance.GetPrefabName(__instance.name) != prefabNameSpearChitin)
                    return;

                PatchHarpoonItemData(__instance.m_itemData);
                PatchHarpoonStatusEffect(__instance.m_itemData.m_shared.m_attackStatusEffect as SE_Harpooned);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public class Player_OnSpawned_HarpoonStats
        {
            public static void Postfix(Player __instance)
            {
                if (!modEnabled.Value) return;

                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
                __instance.GetInventory().GetAllItems(itemDropNameSpearChitin, items);

                foreach (ItemDrop.ItemData item in items)
                {
                    PatchHarpoonItemData(item);
                    PatchHarpoonStatusEffect(item.m_shared.m_attackStatusEffect as SE_Harpooned);
                }
            }
        }

        private static void PatchHarpoonItemData(ItemDrop.ItemData item)
        {
            item.m_shared.m_maxQuality = Math.Max(Math.Min(maxQuality.Value, 4), 1);

            item.m_shared.m_durabilityPerLevel = Mathf.Clamp(durabilityPerLevel.Value, 50, 500);

            item.m_shared.m_useDurability = !disableDurability.Value;

            item.m_shared.m_useDurabilityDrain = durabilityDrain.Value;

            item.m_shared.m_attack.m_attackStamina = disableStamina.Value ? 0f : attackStamina.Value;
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateOwner))]
        public class Ship_UpdateOwner_ShipPulling
        {
            public static bool Prefix(Ship __instance, ZNetView ___m_nview)
            {
                if (!modEnabled.Value) return true;

                if (harpooned == null || m_nview != ___m_nview) return true;

                m_nview.ClaimOwnership();

                return false;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        public static class Projectile_OnHit_HarpoonStats
        {
            private static void Prefix(Projectile __instance, Collider collider, Character ___m_owner, Vector3 hitPoint, ZNetView ___m_nview, ref int ___s_rayMaskSolids, ref int ___m_statusEffectHash)
            {
                if (!modEnabled.Value) return;

                if (!__instance.name.StartsWith("projectile_chitinharpoon")) return;

                if (!___m_nview.IsOwner()) return;

                if (targetItems.Value)
                {
                    ___s_rayMaskSolids = s_rayMaskSolids;
                }

                if (collider == null || ___m_owner != Player.m_localPlayer) return;

                GameObject colliderHitObject = Projectile.FindHitObject(collider);
                if (colliderHitObject == null) return;

                if (colliderHitObject.GetComponentInParent<FollowPlayer>() != null)
                    return;

                if (targetCreatures.Value && colliderHitObject.TryGetComponent(out Character targetCharacter))
                {
                    if (!targetPulling.Value)
                    {
                        LogInfo($"Vanilla {harpoonedStatusEffect.m_name} status effect on {targetCharacter.m_name}");
                        return;
                    }

                    if (targetCharacter.IsPlayer())
                    {
                        LogInfo($"Vanilla {harpoonedStatusEffect.m_name} status effect on player {targetCharacter.m_name}");
                        return; 
                    }

                    ___m_statusEffectHash = 0;
                }

                if (targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() && colliderHitObject.GetComponent<Rigidbody>().isKinematic == true)
                    colliderHitObject.GetComponent<Rigidbody>().isKinematic = false;

                if (deepLoggingEnabled.Value) LogInfo($"Hit Collider: {collider.name} | hit object: {colliderHitObject.name}" +
                    (colliderHitObject.TryGetComponent(out ZNetView collider_nview) ? $" | Owner:{collider_nview.IsOwner()}" : "") +
                    ((bool)colliderHitObject.GetComponent<Destructible>() ? " : Destructible" : "") +
                    ((bool)colliderHitObject.GetComponent<MineRock>() ? " : MineRock" : "") +
                    ((bool)colliderHitObject.GetComponent<MineRock5>() ? " : MineRock5" : "") +
                    ((bool)colliderHitObject.GetComponent<Rigidbody>() ? " : Rigidbody" : "") +
                    ((bool)colliderHitObject.GetComponent<ResourceRoot>() ? " : ResourceRoot" : "") +
                    ((bool)colliderHitObject.GetComponent<ItemDrop>() ? " : ItemDrop" : "") +
                    ((bool)colliderHitObject.GetComponent<Ship>() ? " : Ship" : "") +
                    ((bool)colliderHitObject.GetComponent<Character>() ? " : Character" : "") +
                    ((bool)colliderHitObject.GetComponent<TreeLog>() ? " : TreeLog" : "") +
                    ((bool)colliderHitObject.GetComponent<TreeBase>() ? " : TreeBase" : "") +
                    ((bool)colliderHitObject.GetComponent<Piece>() ? " : Piece" : "") +
                    ((bool)colliderHitObject.GetComponent<Fish>() ? " : Fish" : "") +
                    ((bool)colliderHitObject.GetComponent<Leviathan>() ? " : Leviathan" : "") +
                    ((bool)colliderHitObject.GetComponent<RandomFlyingBird>() ? " : RandomFlyingBird" : "") +
                    ((bool)colliderHitObject.GetComponent<Location>() ? " : Location" : ""));

                if (targetGround.Value ||
                    targetShip.Value && (bool)colliderHitObject.GetComponent<Ship>() ||
                    targetCreatures.Value && (bool)colliderHitObject.GetComponent<Character>() ||
                    targetTreeLog.Value && (bool)colliderHitObject.GetComponent<TreeLog>() ||
                    targetTreeBase.Value && (bool)colliderHitObject.GetComponent<TreeBase>() ||
                    targetPiece.Value && (bool)colliderHitObject.GetComponent<Piece>() ||
                    targetDestructibles.Value && ((bool)colliderHitObject.GetComponent<Destructible>() || (bool)colliderHitObject.GetComponent<MineRock>() || (bool)colliderHitObject.GetComponent<MineRock5>()) ||
                    targetFish.Value && (bool)colliderHitObject.GetComponent<Fish>() ||
                    targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() || 
                    targetItems.Value && (bool)colliderHitObject.GetComponent<ItemDrop>())
                {
                    float hitDistance = Vector3.Distance(hitPoint, Player.m_localPlayer.transform.position);
                    if (hitDistance > m_maxDistance)
                    {
                        LogInfo("Too far");
                        return;
                    }

                    if (harpooned != null && m_time >= 0.5f)
                    {
                        DestroyHarpooned("Reinstantiate");
                        harpooned = null;
                    }

                    if (harpooned == null)
                    {
                        harpooned = Instantiate(ZNetScene.instance.GetPrefab("vfx_Harpooned"), ___m_owner.transform.position, Quaternion.identity, colliderHitObject.transform);
                        SetHarpooned(Player.m_localPlayer, colliderHitObject, hitPoint, KeyPressPullTo(), collider);
                    }
                }
            }
        }

        public static void SetHarpooned(Player attacker, GameObject hitObject, Vector3 hitPoint, bool pullTo, Collider collider)
        {
            m_attacker = attacker;
            m_time = 0f;
            targetName = "";
            m_breakDistance = breakDistance.Value;
            m_maxDistance = maxDistance.Value;
            m_staminaDrain = 0.1f * drainStamina.Value;
            m_pullSpeed = pullSpeed.Value;
            m_smoothDistance = smoothDistance.Value;
            m_ship = hitObject.GetComponent<Ship>();
            m_lineRenderer = null;
            isPullingTo = false;

            objectRbody = hitObject.GetComponent<Rigidbody>();
            attackerRbody = attacker.GetComponent<Rigidbody>();
            
            objectMass = CalculateHitObjectMass(hitObject);

            m_nview = hitObject.GetComponent<ZNetView>();
            m_character = hitObject.GetComponent<Character>();

            if ((bool)hitObject.GetComponent<RandomFlyingBird>())
            {
                // Bird doesn't have rigidbody but is not stational
                if (deepLoggingEnabled.Value) LogInfo("Pull to bird");
                isPullingTo = true;
            }
            else if (!(bool)objectRbody)
            {
                // If the target has no rigidbody we should pull to it - set stational target hit point
                if (deepLoggingEnabled.Value) LogInfo("Pull to object");
                isPullingTo = true;
            }
            else if (objectRbody.isKinematic)
            {
                // If target has kinematic rigidbody we should pull to it - set stational target hit point
                if (deepLoggingEnabled.Value) LogInfo("Pull to kinematic rigidbody");
                isPullingTo = true;
            }
            else if (pullTo)
            {
                // if the target has rigidbody yet we should pull to it - set target transform
                if (deepLoggingEnabled.Value) LogInfo("Pull to target intentional");
                isPullingTo = true;
            }
            else if (m_character != null && (m_character.IsAttached()))
            {
                // You can't move attached Character
                if (deepLoggingEnabled.Value) LogInfo("Can't pull attached");
                isPullingTo = true;
            }
            else if (m_ship != null && m_ship.HaveControllingPlayer())
            {
                // You can't move already moving ship
                if (deepLoggingEnabled.Value) LogInfo("Can't pull already moving ship");
                isPullingTo = true;
            }
            else if (hitObject.TryGetComponent<Vagon>(out Vagon vagon) && vagon.InUse())
            {
                // You can't move already moving vagon
                if (deepLoggingEnabled.Value) LogInfo("Can't pull already moving vagon");
                isPullingTo = true;
            }
            else if (m_ship == null && objectMass > maxBodyMassToPull.Value)
            {
                if (deepLoggingEnabled.Value) LogInfo($"Can't pull object {objectRbody} with mass {objectMass} more that {maxBodyMassToPull.Value}");
                isPullingTo = true;
            }
            else if (!(bool)hitObject.GetComponent<ZSyncTransform>())
            {
                if (deepLoggingEnabled.Value) LogInfo("Can't pull not netsynchronized object");
                isPullingTo = true;
            }
            else if (m_nview.IsOwner())
            {
                if (deepLoggingEnabled.Value) LogInfo("Move owned");
            }
            else
            {
                // screw it take ownership and move
                if (deepLoggingEnabled.Value) LogInfo("Claim ownership and movе");
                m_nview.ClaimOwnership();
            }

            if ((bool)hitObject.GetComponent<ItemDrop>())
                m_pullSpeed = 100f;

            targetHarpooned = hitObject;
            targetDistance = Vector3.Distance(hitPoint, attacker.transform.position); 

            targetName = GetHarpoonedTargetName(hitObject, collider);

            LogInfo($"Attacker: {attacker.m_name}, target: {hitObject.name}, name: {targetName}, mass: {objectMass}, pull to: {isPullingTo}");

            noUpForce = (bool)hitObject.GetComponent<Ship>();

            if (hitObject.TryGetComponent<Leviathan>(out _))
                m_minDistance = 20f;  // Just in case because colliding with Levi will launch you in the sky
            else if (m_ship != null)
                m_minDistance = minDistanceShip.Value;
            else if (m_character != null)
                m_minDistance = minDistanceCreature.Value;
            else if (hitObject.TryGetComponent<ItemDrop>(out _))
                m_minDistance = minDistanceItem.Value;
            else if (isPullingTo)
                m_minDistance = minDistancePullToTarget.Value;
            else
                m_minDistance = minDistancePullToPlayer.Value;

            LineConnect component = harpooned.GetComponent<LineConnect>();
            if ((bool)component)
            {
                component.SetPeer(m_attacker.GetComponent<ZNetView>());
                m_line = component;
                m_line.m_maxDistance = m_maxDistance;
                m_line.m_dynamicThickness = true;
                m_line.m_minThickness = 0.04f;

                m_lineRenderer = harpooned.GetComponent<LineRenderer>();
                m_lineRenderer.transform.position = hitPoint;
            }

            if (applySlowFall.Value)
                castSlowFall = true;

            HarpoonMessage("$msg_harpoon_harpooned");
        }

        public static bool IsPullingTo()
        {
            return isPullingTo || m_nview == null || !m_nview.IsOwner() || alwaysPullTo.Value;
        }

        public static Rigidbody RBody()
        {
            return IsPullingTo() ? attackerRbody : objectRbody;
        }

        public static float Mass()
        {
            return IsPullingTo() ? attackerRbody.mass + m_attacker.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value : objectMass;
        }

        public static Vector3 TargetPosition()
        {
            return IsPullingTo() ? m_lineRenderer.transform.position : m_attacker.transform.position;
        }

        public static float CalculateHitObjectMass(GameObject hitObject)
        {
            float objectMass = 0f;

            hitObject.GetComponentsInChildren<Rigidbody>().Do(rb => objectMass += rb.mass);

            if (m_ship == null && !hitObject.GetComponent<Vagon>())
                hitObject.GetComponentsInChildren<Container>().Do(cont => objectMass += cont.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value);

            if (hitObject.TryGetComponent(out ItemDrop item))
                objectMass += item.m_itemData.GetWeight() * containerInventoryWeightMassFactor.Value;

            return objectMass;
        }

        public void FixedUpdate()
        {
            if (harpooned != null)
                UpdateHarpoonEffect(Time.fixedDeltaTime);
        }

        public static void UpdateHarpoonEffect(float dt)
        {
            m_time += dt;
            
            if (IsDone())
            {
                DestroyHarpooned("Harpooning ended");
                return;
            }

            float distance = Vector3.Distance(TargetPosition(), RBody().transform.position);

            if (distance < m_minDistance)
            {
                HarpoonMessage("$msg_harpoon_released");
                DestroyHarpooned("Too close");
                return;
            }

            Vector3 forcePoint = m_lineRenderer.transform.position;

            float pullForce;
            if (IsPullingTo())
                pullForce = 1f;
            else if (Mass() > 999)
                pullForce = 1f;
            else if (Mass() <= 1f)
                pullForce = 0.05f * pullForceMultiplier.Value;
            else
                pullForce = (1f - (1f / Mathf.Sqrt(Mass()))) * (RBody().mass / Mass()) * pullForceMultiplier.Value;

            float pullSpeed = (m_attacker.IsAttachedToShip() && (bool)m_ship) ? 10000f : m_pullSpeed;

            float num2 = Pull(RBody(), TargetPosition(), targetDistance, pullSpeed, pullForce, m_smoothDistance, IsPullingTo() ? Vector3.zero : forcePoint, m_character != null, noUpForce, useForce.Value, forcePower.Value);
            m_drainStaminaTimer += dt; float stamina = 0f;
            if (m_drainStaminaTimer > m_staminaDrainInterval && num2 > 0f)
            {
                m_drainStaminaTimer = 0f;
                if (!attachedShipStamina.Value || IsPullingTo() || !m_attacker.IsAttachedToShip())
                {
                    stamina = m_staminaDrain * num2 * (IsPullingTo() ? 10f : Mass() > 999 ? 20f : 10f + 20f * pullForce); // Mathf.Clamp(Mathf.Sqrt(mass), 10f, 30f));
                    m_attacker.UseStamina(stamina);
                }
            }

            if ((bool)m_line)
            {
                m_line.SetSlack((1f - Utils.LerpStep(targetDistance / 2f, targetDistance, distance)) * m_maxLineSlack);
            }

            if (deepLoggingEnabled.Value) LogInfo($"dist: {distance,-5:F3} " +
                                                  $"targetDist: {targetDistance,-5:F3} " +
                                                  $"force: {pullForce,-5:F3} " +
                                                  $"dt: {num2,-5:F3} " +
                                                  $"stam: {stamina,-5:F3} " +
                                                  $"break: {distance - targetDistance,-5:F3} < {m_breakDistance}");

            if (distance - targetDistance > m_breakDistance)
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_linebroke");
                LogInfo("Line broke");
            }

            if (!m_attacker.HaveStamina())
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_released");
                LogInfo("Stamina depleted");
            }

            if (IsDone())
            {
                DestroyHarpooned();
            }
            else
            {
                if (targetPulling.Value && (KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
                {
                    float factorMass = IsPullingTo() ? 4f : 2f;

                    if (KeyPressReleaseHarpoon())
                        targetDistance += factorMass * dt * 2f * pullSpeedMultiplier.Value;
                    else if (KeyPressPullHarpoon())
                        targetDistance -= factorMass * dt * pullSpeedMultiplier.Value;

                    targetDistance = Mathf.Max(targetDistance, m_minDistance + 0.5f);
                }
            }
        }

        public static float Pull(Rigidbody body, Vector3 target, float targetDistance, float speed, float force, float smoothDistance, Vector3 forcePoint, bool checkFreezeRotation = false, bool noUpForce = false, bool useForce = false, float power = 1f)
        {
            Vector3 position = forcePoint != Vector3.zero ? Vector3.Lerp(body.position, forcePoint, 0.5f) : body.position;

            Vector3 vector = target - position;
            float magnitude = vector.magnitude;
            if (magnitude < targetDistance)
                return 0f;

            Vector3 normalized = vector.normalized;
            float num = Mathf.Clamp01((magnitude - targetDistance) / smoothDistance);
            num = (float)Math.Pow(num, power);
            Vector3 b = Vector3.Project(body.velocity, normalized.normalized);
            Vector3 a = normalized.normalized * speed - b;
            if (noUpForce && a.y > 0f)
                a.y = 0f;

            ForceMode mode = useForce ? ForceMode.Impulse : ForceMode.VelocityChange;
            Vector3 force2 = a * num * Mathf.Clamp01(force);

            bool surpassFreezeRotation = checkFreezeRotation && body.freezeRotation;

            if (forcePoint != Vector3.zero)
            {
                if (surpassFreezeRotation)
                {
                    RigidbodyConstraints constraints = body.constraints;
                    body.freezeRotation = false;
                    body.constraints = RigidbodyConstraints.FreezeRotationZ;

                    body.AddForceAtPosition(force2, position, mode);

                    body.constraints = constraints;
                    body.freezeRotation = true;
                }
                else
                    body.AddForceAtPosition(force2, position, mode);
            }
            else
                body.AddForce(force2, mode);

            body.velocity = Vector3.ClampMagnitude(body.velocity, maximumVelocity.Value);

            return num;
        }

        public static void DestroyHarpooned(string logEntry = "")
        {
            if (targetLeviathan.Value && targetHarpooned != null && (bool)targetHarpooned.GetComponent<Leviathan>() && targetHarpooned.GetComponent<Rigidbody>().isKinematic == false)
                targetHarpooned.GetComponent<Rigidbody>().isKinematic = true;

            LogInfo(logEntry);

            m_broken = false;
            m_drainStaminaTimer = 0f;
            m_line = null;
            m_lineRenderer = null;
            m_nview = null;
            m_time = 0f;
            attackerRbody = null;
            objectRbody = null;

            targetHarpooned = null;
            m_ship = null;

            if (harpooned != null)
                ZNetScene.instance.Destroy(harpooned);

            harpooned = null;
        }

        public static void HarpoonMessage(string message)
        {
            if (!messagesEnabled.Value) return;

            if (String.IsNullOrEmpty(message)) return;

            string showMessage = targetName + " " + message;
            if (String.IsNullOrEmpty(targetName))
                showMessage = message;

            showMessage = Localization.instance.Localize(showMessage);

            if (showMessage.Length > 1)
                m_attacker.Message(MessageHud.MessageType.Center, showMessage.ToUpper().Substring(0, 1) + showMessage.Substring(1));
            else
                m_attacker.Message(MessageHud.MessageType.Center, showMessage);
        }

        public static bool IsDone()
        {
            if (m_broken)
                return true;

            if (!IsPullingTo() && (m_nview == null || !m_nview.IsValid()))
                return true;

            if (!m_attacker)
                return true;

            if (m_time > timeBeforeStop.Value && (KeyPressStopHarpoon() || m_attacker.IsBlocking()))
            {
                HarpoonMessage("$msg_harpoon_released");
                return true;
            }

            if (m_attacker.IsDead() || m_attacker.IsTeleporting() || m_attacker.InCutscene() || m_attacker.IsEncumbered())
                return true;

            if (IsPullingTo() && m_attacker.IsAttached())
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            if (!targetBosses.Value && targetHarpooned.TryGetComponent<Humanoid>(out Humanoid human) && human.IsBoss())
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            if (Ship.GetLocalShip() != null && Ship.GetLocalShip() == m_ship)
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            if (IsPullingTo() && !pullUnderWater.Value && TargetPosition().y < ZoneSystem.instance.m_waterLevel)
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            return false;
        }

        private static string GetHarpoonedTargetName(GameObject hitObject, Collider collider)
        {
            if (hitObject.TryGetComponent(out HoverText text))
                return text.m_text;

            if (hitObject.TryGetComponent(out ItemDrop item))
                return item.m_itemData.m_shared.m_name;

            if (hitObject.TryGetComponent<Location>(out _))
                return "$piece_lorestone";

            if (hitObject.TryGetComponent(out ResourceRoot root))
                return root.m_name;

            if (hitObject.TryGetComponent<Ship>(out _))
                return hitObject.GetComponent<Piece>().m_name;

            if (targetMessagesEnabled.Value)
            {
                string defaultName = hitObject.name;
                if (hitObject.TryGetComponent(out Piece piece))
                    return piece.m_name;

                if (hitObject.TryGetComponent(out Destructible destr))
                    defaultName = destr.name;

                if (collider.name.StartsWith("Terrain"))
                    return collider.name;

                if (defaultName.Length > 1)
                    return defaultName.ToUpper().Substring(0, 1) + defaultName.Substring(1, defaultName.IndexOf("(") - 1);

                return defaultName;
            }

            return String.Empty;
        }
    }

}