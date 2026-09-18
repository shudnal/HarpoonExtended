using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HarpoonExtended
{
    public partial class HarpoonExtended
    {
        private static readonly HashSet<string> allowedPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> deniedPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static void OnTargetFilterChanged(object sender, EventArgs args) => RebuildTargetFilters();

        private static void RebuildTargetFilters()
        {
            ParsePrefabNames(targetWhitelist.Value, allowedPrefabs);
            ParsePrefabNames(targetBlacklist.Value, deniedPrefabs);
        }

        private static void ParsePrefabNames(string value, HashSet<string> result)
        {
            result.Clear();
            foreach (string entry in (value ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = entry.Trim();
                if (name.Length != 0)
                    result.Add(name);
            }
        }

        private static bool IsHarpoonProjectile(Projectile projectile)
            => projectile != null && projectile.name.StartsWith("projectile_chitinharpoon", StringComparison.Ordinal);

        private static bool IsRopeForbidden(GameObject target, Collider collider)
        {
            if (target == null || collider == null || target.GetComponentInParent<GrapplingBlocker>() != null || collider.GetComponentInParent<GrapplingBlocker>() != null)
                return true;
            ZNetView view = target.GetComponentInParent<ZNetView>();
            GameObject prefab = view != null && view.IsValid() && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(view.GetZDO().GetPrefab()) : null;
            string name = prefab != null ? prefab.name : Utils.GetPrefabName(view != null ? view.gameObject : target);
            return deniedPrefabs.Contains(name) || (allowedPrefabs.Count != 0 && !allowedPrefabs.Contains(name));
        }

        private static bool IsCustomTarget(GameObject target)
        {
            return targetGround.Value ||
                targetShip.Value && target.GetComponentInParent<Ship>() != null ||
                targetCreatures.Value && target.GetComponentInParent<Character>() != null ||
                targetTreeLog.Value && target.GetComponentInParent<TreeLog>() != null ||
                targetTreeBase.Value && target.GetComponentInParent<TreeBase>() != null ||
                targetPiece.Value && target.GetComponentInParent<Piece>() != null ||
                targetDestructibles.Value && (target.GetComponentInParent<Destructible>() != null || target.GetComponentInParent<MineRock>() != null || target.GetComponentInParent<MineRock5>() != null) ||
                targetFish.Value && target.GetComponentInParent<Fish>() != null ||
                targetLeviathan.Value && target.GetComponentInParent<Leviathan>() != null ||
                targetItems.Value && target.GetComponentInParent<ItemDrop>() != null;
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Awake))]
        public static class Projectile_Awake_HarpoonStats
        {
            private static void Postfix(Projectile __instance)
            {
                if (!IsHarpoonProjectile(__instance) || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                    return;
                __instance.m_gravity *= projectileGravityMiltiplier.Value;
                __instance.m_rayRadius = Mathf.Clamp(hitboxSize.Value, 0f, 0.5f);
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        public static class Projectile_Setup_HarpoonStats
        {
            private static void Postfix(Projectile __instance)
            {
                if (!IsHarpoonProjectile(__instance) || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                    return;
                if (disableDamage.Value)
                    __instance.m_damage.Modify(0f);
                __instance.m_vel *= projectileVelocityMultiplier.Value;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.FixedUpdate))]
        public static class Projectile_FixedUpdate_HarpoonMask
        {
            public struct MaskState
            {
                public bool Applied;
                public int Original;
            }

            private static void Prefix(Projectile __instance, out MaskState __state)
            {
                __state = default;
                if (!targetItems.Value || !IsHarpoonProjectile(__instance) || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                    return;
                // The collision query is in FixedUpdate, before OnHit. Do not change the mask in Awake.
                __state.Original = Projectile.s_rayMaskSolids;
                __state.Applied = true;
                Projectile.s_rayMaskSolids |= LayerMask.GetMask("item");
            }

            private static void Finalizer(MaskState __state)
            {
                // Restore this invocation's snapshot even if another patch or the hit callback throws.
                if (__state.Applied)
                    Projectile.s_rayMaskSolids = __state.Original;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        public static class Projectile_OnHit_HarpoonStats
        {
            public sealed class HitState
            {
                public Player Attacker;
                public GameObject Target;
                public Collider Collider;
                public Vector3 Point;
                public bool PullTo;
                public bool Attach;
                public bool SuppressedStatus;
                public int OriginalStatus;
            }

            private static void Prefix(Projectile __instance, Collider collider, Vector3 hitPoint, out HitState __state)
            {
                __state = null;
                if (!IsHarpoonProjectile(__instance) || __instance.m_didHit || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                    return;
                Player attacker = __instance.m_owner as Player;
                if (attacker == null || attacker != Player.m_localPlayer || collider == null)
                    return;
                GameObject target = Projectile.FindHitObject(collider);
                if (target == null)
                    return;
                __state = new HitState
                {
                    Attacker = attacker,
                    Target = target,
                    Collider = collider,
                    Point = hitPoint,
                    PullTo = KeyPressPullTo(),
                    OriginalStatus = __instance.m_statusEffectHash
                };
                if (IsRopeForbidden(target, collider))
                {
                    SuppressStatus(__instance, __state);
                    return;
                }
                Character character = target.GetComponentInParent<Character>();
                // Keep the existing vanilla path for players and disabled creature overrides.
                if (character != null && (character.IsPlayer() || !targetCreatures.Value || !targetPulling.Value))
                    return;
                if (target == attacker.gameObject || target.GetComponentInParent<FollowPlayer>() != null)
                {
                    SuppressStatus(__instance, __state);
                    return;
                }
                if (!IsCustomTarget(target))
                    return;
                // Only the rope is replaced. Original hit acceptance, damage, effects and projectile completion still run.
                SuppressStatus(__instance, __state);
                if (character != null && character.IsBoss() && !targetBosses.Value)
                    return;
                float distance = Vector3.Distance(hitPoint, attacker.transform.position);
                if (!IsFinite(hitPoint) || !IsFinite(distance) || !IsFinite(maxDistance.Value) || maxDistance.Value <= 0f || distance > maxDistance.Value)
                    return;
                ZNetView targetView = target.GetComponentInParent<ZNetView>();
                if (targetView != null && !targetView.IsValid())
                    return;
                __state.Attach = true;
            }

            private static void SuppressStatus(Projectile projectile, HitState state)
            {
                state.SuppressedStatus = true;
                projectile.m_statusEffectHash = 0;
            }

            private static void Postfix(Projectile __instance, HitState __state)
            {
                if (__state == null || !__state.Attach || __instance == null || !__instance.m_didHit || __instance.m_didBounce || __state.Target == null || __state.Collider == null || __state.Attacker == null)
                    return;
                if (harpoonActive && harpooned != null && m_time < 0.5f)
                    return;
                try
                {
                    SetHarpooned(__state.Attacker, __state.Target, __state.Point, __state.PullTo, __state.Collider);
                }
                catch (Exception exception)
                {
                    instance.Logger.LogError($"Harpoon attachment failed; releasing the rope. {exception}");
                    DestroyHarpooned("Attachment failed");
                }
            }

            private static void Finalizer(Projectile __instance, HitState __state)
            {
                if (__state != null && __state.SuppressedStatus && __instance != null)
                    __instance.m_statusEffectHash = __state.OriginalStatus;
            }
        }
    }
}
