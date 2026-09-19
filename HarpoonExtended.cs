using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using ConditionalConfigSync;
using HarmonyLib;
using UnityEngine;

namespace HarpoonExtended
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInDependency("_shudnal.ConditionalConfigSync", "1.0.5")]
    public partial class HarpoonExtended : BaseUnityPlugin
    {
        internal const string pluginID = "shudnal.HarpoonExtended";
        internal const string pluginName = "Harpoon Extended";
        internal const string pluginVersion = "1.2.0";

        private readonly Harmony harmony = new Harmony(pluginID);
        internal static readonly ConfigSync configSync = new ConfigSync(pluginID)
        {
            DisplayName = pluginName,
            CurrentVersion = pluginVersion,
            MinimumRequiredVersion = pluginVersion,
            ModRequired = true
        };

        public static ConfigEntry<bool> configLocked;
        public static ConfigEntry<bool> loggingEnabled;
        public static ConfigEntry<bool> deepLoggingEnabled;
        public static ConfigEntry<bool> messagesEnabled;
        public static ConfigEntry<bool> targetMessagesEnabled;
        public static ConfigEntry<float> timeBeforeStop;
        public static ConfigEntry<bool> applySlowFall;
        public static ConfigEntry<bool> attachedShipStamina;
        public static ConfigEntry<bool> pullUnderWater;
        public static ConfigEntry<bool> removeSlowFallWithoutHarpoon;
        public static ConfigEntry<float> removeSlowFallonGroundThreshold;
        public static ConfigEntry<float> slowFallSpeed;
        public static ConfigEntry<float> slowFallDamageMultiplier;
        public static ConfigEntry<bool> targetPulling;
        public static ConfigEntry<float> pullSpeedMultiplier;
        public static ConfigEntry<float> maxBodyMassToPull;
        public static ConfigEntry<float> containerInventoryWeightMassFactor;
        public static ConfigEntry<bool> targetCreatures;
        public static ConfigEntry<bool> targetShip;
        public static ConfigEntry<bool> targetTreeLog;
        public static ConfigEntry<bool> targetTreeBase;
        public static ConfigEntry<bool> targetFish;
        public static ConfigEntry<bool> targetPiece;
        public static ConfigEntry<bool> targetDestructibles;
        public static ConfigEntry<bool> targetLeviathan;
        public static ConfigEntry<bool> targetItems;
        public static ConfigEntry<bool> targetBosses;
        public static ConfigEntry<bool> targetGround;
        public static ConfigEntry<string> targetWhitelist;
        public static ConfigEntry<string> targetBlacklist;
        public static ConfigEntry<float> breakDistance;
        public static ConfigEntry<float> maxDistance;
        public static ConfigEntry<float> drainStamina;
        public static ConfigEntry<float> minDistanceShip;
        public static ConfigEntry<float> minDistanceCreature;
        public static ConfigEntry<float> minDistanceItem;
        public static ConfigEntry<float> minDistancePullToTarget;
        public static ConfigEntry<float> minDistancePullToPlayer;
        public static ConfigEntry<float> pullSpeed;
        public static ConfigEntry<float> smoothDistance;
        public static ConfigEntry<float> pullForceMultiplier;
        public static ConfigEntry<float> forcePower;
        public static ConfigEntry<bool> useForce;
        public static ConfigEntry<bool> alwaysPullTo;
        public static ConfigEntry<float> maximumVelocity;
        public static ConfigEntry<float> projectileGravityMiltiplier;
        public static ConfigEntry<float> hitboxSize;
        public static ConfigEntry<float> projectileVelocityMultiplier;
        public static ConfigEntry<int> maxQuality;
        public static ConfigEntry<float> durabilityPerLevel;
        public static ConfigEntry<bool> disableDurability;
        public static ConfigEntry<float> durabilityDrain;
        public static ConfigEntry<float> attackStamina;
        public static ConfigEntry<bool> disableDamage;
        public static ConfigEntry<bool> disableStamina;
        public static ConfigEntry<KeyboardShortcut> shortcutPull;
        public static ConfigEntry<KeyboardShortcut> shortcutPullTo;
        public static ConfigEntry<KeyboardShortcut> shortcutRelease;
        public static ConfigEntry<KeyboardShortcut> shortcutStop;

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
        public static bool castSlowFall;
        public static bool slowFallCasted;
        public static float onGroundTimer;
        public static SE_Harpooned harpoonedStatusEffect;

        private void Awake()
        {
            instance = this;
            ConfigInit();
            configSync.AddLockingConfigEntry(configLocked);
            harmony.PatchAll();
            Game.isModded = true;
        }

        private void OnDestroy()
        {
            DestroyHarpooned("Plugin destroyed");
            ResetSlowFallState(true);
            Config.Save();
            harmony.UnpatchSelf();
            instance = null;
        }

        public static void LogInfo(object data)
        {
            if (instance != null && loggingEnabled != null && loggingEnabled.Value && data != null && !string.IsNullOrWhiteSpace(data.ToString()))
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("1 - General", "NexusID", 2528, "Nexus mod ID for updates", false);
            configLocked = config("1 - General", "Lock Configuration", true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("1 - General", "Logging enabled", false, "Enable logging for debug events. [Not Synced with Server]", false);

            targetCreatures = config("2 - Targets", "Creatures (override)", true, "Enable pulling creatures. Overrides vanilla behaviour. Restart required after change.");
            targetShip = config("2 - Targets", "Ship", true, "Enable pulling ships.");
            targetTreeLog = config("2 - Targets", "Tree log", true, "Enable pulling logs.");
            targetTreeBase = config("2 - Targets", "Trees", true, "Enable pulling to trees.");
            targetFish = config("2 - Targets", "Fish", true, "Enable pulling fish.");
            targetPiece = config("2 - Targets", "Buildings", true, "Enable pulling to buildings.");
            targetDestructibles = config("2 - Targets", "Destructibles", true, "Enable pulling to destructibles.");
            targetLeviathan = config("2 - Targets", "Leviathan", false, "Enable pulling a Leviathan. Use with caution. Can cause deadly effect");
            targetItems = config("2 - Targets", "Items", true, "Enable pulling an items. Fish considered as item.");
            targetBosses = config("2 - Targets", "Bosses", false, "Enable pulling a boss.");
            targetGround = config("2 - Targets", "Any target", false, "Track any hitpoint. Every hit collision. Terrain included.");
            targetWhitelist = config("2 - Targets", "Prefab whitelist", "", "Comma-separated prefab names allowed to attach a rope. Empty means no additional restriction. Does not bypass other target rules. Hits and damage still occur.");
            targetBlacklist = config("2 - Targets", "Prefab blacklist", "", "Comma-separated prefab names forbidden to attach a rope. Takes priority over the whitelist. Hits and damage still occur.");
            targetWhitelist.SettingChanged += OnTargetFilterChanged;
            targetBlacklist.SettingChanged += OnTargetFilterChanged;
            RebuildTargetFilters();

            messagesEnabled = config("6 - Misc", "Enabled harpooning messages", true, "Enable localized notification of current state. [Not Synced with Server]", false);
            timeBeforeStop = config("6 - Misc", "Time before harpoon can be dropped", 1f, "Time in seconds the harpoon should exists before it can be released. To prevent spam mistakes. [Not Synced with Server]", false);
            applySlowFall = config("6 - Misc", "Apply Feather Fall while harpooning around", true, "Apply Feather Fall while using the harpoon to prevent fall damage");
            drainStamina = config("6 - Misc", "Stamina drain multiplier", 1f, "Stamina drain for target pulling.");
            attachedShipStamina = config("6 - Misc", "No stamina usage while attached to ship", true, "Disable stamina usage while attached to ship");
            pullUnderWater = config("6 - Misc", "Pull to underwater", true, "Pull to underwater terrain.");
            removeSlowFallWithoutHarpoon = config("6 - Misc", "Remove Feather Fall without harpoon", false, "Remove Feather Fall if harpoon is not equipped.");
            removeSlowFallonGroundThreshold = config("6 - Misc", "Remove Feather Fall after seconds on ground", 2f, "Remove Feather Fall if a player stays without harpoon line on the ground for set amount of seconds.");
            slowFallSpeed = config("6 - Misc", "Feather Fall maximum fall speed", 7f, "Maximum downward speed in meters per second. Zero disables the speed limit. Applied only when the harpoon's own Feather Fall effect is added.");
            slowFallDamageMultiplier = config("6 - Misc", "Feather Fall damage multiplier", 0f, new ConfigDescription("Fall damage multiplier: 0 prevents base fall damage, 0.5 halves it, 1 leaves it unchanged. Other status effects still contribute normally. Applied only when the harpoon's own Feather Fall effect is added.", new AcceptableValueRange<float>(0f, 1f)));

            targetPulling = config("3 - Pull", "Enable pulling", true, "Enable active pulling harpooned target or yourself. Hold Use button to retrieve line or Crouch + Use buttons to cast line.");
            pullSpeedMultiplier = config("3 - Pull", "Harpoon line casting and retrieving speed multiplier", 1f, "Speed of line casting and retrieving");
            maxBodyMassToPull = config("3 - Pull", "Maximum mass you can pull", 1000f, "Objects with mass more than set will not be pulled but instead you will be pulled to them.\nLeviathan mass is 1000, Lox 60, Serpent 30. Ships excluded from this restriction.\nContainers and items weight depends on \"Pulled container inventory weight mass factor\"");
            containerInventoryWeightMassFactor = config("3 - Pull", "Pulled container inventory weight mass factor", 0.1f, "If pulled object contains inventory, like CargoCrate, the inventory total weight will be multiplied by that factor.\nThis calculation also applies to pulling items but this effect is mostly negligible except some heavy stack");

            breakDistance = config("4 - Line", "Break distance", 15f, "Line will break if distance between you and target will be more than target line length + break distance.");
            maxDistance = config("4 - Line", "Max distance", 50f, "Max distance. Balanced is 100. Big numbers (>200) will work but may cause unwanted net code effects.");
            minDistanceShip = config("4 - Line", "Min distance (Ship)", 5f, "Minimal distance where the line broke to avoid unwanted collisions (Ships)");
            minDistanceCreature = config("4 - Line", "Min distance (Creature)", 0.5f, "Minimal distance where the line broke to avoid unwanted collisions (living creatures)");
            minDistanceItem = config("4 - Line", "Min distance (Item)", 0.1f, "Minimal distance where the line broke to avoid unwanted collisions (items)");
            minDistancePullToTarget = config("4 - Line", "Min distance (pull to target)", 1f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling player to general target)");
            minDistancePullToPlayer = config("4 - Line", "Min distance (pull to player)", 2f, "Minimal distance where the line broke to avoid unwanted collisions (When pulling general target to player)");

            maxQuality = config("5 - Item", "Max quality", 4, "Maximum quality level");
            durabilityPerLevel = config("5 - Item", "Durability per level", 100f, "Durability added per level");
            durabilityDrain = config("5 - Item", "Durability drain on attack", 1f, "Durability drain on usage");
            attackStamina = config("5 - Item", "Stamina drain on attack", 15f, "Stamina drain on usage");
            disableDurability = config("5 - Item", "Disable harpoon durability usage", false, "Make harpoon to not use durability. Restart required after change.");
            disableDamage = config("5 - Item", "Disable harpoon damage", false, "Make harpoon to deal no damage. Handy to ride a deathsquito without killing it. Or even birds. Restart required after change.");
            disableStamina = config("5 - Item", "Disable harpoon stamina usage", false, "Make harpoon to not use stamina. Restart required after change.");
            projectileGravityMiltiplier = config("5 - Item", "Projectile gravity multiplier", 1f, "Multiplier of gravity affecting harpoon projectile");
            projectileVelocityMultiplier = config("5 - Item", "Projectile velocity multiplier", 1f, "Basically speed of initial harpoon flight");

            shortcutPull = config("7 - Shortcuts", "Pull", new KeyboardShortcut(KeyCode.T), "Pull target closer if applicable [Not Synced with Server]", false);
            shortcutPullTo = config("7 - Shortcuts", "Pull To Target mode", new KeyboardShortcut(KeyCode.LeftShift), "Hold why harpoon is flying to make you always pull to target [Not Synced with Server]", false);
            shortcutRelease = config("7 - Shortcuts", "Release", new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl), "Release line [Not Synced with Server]", false);
            shortcutStop = config("7 - Shortcuts", "Stop harpooning", new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift, KeyCode.LeftControl), "Stop harpooning [Not Synced with Server]", false);

            pullSpeed = config("8 - Debug", "Pull speed", 1000f, "[Math] Pull speed of static line. Used in velocity math. No actual need to mess with it.");
            pullForceMultiplier = config("8 - Debug", "Pull force multiplier", 1f, "[Math] Pull force multiplier. Depends on moved body mass. No actual need to mess with it.");
            smoothDistance = config("8 - Debug", "Smooth distance", 2f, "[Math] Makes the applied force smoother. No actual need to mess with it.");
            forcePower = config("8 - Debug", "Force power", 1f, "[Math] Power (exponentiation part) of the actual force. No actual need to mess with it.");
            useForce = config("8 - Debug", "Use force", true, "[Math] If true - pull physics use force applied to moved body. If false - uses velocity calculation. No actual need to mess with it.");
            targetMessagesEnabled = config("8 - Debug", "Enabled harpooning target message for all objects", false, "Enable unlocalized target name for any object you hit. [Not Synced with Server]", false);
            deepLoggingEnabled = config("8 - Debug", "Logging deep stats", false, "Enable deep logging to debug physics events. [Not Synced with Server]", false);
            hitboxSize = config("8 - Debug", "Hitbox size", 0f, "Hitbox size. 0.0 min - 0.5 max. You can try to change it if you have difficulties with aiming small targets");
            alwaysPullTo = config("8 - Debug", "Always pull to", false, "Always pull to target regardress hotkey");
            maximumVelocity = config("8 - Debug", "Maximum velocity", 10f, "Maximum velocity imparted to player rigidbody by harpoon pulling");
        }

        private ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> entry = Config.Bind(group, name, defaultValue, description);
            configSync.AddConfigEntry(entry, synchronizedSetting ? ConfigSyncMode.AlwaysServerControlled : ConfigSyncMode.AlwaysClientControlled);
            return entry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true)
            => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private static bool KeyPressStopHarpoon() => shortcutStop.Value.IsDown() || ZInput.GetButton("Block") || ZInput.GetButton("JoyBlock");
        private static bool KeyPressPullHarpoon() => ZInput.GetButton("Use") || ZInput.GetButton("JoyUse") || shortcutPull.Value.IsPressed();
        private static bool KeyPressReleaseHarpoon() => (KeyPressPullHarpoon() && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch"))) || shortcutRelease.Value.IsPressed();
        private static bool KeyPressPullTo() => ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace") || shortcutPullTo.Value.IsPressed();

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
        public static class SE_Harpooned_UpdateStatusEffect_HarpoonPull
        {
            [HarmonyPriority(Priority.First)]
            private static void Postfix(SE_Harpooned __instance, float dt, Character ___m_attacker, Character ___m_character, ref float ___m_baseDistance)
            {
                if (!targetPulling.Value || ___m_attacker == null || ___m_attacker != Player.m_localPlayer || ___m_character == null || ___m_character.IsPlayer())
                    return;
                if ((KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
                {
                    if (___m_character.m_nview == null || !___m_character.m_nview.IsValid())
                        return;
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
                if (__instance == null || ___m_attacker == null || ___m_character == null || ___m_attacker != Player.m_localPlayer || !targetPulling.Value)
                    return true;
                if (!KeyPressStopHarpoon())
                    return true;
                ___m_attacker.Message(MessageHud.MessageType.Center, ___m_character.m_name + Localization.instance.Localize(" $msg_harpoon_released"));
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateCrouch))]
        public static class Player_UpdateCrouch_DisableCrouchOnHarpooning
        {
            private static void Prefix(Player __instance, ref bool ___m_crouchToggled)
            {
                if (Player.m_localPlayer == __instance && harpoonActive && ___m_crouchToggled)
                    ___m_crouchToggled = false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        public static class Player_TeleportTo_HarpoonStat
        {
            private static void Postfix(Player __instance)
            {
                if (harpoonActive && m_attacker == __instance && __instance.IsTeleporting())
                    DestroyHarpooned("Teleport initiated");
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_HarpoonStat
        {
            public static void PatchHarpoonStatusEffectAndRecipe(ObjectDB db)
            {
                HarpoonSlowFall.Register(db);
                GameObject prefab = db.GetItemPrefab(prefabNameSpearChitin);
                if (prefab == null || !prefab.TryGetComponent(out ItemDrop item))
                    return;
                PatchHarpoonItemData(item.m_itemData);
                foreach (StatusEffect effect in db.m_StatusEffects)
                {
                    if (effect != null && effect.name == statusEffectNameHarpooned && effect is SE_Harpooned status)
                    {
                        harpoonedStatusEffect = status;
                        PatchHarpoonStatusEffect(status);
                        break;
                    }
                }
                Recipe recipe = db.GetRecipe(item.m_itemData);
                if (recipe != null)
                    foreach (Piece.Requirement resource in recipe.m_resources)
                        if (resource != null && resource.m_resItem != null)
                        {
                            resource.m_amountPerLevel = resource.m_resItem.m_itemData.m_shared.m_name == "$item_chitin" ? 15 : 0;
                            resource.m_upgraderResource = false;
                        }
            }

            private static void Postfix(ObjectDB __instance) => PatchHarpoonStatusEffectAndRecipe(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class ObjectDB_CopyOtherDB_HarpoonStat
        {
            private static void Postfix(ObjectDB __instance) => ObjectDB_Awake_HarpoonStat.PatchHarpoonStatusEffectAndRecipe(__instance);
        }

        private static void PatchHarpoonStatusEffect(SE_Harpooned effect)
        {
            if (effect == null)
                return;
            effect.m_breakDistance = breakDistance.Value;
            effect.m_maxDistance = maxDistance.Value;
            effect.m_staminaDrain = 0.1f * drainStamina.Value;
            effect.m_pullSpeed = pullSpeed.Value;
            effect.m_smoothDistance = smoothDistance.Value;
            effect.m_forcePower = forcePower.Value;
        }

        private static void PatchInventory(Inventory inventory)
        {
            if (inventory.m_temoraryInventory)
                return;

            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            inventory.GetAllItems(itemDropNameSpearChitin, items);
            foreach (ItemDrop.ItemData item in items)
            {
                PatchHarpoonItemData(item);
                PatchHarpoonStatusEffect(item.m_shared.m_attackStatusEffect as SE_Harpooned);
            }
        }

        [HarmonyPatch]
        private static class Inventory_Load_HarpoonStats
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
            }

            private static void Postfix(Inventory __instance) => PatchInventory(__instance);
        }


        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
        public static class ItemDrop_Start_HarpoonStats
        {
            private static void Postfix(ItemDrop __instance)
            {
                if (Utils.GetPrefabName(__instance.gameObject) != prefabNameSpearChitin)
                    return;
                PatchHarpoonItemData(__instance.m_itemData);
                PatchHarpoonStatusEffect(__instance.m_itemData.m_shared.m_attackStatusEffect as SE_Harpooned);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Player_OnSpawned_HarpoonStats
        {
            private static void Postfix(Player __instance) => PatchInventory(__instance.GetInventory());
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
        public static class Ship_UpdateOwner_ShipPulling
        {
            private static bool Prefix(Ship __instance, ZNetView ___m_nview)
            {
                if (!harpoonActive || harpooned == null || m_nview != ___m_nview || ___m_nview == null || !___m_nview.IsValid())
                    return true;
                // Preserve the existing ship ownership policy for this maintenance release.
                m_nview.ClaimOwnership();
                return false;
            }
        }

        public static void SetHarpooned(Player attacker, GameObject hitObject, Vector3 hitPoint, bool pullTo, Collider collider)
        {
            if (attacker == null || hitObject == null || collider == null || ZNetScene.instance == null || !IsFinite(hitPoint))
                return;
            if (harpoonActive)
                DestroyHarpooned("Reinstantiate");

            m_attacker = attacker;
            targetHarpooned = hitObject;
            targetCollider = collider;
            connectionScene = ZNetScene.instance;
            attackerView = attacker.GetComponent<ZNetView>();
            attackerRbody = attacker.GetComponent<Rigidbody>();
            m_nview = hitObject.GetComponentInParent<ZNetView>();
            m_ship = hitObject.GetComponentInParent<Ship>();
            m_character = hitObject.GetComponentInParent<Character>();
            objectRbody = hitObject.GetComponentInParent<Rigidbody>() ?? collider.attachedRigidbody ?? hitObject.GetComponentInChildren<Rigidbody>();
            hadTargetView = m_nview != null;
            hadTargetBody = objectRbody != null;
            hadTargetCharacter = m_character != null;
            harpoonActive = true;
            if (attackerView == null || !attackerView.IsValid() || attackerRbody == null || (hadTargetView && !m_nview.IsValid()))
            {
                DestroyHarpooned("Missing initial participant state");
                return;
            }

            m_time = 0f;
            m_broken = false;
            m_drainStaminaTimer = 0f;
            m_breakDistance = breakDistance.Value;
            m_maxDistance = maxDistance.Value;
            m_staminaDrain = 0.1f * drainStamina.Value;
            m_pullSpeed = pullSpeed.Value;
            m_smoothDistance = smoothDistance.Value;
            targetDistance = Vector3.Distance(hitPoint, attacker.transform.position);
            objectMass = CalculateHitObjectMass(hitObject);
            targetName = GetHarpoonedTargetName(hitObject, collider);
            isPullingTo = false;
            if (targetLeviathan.Value && hitObject.GetComponentInParent<Leviathan>() != null && objectRbody != null && objectRbody.isKinematic)
            {
                temporarilyDynamicBody = objectRbody;
                objectRbody.isKinematic = false;
            }

            if (hitObject.GetComponentInParent<RandomFlyingBird>() != null || objectRbody == null || objectRbody.isKinematic || pullTo)
                isPullingTo = true;
            else if (m_character != null && m_character.IsAttached())
                isPullingTo = true;
            else if (m_ship != null && m_ship.HaveControllingPlayer())
                isPullingTo = true;
            else if (hitObject.GetComponentInParent<Vagon>() is Vagon vagon && vagon.InUse())
                isPullingTo = true;
            else if (m_ship == null && objectMass > maxBodyMassToPull.Value)
                isPullingTo = true;
            else if (hitObject.GetComponentInParent<ZSyncTransform>() == null || m_nview == null)
                isPullingTo = true;
            else if (!m_nview.IsOwner())
                m_nview.ClaimOwnership();

            if (hitObject.GetComponent<ItemDrop>() != null)
                m_pullSpeed = 100f;
            noUpForce = m_ship != null;
            if (hitObject.GetComponentInParent<Leviathan>() != null)
                m_minDistance = 20f;
            else if (m_ship != null)
                m_minDistance = minDistanceShip.Value;
            else if (m_character != null)
                m_minDistance = minDistanceCreature.Value;
            else if (hitObject.GetComponent<ItemDrop>() != null)
                m_minDistance = minDistanceItem.Value;
            else if (isPullingTo)
                m_minDistance = minDistancePullToTarget.Value;
            else
                m_minDistance = minDistancePullToPlayer.Value;

            GameObject prefab = connectionScene.GetPrefab("vfx_Harpooned");
            if (prefab == null)
            {
                DestroyHarpooned("Missing rope prefab");
                return;
            }
            harpooned = Instantiate(prefab, attacker.transform.position, Quaternion.identity, hitObject.transform);
            m_line = harpooned.GetComponent<LineConnect>();
            m_lineRenderer = harpooned.GetComponent<LineRenderer>();
            ropeView = harpooned.GetComponent<ZNetView>();
            if (m_line == null || m_lineRenderer == null || ropeView == null || !ropeView.IsValid() || !ropeView.IsOwner())
            {
                DestroyHarpooned("Missing rope components");
                return;
            }
            ropeID = ropeView.GetZDO().m_uid;
            m_line.SetPeer(attackerView);
            m_line.m_maxDistance = m_maxDistance;
            m_line.m_dynamicThickness = true;
            m_line.m_minThickness = 0.04f;
            m_lineRenderer.transform.position = hitPoint;
            ropeView.GetZDO().Set(HarpoonLine.VisualMarker, true);
            lineVisual = harpooned.AddComponent<HarpoonLine>();
            if (!lineVisual.Initialize(m_line) || !ValidateConnection(out string reason))
            {
                DestroyHarpooned("Invalid initial rope state");
                return;
            }
            if (applySlowFall.Value)
                castSlowFall = true;
            LogInfo($"Attacker: {attacker.m_name}, target: {hitObject.name}, name: {targetName}, mass: {objectMass}, pull to: {isPullingTo}");
            HarpoonMessage("$msg_harpoon_harpooned");
        }

        public static bool IsPullingTo() => isPullingTo || m_nview == null || !m_nview.IsOwner() || alwaysPullTo.Value;
        public static Rigidbody RBody() => IsPullingTo() ? attackerRbody : objectRbody;
        public static float Mass() => IsPullingTo() ? attackerRbody.mass + m_attacker.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value : objectMass;
        public static Vector3 TargetPosition() => IsPullingTo() ? m_lineRenderer.transform.position : m_attacker.transform.position;

        public static float CalculateHitObjectMass(GameObject hitObject)
        {
            float mass = 0f;
            foreach (Rigidbody body in hitObject.GetComponentsInChildren<Rigidbody>())
                mass += body.mass;
            if (m_ship == null && hitObject.GetComponent<Vagon>() == null)
                foreach (Container container in hitObject.GetComponentsInChildren<Container>())
                    mass += container.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value;
            if (hitObject.TryGetComponent(out ItemDrop item))
                mass += item.m_itemData.GetWeight() * containerInventoryWeightMassFactor.Value;
            return mass;
        }

        public void FixedUpdate()
        {
            if (!harpoonActive)
                return;
            try
            {
                UpdateHarpoonEffect(Time.fixedDeltaTime);
            }
            catch (Exception exception)
            {
                Logger.LogError($"Harpoon update failed; releasing the rope. {exception}");
                DestroyHarpooned("Update failed");
            }
        }

        public static void UpdateHarpoonEffect(float dt)
        {
            m_time += dt;
            if (IsDone())
            {
                DestroyHarpooned("Harpooning ended");
                return;
            }
            // Keep the legacy endpoints and thresholds; reject invalid distance before applying force.
            float distance = Vector3.Distance(TargetPosition(), RBody().transform.position);
            if (!CheckDistance(distance))
                return;
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
            float speed = (m_attacker.IsAttachedToShip() && m_ship != null) ? 10000f : m_pullSpeed;
            float num2 = Pull(RBody(), TargetPosition(), targetDistance, speed, pullForce, m_smoothDistance, IsPullingTo() ? Vector3.zero : forcePoint, m_character != null, noUpForce, useForce.Value, forcePower.Value);
            m_drainStaminaTimer += dt;
            float stamina = 0f;
            if (m_drainStaminaTimer > m_staminaDrainInterval && num2 > 0f)
            {
                m_drainStaminaTimer = 0f;
                if (!attachedShipStamina.Value || IsPullingTo() || !m_attacker.IsAttachedToShip())
                {
                    stamina = m_staminaDrain * num2 * (IsPullingTo() ? 10f : Mass() > 999 ? 20f : 10f + 20f * pullForce);
                    m_attacker.UseStamina(stamina);
                }
            }
            m_line.SetSlack((1f - Utils.LerpStep(targetDistance / 2f, targetDistance, distance)) * m_maxLineSlack);
            if (deepLoggingEnabled.Value)
                LogInfo($"dist: {distance,-5:F3} targetDist: {targetDistance,-5:F3} force: {pullForce,-5:F3} dt: {num2,-5:F3} stam: {stamina,-5:F3} break: {distance - targetDistance,-5:F3} < {m_breakDistance}");
            if (!m_attacker.HaveStamina())
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_released");
            }
            if (IsDone())
                DestroyHarpooned();
            else if (targetPulling.Value && (KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
            {
                float factorMass = IsPullingTo() ? 4f : 2f;
                if (KeyPressReleaseHarpoon())
                    targetDistance += factorMass * dt * 2f * pullSpeedMultiplier.Value;
                else if (KeyPressPullHarpoon())
                    targetDistance -= factorMass * dt * pullSpeedMultiplier.Value;
                targetDistance = Mathf.Max(targetDistance, m_minDistance + 0.5f);
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
            Vector3 b = Vector3.Project(body.linearVelocity, normalized.normalized);
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

            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maximumVelocity.Value);

            return num;
        }

        public static void HarpoonMessage(string message)
        {
            if (!messagesEnabled.Value || string.IsNullOrEmpty(message) || m_attacker == null || Localization.instance == null)
                return;
            string text = string.IsNullOrEmpty(targetName) ? message : targetName + " " + message;
            text = Localization.instance.Localize(text);
            if (text.Length > 1)
                text = text.ToUpper().Substring(0, 1) + text.Substring(1);
            m_attacker.Message(MessageHud.MessageType.Center, text);
        }

        public static bool IsDone()
        {
            if (m_broken)
                return true;
            if (!ValidateConnection(out string reason))
            {
                LogInfo(reason);
                return true;
            }
            if (m_time > timeBeforeStop.Value && (KeyPressStopHarpoon() || m_attacker.IsBlocking()))
            {
                HarpoonMessage("$msg_harpoon_released");
                return true;
            }
            if (m_attacker.IsDead() || m_attacker.IsTeleporting() || m_attacker.InCutscene() || m_attacker.IsEncumbered())
                return true;
            if (m_character != null && (m_character.IsDead() || m_character.IsTeleporting()))
                return true;
            if ((IsPullingTo() && m_attacker.IsAttached()) || (!targetBosses.Value && m_character != null && m_character.IsBoss()) || (Ship.GetLocalShip() != null && Ship.GetLocalShip() == m_ship))
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }
            if (IsPullingTo() && !pullUnderWater.Value && (ZoneSystem.instance == null || TargetPosition().y < ZoneSystem.instance.m_waterLevel))
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
            if (hitObject.GetComponent<Location>() != null)
                return "$piece_lorestone";
            if (hitObject.TryGetComponent(out ResourceRoot root))
                return root.m_name;
            if (hitObject.GetComponent<Ship>() != null && hitObject.TryGetComponent(out Piece shipPiece))
                return shipPiece.m_name;
            if (!targetMessagesEnabled.Value)
                return string.Empty;
            if (hitObject.TryGetComponent(out Piece piece))
                return piece.m_name;
            if (collider != null && collider.name.StartsWith("Terrain", StringComparison.Ordinal))
                return collider.name;
            string name = Utils.GetPrefabName(hitObject);
            return string.IsNullOrEmpty(name) ? string.Empty : char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
