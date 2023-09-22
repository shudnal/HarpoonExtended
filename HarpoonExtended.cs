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
        const string pluginVersion = "1.0.1";

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> loggingEnabled;
        private static ConfigEntry<bool> deepLoggingEnabled;

        private static ConfigEntry<bool> messagesEnabled;
        private static ConfigEntry<bool> targetMessagesEnabled;

        private static ConfigEntry<float> timeBeforeStop;
        private static ConfigEntry<bool> applySlowFall;

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
        private static ConfigEntry<float> maxLineSlack;
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

        private static ConfigEntry<float> projectileGravityMiltiplier;
        private static ConfigEntry<float> hitboxSize;
        private static ConfigEntry<float> projectileVelocityMultiplier;

        private static ConfigEntry<int> maxQuality;
        private static ConfigEntry<float> durabilityPerLevel;
        private static ConfigEntry<bool> disableDurability;
        private static ConfigEntry<bool> disableDamage;

        private static ConfigEntry<KeyboardShortcut> shortcutPull;
        private static ConfigEntry<KeyboardShortcut> shortcutPullTo;
        private static ConfigEntry<KeyboardShortcut> shortcutRelease;
        private static ConfigEntry<KeyboardShortcut> shortcutStop;

        internal static int s_rayMaskSolidsAndItem = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle", "item");

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

        public static Rigidbody rbody;

        public static GameObject targetHarpooned;

        public static Vector3 harpoonHitPoint;

        public static string targetName;

        public static bool noUpForce;

        public static Transform target;

        public static Vector3 targetPoint;

        public static ZNetView m_nview;

        public static Ship m_ship;

        public static float mass;

        public static LineRenderer m_lineRenderer;

        public static bool castSlowFall = false;
        public static bool playerDropped = false;
        public static float onGroundTimer = 0f;

        public static SE_Harpooned harpoonedStatusEffect;

        private void Awake()
        {
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), pluginID);

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            _harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value && !data.ToString().IsNullOrWhiteSpace())
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2528, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            
            loggingEnabled = config("Logging", "Enabled logging", defaultValue: false, "Enable logging for debug events. [Not Synced with Server]", false);
            deepLoggingEnabled = config("Logging", "Enabled deep logging", defaultValue: false, "Enable deep logging for debug physics events. [Not Synced with Server]", false);
           
            messagesEnabled = config("Messages", "Enabled harpooning messages", defaultValue: true, "Enable localized notification of current state. [Not Synced with Server]", false);
            targetMessagesEnabled = config("Messages", "Enabled harpooning target message for all objects", defaultValue: false, "Enable unlocalized target name for any object you hit. [Not Synced with Server]", false);

            timeBeforeStop = config("Misc", "Time before harpoon can be dropped", defaultValue: 1.0f, "Time in seconds the harpoon should exists before it can be released. To prevent spam mistakes. [Not Synced with Server]", false);
            applySlowFall = config("Misc", "Apply Feather Fall while harpooning around", defaultValue: true, "Apply Feather Fall while using the harpoon to prevent fall damage");

            targetPulling = config("Pull", "Enable pulling", defaultValue: true, "Enable pulling harpooned target. Alternative action button to release the line. Pulling speed depends on weight difference.");
            pullSpeedMultiplier = config("Pull", "Harpoon line casting and retrieving speed multiplier", defaultValue: 1.0f, "Speed of line casting and retrieving");
            maxBodyMassToPull = config("Pull", "Maximum body mass you can pull", defaultValue: 1000.0f, "Objects with mass more than set will not be pulled but instead pull you to them." +
                                                                                                        "\nLeviathan mass is 1000, Lox 60, Serpent 30. Longship 2000. Karve and raft 1000.");
            containerInventoryWeightMassFactor = config("Pull", "Pulled container inventory weight mass factor", defaultValue: 0.1f, "If pulled object contains inventory, like CargoCrate, the inventory total weight will be multiplied by that factor.");


            targetCreatures = config("Pull targets", "Creatures (override)", defaultValue: true, "Enable pulling creatures. Overrides vanilla behaviour. Restart required after change.");
            targetShip = config("Pull targets", "Ship", defaultValue: true, "Enable pulling ships.");
            targetTreeLog = config("Pull targets", "Tree log", defaultValue: true, "Enable pulling logs.");
            targetTreeBase = config("Pull targets", "Trees", defaultValue: true, "Enable pulling to trees.");
            targetFish = config("Pull targets", "Fish", defaultValue: true, "Enable pulling fish.");
            targetPiece = config("Pull targets", "Buildings", defaultValue: true, "Enable pulling to buildings.");
            targetDestructibles = config("Pull targets", "Destructibles", defaultValue: true, "Enable pulling to destructibles.");
            targetLeviathan = config("Pull targets", "Leviathan", defaultValue: false, "Enable pulling a Leviathan. Use with caution. Can cause fun effect");
            targetItems = config("Pull targets", "Items", defaultValue: true, "Enable pulling a item on the ground. Fish considered as item. Use with caution. Can cause fun effect");
            targetBosses = config("Pull targets", "Bosses", defaultValue: false, "Enable pulling a boss.");
            targetGround = config("Pull targets", "Any target", defaultValue: false, "Track any hitpoint. Every hit collision.");

            breakDistance = config("Stats - Line", "Break distance", defaultValue: 15f, "Line will break if distance between you and target will be more than line length + break distance.");
            maxDistance = config("Stats - Line", "Max distance", defaultValue: 50f, "Max distance. Balanced is 100. Big numbers (>200) will work but may cause unwanted net code effects.");
            maxLineSlack = config("Stats - Line", "Max line slack", defaultValue: 0.3f, "Stamina drain. Better to relog to apply after change.");
            minDistanceShip = config("Stats - Line", "Min distance (Ship)", defaultValue: 5f, "Minimal distance where the line broke to avoid unwanted collisions (Ships)");
            minDistanceCreature = config("Stats - Line", "Min distance (Creature)", defaultValue: 0.5f, "Minimal distance where the line broke to avoid unwanted collisions (living creatures)");
            minDistanceItem = config("Stats - Line", "Min distance (Item)", defaultValue: 0.1f, "Minimal distance where the line broke to avoid unwanted collisions (items)");
            minDistancePullToTarget = config("Stats - Line", "Min distance (pull to target)", defaultValue: 1f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling player to general target)");
            minDistancePullToPlayer = config("Stats - Line", "Min distance (pull to player)", defaultValue: 2f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling general target to player)");
       
            pullSpeed = config("Stats - Force", "Pull speed", defaultValue: 1000f, "[Math] Pull speed of static line. Used in velocity math. No actual need to mess with it.");
            pullForceMultiplier = config("Stats - Force", "Pull force multiplier", defaultValue: 1f, "[Math] Pull force multiplier. Depends on moved body mass. No actual need to mess with it.");
            smoothDistance = config("Stats - Force", "Smooth distance", defaultValue: 2f, "[Math] Makes the applied force smoother. No actual need to mess with it.");
            forcePower = config("Stats - Force", "Force power", defaultValue: 1f, "[Math] Power (exponentiation part) of the actual force. No actual need to mess with it.");
            useForce = config("Stats - Force", "Use force", defaultValue: true, "[Math] If true - pull physics use force applied to moved body. If false - uses velocity calculation. No actual need to mess with it.");
            drainStamina = config("Stats - Force", "Stamina drain", defaultValue: 0.1f, "Stamina drain.");

            projectileGravityMiltiplier = config("Stats - Projectile", "Projectile gravity multiplier", defaultValue: 1.0f, "Multiplier of gravity affecting harpoon projectile");
            hitboxSize = config("Stats - Projectile", "Hitbox size", 0.0f, "Hitbox size. 0.0 min - 0.5 max. You can try to change it if you have difficulties with aiming small targets");
            projectileVelocityMultiplier = config("Stats - Projectile", "Velocity multiplier", 1.0f, "Basically speed of initial harpoon flight");

            maxQuality = config("Stats - Item", "Max quality", defaultValue: 4, "Maximum quality level");
            durabilityPerLevel = config("Stats - Item", "Durability per level", defaultValue: 100f, "Durability added per level");
            disableDurability = config("Stats - Item", "Disable durability", defaultValue: false, "Make harpoon to not use durability");
            disableDamage = config("Stats - Item", "Disable damage", defaultValue: false, "Make harpoon to deal no damage. Handy to ride a deathsquito without killing it. Or even birds.");

            shortcutPull = config("Shortcuts", "Pull", defaultValue: new KeyboardShortcut(KeyCode.T), "Pull target closer if applicable [Not Synced with Server]", false);
            shortcutPullTo = config("Shortcuts", "Pull To Target mode", defaultValue: new KeyboardShortcut(KeyCode.LeftShift), "Hold why harpoon is flying to make you always pull to target [Not Synced with Server]", false);
            shortcutRelease = config("Shortcuts", "Release", defaultValue: new KeyboardShortcut(KeyCode.T, new KeyCode[1] { KeyCode.LeftControl }), "Release line [Not Synced with Server]", false);
            shortcutStop = config("Shortcuts", "Stop harpooning", defaultValue: new KeyboardShortcut(KeyCode.T, new KeyCode[2] { KeyCode.LeftShift, KeyCode.LeftControl }), "Stop harpooning [Not Synced with Server]", false);
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

                if (!___m_character.IsOwner())
                    ___m_character.m_nview.ClaimOwnership();

                if ((KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
                {
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

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        public static class Player_Update_Taxi
        {
            private static void Postfix(Player __instance, ref SEMan ___m_seman)
            {
                if (!modEnabled.Value)
                    return;

                if (Player.m_localPlayer != __instance)
                    return;

                if (castSlowFall)
                {
                    if (!___m_seman.HaveStatusEffect("SlowFall"))
                    {
                        ___m_seman.AddStatusEffect("SlowFall".GetStableHashCode());
                        LogInfo("Cast slow fall");
                    }
                    castSlowFall = false; 
                    onGroundTimer = 0f;
                }

                if (harpooned == null && playerDropped && __instance.IsOnGround())
                {
                    onGroundTimer += Time.deltaTime;
                    if (onGroundTimer >= 2f)
                    {
                        playerDropped = false;
                        if (___m_seman.HaveStatusEffect("SlowFall"))
                            ___m_seman.RemoveStatusEffect("SlowFall".GetStableHashCode(), true);
                        LogInfo("Remove slow fall");
                    }
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
            private static void Postfix(ObjectDB __instance, ref List<StatusEffect>  ___m_StatusEffects, ref List<Recipe> ___m_recipes)
            {
                if (!modEnabled.Value) return;

                foreach (StatusEffect statusEffect in ___m_StatusEffects)
                {
                    if (statusEffect.name == "Harpooned" && statusEffect is SE_Harpooned)
                    {
                        harpoonedStatusEffect = statusEffect as SE_Harpooned;
                        PatchHarpoonStatusEffect(harpoonedStatusEffect);
                        break;
                    }
                }

                for (int index = 0; index < ___m_recipes.Count - 1; index++)
                {
                    if (___m_recipes[index].m_item == null)
                        continue;

                    if (ObjectDB.instance.m_recipes[index].m_item.m_itemData.m_shared.m_name == "$item_spear_chitin")
                    {
                        PatchHarpoonItemData(___m_recipes[index].m_item.m_itemData);

                        foreach (Piece.Requirement resource in ___m_recipes[index].m_resources)
                            resource.m_amountPerLevel = !(resource.m_resItem.m_itemData.m_shared.m_name == "$item_chitin") ? 0 : 20;

                        break;
                    }
                }
            }
        }

        private static void PatchHarpoonStatusEffect(SE_Harpooned statusEffect)
        {
            if (statusEffect == null) return;

            statusEffect.m_breakDistance = breakDistance.Value;
            statusEffect.m_maxDistance  = maxDistance.Value;
            statusEffect.m_staminaDrain = drainStamina.Value;
            statusEffect.m_pullSpeed = pullSpeed.Value;
            statusEffect.m_smoothDistance = smoothDistance.Value;
            statusEffect.m_forcePower = forcePower.Value;
            statusEffect.m_maxLineSlack = maxLineSlack.Value;
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
                    ___s_rayMaskSolids = s_rayMaskSolidsAndItem;
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

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        public static class ItemDrop_Awake_HarpoonStats
        {
            private static void Postfix(ref ItemDrop __instance)
            {
                if (!modEnabled.Value) return;

                if (__instance.m_itemData.m_shared.m_name != "$item_spear_chitin") return;

                PatchHarpoonItemData(__instance.m_itemData);
                PatchHarpoonStatusEffect(__instance.m_itemData.m_shared.m_attackStatusEffect as SE_Harpooned);
            }
        }
        
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.SlowUpdate))]
        private static class ItemDrop_SlowUpdate_HarpoonStats
        {
            private static void Postfix(ref ItemDrop __instance)
            {
                if (!modEnabled.Value) return;

                if (__instance.m_itemData.m_shared.m_name != "$item_spear_chitin") return;

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
                __instance.GetInventory().GetAllItems("$item_spear_chitin", items);
                
                foreach (ItemDrop.ItemData item in items)
                {
                    if (item.m_shared.m_name != "$item_spear_chitin") return;

                    PatchHarpoonItemData(item);
                    PatchHarpoonStatusEffect(item.m_shared.m_attackStatusEffect as SE_Harpooned);
                }
            }
        }

        private static void PatchHarpoonItemData(ItemDrop.ItemData item)
        {
            if (item.m_shared.m_name != "$item_spear_chitin") return;

            item.m_shared.m_maxQuality = Math.Max(Math.Min(maxQuality.Value, 4), 1);

            item.m_shared.m_durabilityPerLevel = Mathf.Clamp(durabilityPerLevel.Value, 50, 500);

            item.m_shared.m_useDurability = !disableDurability.Value;

            if (targetPulling.Value && targetCreatures.Value)
                item.m_shared.m_attackStatusEffect = null;
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
            private static void Prefix(Projectile __instance, Collider collider, Character ___m_owner, Vector3 hitPoint)
            {
                if (!modEnabled.Value) return;

                if (!__instance.name.StartsWith("projectile_chitinharpoon")) return;

                if (collider == null || ___m_owner != Player.m_localPlayer) return;

                GameObject colliderHitObject = Projectile.FindHitObject(collider);
                if (colliderHitObject == null) return;

                if (targetCreatures.Value && colliderHitObject.TryGetComponent<Character>(out Character targetCharacter) && (targetCharacter.IsPlayer()))
                {
                    if (targetCharacter.GetSEMan().HaveStatusEffect(harpoonedStatusEffect.NameHash()))
                        return;
                    
                    StatusEffect statusEffect = targetCharacter.GetSEMan().AddStatusEffect(harpoonedStatusEffect);
                    statusEffect.SetAttacker(___m_owner);
                    LogInfo($"Vanilla {harpoonedStatusEffect.m_name} status effect on {targetCharacter.m_name}");
                    return;
                }

                if (targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() && colliderHitObject.GetComponent<Rigidbody>().isKinematic == true)
                    colliderHitObject.GetComponent<Rigidbody>().isKinematic = false;

                if (deepLoggingEnabled.Value) LogInfo($"Hit Collider: {collider.name} | hit object: {colliderHitObject.name} | " +
                    (colliderHitObject.TryGetComponent<ZNetView>(out ZNetView collider_nview) ? $"Owner:{collider_nview.IsOwner()}" : "") +
                    ((bool)colliderHitObject.GetComponent<Destructible>() ? " : Destructible" : "") +
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
                    targetDestructibles.Value && (bool)colliderHitObject.GetComponent<Destructible>() ||
                    targetFish.Value && (bool)colliderHitObject.GetComponent<Fish>() ||
                    targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() || 
                    targetItems.Value && (bool)colliderHitObject.GetComponent<ItemDrop>())
                {
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

        public static void SetHarpoonPullTo(Player objectToPull, GameObject pullToObjectTransform)
        {
            rbody = objectToPull.GetComponent<Rigidbody>();
            target = pullToObjectTransform.transform;
        }

        public static float CalculateHitObjectMass(GameObject hitObject)
        {
            float objectMass = 0f;

            hitObject.GetComponentsInChildren<Rigidbody>().Do(rb => objectMass += rb.mass);

            if (!hitObject.GetComponent<Ship>() && !hitObject.GetComponent<Vagon>())
                hitObject.GetComponentsInChildren<Container>().Do(cont => objectMass += cont.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value);
            
            if (hitObject.TryGetComponent<ItemDrop>(out ItemDrop item))
                objectMass += item.m_itemData.GetWeight() * containerInventoryWeightMassFactor.Value;

            return objectMass;
        }

        public static void SetHarpooned(Player attacker, GameObject hitObject, Vector3 hitPoint, bool pullTo, Collider collider)
        {
            m_attacker = attacker;
            m_time = 0f;
            harpoonHitPoint = hitPoint;
            targetName = "";
            m_breakDistance = breakDistance.Value;
            m_maxDistance = maxDistance.Value;
            m_staminaDrain = drainStamina.Value;
            m_pullSpeed = pullSpeed.Value;
            m_smoothDistance = smoothDistance.Value;
            m_maxLineSlack = maxLineSlack.Value;
            targetPoint = Vector3.zero;
            m_ship = hitObject.GetComponent<Ship>();
            m_lineRenderer = null;

            float hitObjectMass = CalculateHitObjectMass(hitObject);

            m_nview = hitObject.GetComponent<ZNetView>();
            Character m_character = hitObject.GetComponent<Character>();

            if ((bool)hitObject.GetComponent<RandomFlyingBird>())
            {
                // Bird doesn't have rigidbody but is not stational
                if (deepLoggingEnabled.Value) LogInfo("Pull to bird");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (!hitObject.TryGetComponent<Rigidbody>(out Rigidbody objectRbody))
            {
                // If the target has no rigidbody we should pull to it - set stational target hit point
                if (deepLoggingEnabled.Value) LogInfo("Pull to object");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
                targetPoint = hitPoint;
            }
            else if (pullTo)
            {
                // if the target has rigidbody yet we should pull to it - set target transform
                if (deepLoggingEnabled.Value) LogInfo("Pull to target intentional");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (m_character != null && (m_character.IsAttached()))
            {
                // You can't move attached Character
                if (deepLoggingEnabled.Value) LogInfo("Can't pull attached");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (m_ship != null && m_ship.HaveControllingPlayer())
            {
                // You can't move already moving ship
                if (deepLoggingEnabled.Value) LogInfo("Can't pull owned moving ship");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (hitObject.TryGetComponent<Vagon>(out Vagon vagon) && vagon.InUse())
            {
                // You can't move already moving vagon
                if (deepLoggingEnabled.Value) LogInfo("Can't pull owned moving vagon");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (m_ship == null && hitObjectMass > maxBodyMassToPull.Value)
            {
                if (deepLoggingEnabled.Value) LogInfo($"Can't pull object {objectRbody} with mass {hitObjectMass} more that {maxBodyMassToPull.Value}");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (!(bool)hitObject.GetComponent<ZSyncTransform>())
            {
                if (deepLoggingEnabled.Value) LogInfo("Can't pull not netsynchronized object");
                SetHarpoonPullTo(objectToPull: attacker, pullToObjectTransform: hitObject);
            }
            else if (m_nview.IsOwner())
            {
                if (deepLoggingEnabled.Value) LogInfo("Move owned");
                rbody = objectRbody;
                target = attacker.transform;
            }
            else
            {
                // screw it take ownership and move
                if (deepLoggingEnabled.Value) LogInfo("Claim ownership and movе");
                m_nview.ClaimOwnership();
                rbody = objectRbody;
                target = attacker.transform;
            }

            if ((bool)hitObject.GetComponent<ItemDrop>())
                m_pullSpeed = 100f;

            targetHarpooned = hitObject;

            mass = IsPullingTo() ? rbody.mass : hitObjectMass;

            targetName = GetHarpoonedTargetName(hitObject, collider);

            LogInfo($"Attacker: {attacker.m_name}, target: {hitObject.name}, name: {targetName}, mass: {hitObjectMass}, pull to: {IsPullingTo()}");

            targetDistance = Vector3.Distance(PullPosition(), rbody.transform.position);
            if (targetDistance > m_maxDistance)
            {
                HarpoonMessage("$msg_harpoon_targettoofar");
                m_broken = true;
                LogInfo("Too far");
                return;
            }

            noUpForce = (bool)hitObject.GetComponent<Ship>();

            if (hitObject.TryGetComponent<Leviathan>(out _))
                m_minDistance = 20f;  // Just in case because colliding with Levi will launch you in the sky
            else if (m_ship != null)
                m_minDistance = minDistanceShip.Value;
            else if (hitObject.TryGetComponent<Character>(out _))
                m_minDistance = minDistanceCreature.Value;
            else if (hitObject.TryGetComponent<ItemDrop>(out _))
                m_minDistance = minDistanceItem.Value;
            else if (IsPullingTo())
                m_minDistance = minDistancePullToTarget.Value;
            else
                m_minDistance = minDistancePullToPlayer.Value;

            LineConnect component = harpooned.GetComponent<LineConnect>();
            if ((bool)component)
            {
                component.SetPeer(m_attacker.GetComponent<ZNetView>());
                m_line = component;
                m_line.m_maxDistance = m_maxDistance;

                m_lineRenderer = harpooned.GetComponent<LineRenderer>();
                m_lineRenderer.transform.position = harpoonHitPoint;
            }

            if (applySlowFall.Value)
                castSlowFall = true;

            HarpoonMessage("$msg_harpoon_harpooned");
        }

        public static Vector3 PullPosition()
        {
            return IsPullingTo() ? targetPoint : target.position;
        }

        public static bool IsPullingTo()
        {
            return targetPoint != Vector3.zero;
        }

        public void FixedUpdate()
        {
            if (harpooned != null) 
                UpdateStatusEffect(Time.fixedDeltaTime);
        }

        public static void UpdateStatusEffect(float dt)
        {
            m_time += dt;
            
            if (IsDone())
            {
                DestroyHarpooned("Harpooning ended");
                return;
            }

            float distance = Vector3.Distance(PullPosition(), rbody.transform.position);

            if (distance < m_minDistance)
            {
                HarpoonMessage("$msg_harpoon_released");
                DestroyHarpooned("Too close");
                return;
            }

            Vector3 forcePoint = m_lineRenderer.transform.position;

            float pullForce = (IsPullingTo() ? 1f : (1f - (1f / Mathf.Sqrt(mass + 1))) * rbody.mass / mass) * pullForceMultiplier.Value;

            float num2 = Pull(rbody, PullPosition(), targetDistance, m_pullSpeed, pullForce, m_smoothDistance, IsPullingTo() ? Vector3.zero : forcePoint, noUpForce, useForce.Value, forcePower.Value);
            m_drainStaminaTimer += dt; float stamina = 0f;
            if (m_drainStaminaTimer > m_staminaDrainInterval && num2 > 0f)
            {
                stamina = m_staminaDrain * num2 * (IsPullingTo() ? 10f : Mathf.Sqrt(mass)); // Mathf.Clamp(Mathf.Sqrt(mass), 10f, 30f));
                m_attacker.UseStamina(stamina);
                m_drainStaminaTimer = 0f;
            }

            if ((bool)m_line)
            {
                m_line.SetSlack((1f - Utils.LerpStep(targetDistance / 2f, targetDistance, distance)) * m_maxLineSlack);
            }

            if (deepLoggingEnabled.Value) LogInfo($"dist: {distance} targetDist: {targetDistance} force: {pullForce} dt: {num2} stam: {stamina} break: {distance - targetDistance} < {m_breakDistance}");

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

        public static float Pull(Rigidbody body, Vector3 target, float targetDistance, float speed, float force, float smoothDistance, Vector3 forcePoint, bool noUpForce = false, bool useForce = false, float power = 1f)
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

            if (forcePoint != Vector3.zero)
                body.AddForceAtPosition(force2, position, mode);
            else
                body.AddForce(force2, mode);

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
            rbody = null;
            targetHarpooned = null;
            target = null;
            m_ship = null;

            if (harpooned != null)
                ZNetScene.instance.Destroy(harpooned);

            harpooned = null;

            playerDropped = true;
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

            return false;
        }

        private static string GetHarpoonedTargetName(GameObject hitObject, Collider collider)
        {
            if (hitObject.TryGetComponent<HoverText>(out HoverText text))
                return text.m_text;

            if (hitObject.TryGetComponent<ItemDrop>(out ItemDrop item))
                return item.m_itemData.m_shared.m_name;

            if (hitObject.TryGetComponent<Location>(out _))
                return "$piece_lorestone";

            if (hitObject.TryGetComponent<ResourceRoot>(out ResourceRoot root))
                return root.m_name;

            if (hitObject.TryGetComponent<Ship>(out _))
                return hitObject.GetComponent<Piece>().m_name;

            if (targetMessagesEnabled.Value)
            {
                string defaultName = hitObject.name;
                if (hitObject.TryGetComponent<Piece>(out Piece piece))
                    return piece.m_name;

                if (hitObject.TryGetComponent<Destructible>(out Destructible destr))
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