using HarmonyLib;
using UnityEngine;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    public static class HarpoonedPatches
    {
        public static bool IsHarpoonedTarget(GameObject gameObject) => Harpooned.HarpoonedTargets.ContainsKey(gameObject);

        [HarmonyPatch(typeof(MonoUpdaters), nameof(MonoUpdaters.FixedUpdate))]
        public static class MonoUpdaters_FixedUpdate_UpdateInBulk
        {
            private static void Postfix(MonoUpdaters __instance)
            {
                __instance.m_update.CustomFixedUpdate(Harpooned.Instances, "MonoUpdaters.FixedUpdate.Harpooned", Time.fixedDeltaTime);
                
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        public static class BaseAI_UpdateAI_FreeFromHarpooners
        {
            public static bool FleeFromHarpooner(BaseAI __instance, float dt)
            {
                if (!alwaysFlee.Value || !Harpooned.HarpoonedAI.TryGetValue(__instance, out Harpooned harpooned))
                    return false;
                
                __instance.SetAlerted(alert: true);

                Vector3 position = harpooned.GetAveragePosition();
                if (position != Vector3.zero)
                    __instance.Flee(dt, position);
                
                return true;
            }

            private static void Postfix(BaseAI __instance, float dt, ref bool __result)
            {
                if (__result && FleeFromHarpooner(__instance, dt))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
        public static class Turret_ShootProjectile_PreventShootingIfHarpooning
        {
            private static bool Prefix(Turret __instance) => !IsHarpoonedTarget(__instance.gameObject);
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.HasAmmo))]
        public static class Turret_HasAmmo_TurretHasAmmoIfHarpooning
        {
            private static void Postfix(Turret __instance, ref bool __result)
            {
                if (IsHarpoonedTarget(__instance.gameObject))
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTarget))]
        public static class Turret_UpdateTarget_PreventOtherTargetSeeking
        {
            private static void Prefix(Turret __instance)
            {
                if (IsHarpoonedTarget(__instance.gameObject))
                    __instance.m_updateTargetTimer = 0.5f;
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTurretRotation))]
        public static class Turret_UpdateTurretRotation_SeekHarpoonedTarget
        {
            private static void Prefix(Turret __instance, ref float __state)
            {
                if (IsHarpoonedTarget(__instance.gameObject))
                {
                    __instance.m_target ??= Harpooned.HarpoonedTargets[__instance.gameObject].CharacterToPull;
                    __state = __instance.m_horizontalAngle;
                    __instance.m_horizontalAngle = 180f;
                }
            }

            private static void Postfix(Turret __instance, float __state)
            {
                if (__state != 0f)
                    __instance.m_horizontalAngle = __state;
            }
        }
    }
}
