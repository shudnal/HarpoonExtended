using HarmonyLib;
using UnityEngine;

namespace HarpoonExtended
{
    internal static class HarpoonSlowFall
    {
        internal const string EffectName = "HarpoonExtendedSlowFall";
        internal static readonly int EffectHash = EffectName.GetStableHashCode();
        private static readonly int sourceHash = "SlowFall".GetStableHashCode();
        private static SE_Stats template;

        internal static void Register(ObjectDB db)
        {
            if (db == null || db.GetStatusEffect(EffectHash) != null)
                return;
            if (template == null)
            {
                SE_Stats source = db.GetStatusEffect(sourceHash) as SE_Stats;
                if (source == null)
                    return;
                // Keep all presentation and other gameplay properties of the original effect.
                template = Object.Instantiate(source);
                template.name = EffectName;
                template.m_nameHash = 0;
            }
            db.m_StatusEffects.Add(template);
        }

        internal static void Apply(Player player, float speed, float damageMultiplier)
        {
            SEMan effects = player.GetSEMan();
            if (effects.HaveStatusEffect(EffectHash))
                return;
            Register(ObjectDB.instance);
            if (!(effects.AddStatusEffect(EffectHash, false, 0, 0f, -1) is SE_Stats effect))
                return;
            // SE_Stats adds a fraction of base damage; the public config uses a conventional multiplier.
            effect.m_maxMaxFallSpeed = HarpoonExtended.IsFinite(speed) ? Mathf.Max(0f, speed) : 5f;
            effect.m_fallDamageModifier = (HarpoonExtended.IsFinite(damageMultiplier) ? Mathf.Max(0f, damageMultiplier) : 0f) - 1f;
        }
    }

    public partial class HarpoonExtended
    {
        private static Player slowFallPlayer;

        private static void ResetSlowFallState(bool removeEffect)
        {
            if (removeEffect && slowFallPlayer != null)
                slowFallPlayer.GetSEMan()?.RemoveStatusEffect(HarpoonSlowFall.EffectHash, true);
            slowFallPlayer = null;
            castSlowFall = false;
            slowFallCasted = false;
            onGroundTimer = 0f;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
        public static class Player_FixedUpdate_SlowFallControl
        {
            private static void RemoveSlowFall(SEMan effects, string reason)
            {
                effects.RemoveStatusEffect(HarpoonSlowFall.EffectHash, true);
                slowFallCasted = false;
                slowFallPlayer = null;
                LogInfo(reason);
            }

            private static void CheckSlowFallStatus(Player player, SEMan effects)
            {
                if (harpoonActive)
                    return;
                if (removeSlowFallonGroundThreshold.Value > 0f && onGroundTimer >= removeSlowFallonGroundThreshold.Value)
                    RemoveSlowFall(effects, "Remove slow fall on ground");
                else if (player.IsAttached())
                    RemoveSlowFall(effects, "Remove slow fall on attached");
                else if (player.IsSwimming())
                    RemoveSlowFall(effects, "Remove slow fall on swimming");
                else if (player.IsDebugFlying())
                    RemoveSlowFall(effects, "Remove slow fall on flying");
                else if (removeSlowFallWithoutHarpoon.Value &&
                    (player.GetLeftItem() == null || player.GetLeftItem().m_shared.m_name != itemDropNameSpearChitin) &&
                    (player.GetRightItem() == null || player.GetRightItem().m_shared.m_name != itemDropNameSpearChitin))
                    RemoveSlowFall(effects, "Remove slow fall without harpoon");
            }

            private static void Postfix(Player __instance, SEMan ___m_seman)
            {
                if (__instance != Player.m_localPlayer || __instance.m_nview == null || !__instance.m_nview.IsValid() || ___m_seman == null)
                    return;
                if (slowFallPlayer != null && slowFallPlayer != __instance)
                    ResetSlowFallState(true);
                if (castSlowFall)
                {
                    HarpoonSlowFall.Apply(__instance, slowFallSpeed.Value, slowFallDamageMultiplier.Value);
                    slowFallCasted = ___m_seman.HaveStatusEffect(HarpoonSlowFall.EffectHash);
                    slowFallPlayer = slowFallCasted ? __instance : null;
                    castSlowFall = false;
                    onGroundTimer = 0f;
                }
                if (!slowFallCasted)
                    return;
                if (!___m_seman.HaveStatusEffect(HarpoonSlowFall.EffectHash))
                {
                    ResetSlowFallState(false);
                    return;
                }
                // Intentionally cumulative: brief jumps must not reset the removal timer.
                if (__instance.IsOnGround())
                    onGroundTimer += Time.fixedDeltaTime;
                CheckSlowFallStatus(__instance, ___m_seman);
            }
        }
    }
}
