using HarmonyLib;
using UnityEngine;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    internal class HarpoonedPatches
    {
        [HarmonyPatch(typeof(MonoUpdaters), nameof(MonoUpdaters.FixedUpdate))]
        public static class MonoUpdaters_FixedUpdate_UpdateInBulk
        {
            private static void Postfix(MonoUpdaters __instance)
            {
                __instance.m_update.CustomFixedUpdate(Harpooned.Instances, "MonoUpdaters.FixedUpdate.Harpooned", Time.fixedDeltaTime);
                
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        public static class BaseAI_UpdateAI_FreeFromHarpooner
        {
            public static bool FleeFromHarpooner(BaseAI __instance, float dt)
            {
                if (!alwaysFlee.Value || !Harpooned.HarpoonedAI.TryGetValue(__instance, out Harpooned harpooned))
                    return false;
                
                __instance.SetAlerted(alert: true);
                __instance.Flee(dt, harpooned.TargetPosition());
                
                return true;
            }

            private static void Postfix(BaseAI __instance, float dt, ref bool __result)
            {
                if (__result && FleeFromHarpooner(__instance, dt))
                    __result = false;
            }
        }
    }
}
