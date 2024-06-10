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
        const string pluginVersion = "1.2.0";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static ConfigEntry<bool> configLocked;

        internal static ConfigEntry<bool> loggingEnabled;
        internal static ConfigEntry<bool> deepLoggingEnabled;

        internal static ConfigEntry<bool> messagesEnabled;
        internal static ConfigEntry<bool> targetMessagesEnabled;

        internal static ConfigEntry<float> timeBeforeStop;
        internal static ConfigEntry<bool> applySlowFall;
        internal static ConfigEntry<bool> attachedShipStamina;
        internal static ConfigEntry<bool> pullUnderWater;
        internal static ConfigEntry<bool> removeSlowFallWithoutHarpoon;
        internal static ConfigEntry<float> removeSlowFallonGroundThreshold;

        internal static ConfigEntry<bool> targetPulling;
        internal static ConfigEntry<float> pullSpeedMultiplier;
        internal static ConfigEntry<float> maxBodyMassToPull;
        internal static ConfigEntry<float> containerInventoryWeightMassFactor;

        internal static ConfigEntry<bool> targetCreatures;
        internal static ConfigEntry<bool> targetShip;
        internal static ConfigEntry<bool> targetTreeLog;
        internal static ConfigEntry<bool> targetTreeBase;
        internal static ConfigEntry<bool> targetFish;
        internal static ConfigEntry<bool> targetPiece;
        internal static ConfigEntry<bool> targetDestructibles;
        internal static ConfigEntry<bool> targetLeviathan;
        internal static ConfigEntry<bool> targetItems;
        internal static ConfigEntry<bool> targetBosses;
        internal static ConfigEntry<bool> targetGround;

        internal static ConfigEntry<float> breakDistance;
        internal static ConfigEntry<float> maxDistance;
        internal static ConfigEntry<float> drainStamina;
        internal static ConfigEntry<float> minDistanceShip;
        internal static ConfigEntry<float> minDistanceCreature;
        internal static ConfigEntry<float> minDistanceItem;
        internal static ConfigEntry<float> minDistancePullToTarget;
        internal static ConfigEntry<float> minDistancePullToPlayer;

        internal static ConfigEntry<float> pullSpeed;
        internal static ConfigEntry<float> smoothDistance;
        internal static ConfigEntry<float> pullForceMultiplier;
        internal static ConfigEntry<float> forcePower;
        internal static ConfigEntry<bool> useForce;
        internal static ConfigEntry<bool> alwaysPullTo;
        internal static ConfigEntry<float> maximumVelocity;

        internal static ConfigEntry<float> projectileGravityMiltiplier;
        internal static ConfigEntry<float> hitboxSize;
        internal static ConfigEntry<float> projectileVelocityMultiplier;

        internal static ConfigEntry<int> maxQuality;
        internal static ConfigEntry<float> durabilityPerLevel;
        internal static ConfigEntry<bool> disableDurability;
        internal static ConfigEntry<float> durabilityDrain;
        internal static ConfigEntry<float> attackStamina;
        internal static ConfigEntry<bool> disableDamage;
        internal static ConfigEntry<bool> disableStamina;

        internal static ConfigEntry<KeyboardShortcut> shortcutPull;
        internal static ConfigEntry<KeyboardShortcut> shortcutPullTo;
        internal static ConfigEntry<KeyboardShortcut> shortcutRelease;
        internal static ConfigEntry<KeyboardShortcut> shortcutStop;


        internal static int m_slowFallHash = "SlowFall".GetStableHashCode();
        internal const string statusEffectNameHarpooned = "Harpooned";

        internal static HarpoonExtended instance;


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

            Game.isModded = true;
        }

        private void OnDestroy()
        {
            Config.Save();
            instance = null;
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

            maxQuality.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            durabilityPerLevel.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            durabilityDrain.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            attackStamina.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            disableDurability.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            disableDamage.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            disableStamina.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            projectileGravityMiltiplier.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
            projectileVelocityMultiplier.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();


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

            hitboxSize.SettingChanged += (sender, args) => HarpoonItem.PatchHarpoonItemOnConfigChange();
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

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
                //if (harpooned != null)
                    //return;

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

                if (removeSlowFallWithoutHarpoon.Value && HarpoonItem.HarpoonEquipped(player))
                {
                    RemoveSlowFall(seman, "Remove slow fall without harpoon");
                    return;
                }
            }

            private static void Postfix(Player __instance, SEMan ___m_seman)
            {
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

/*        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateOwner))]
        public class Ship_UpdateOwner_ShipPulling
        {
            public static bool Prefix(Ship __instance, ZNetView ___m_nview)
            {
                if (harpooned == null || m_nview != ___m_nview) return true;

                m_nview.ClaimOwnership();

                return false;
            }
        }*/

        /*public static void DestroyHarpooned(string logEntry = "")
        {

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
        }*/

    }

}