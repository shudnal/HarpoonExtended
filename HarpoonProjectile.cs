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
    internal class HarpoonProjectile
    {
        internal static int s_rayMaskSolidsAndItem;
        internal static int s_rayMaskSolids;

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Awake))]
        public static class Projectile_Awake_HarpoonProjectile
        {
            private static void Postfix()
            {
                if (s_rayMaskSolids == 0)
                    s_rayMaskSolids = Projectile.s_rayMaskSolids;

                if (s_rayMaskSolidsAndItem == 0)
                    s_rayMaskSolidsAndItem = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle", "item");
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.FixedUpdate))]
        public static class Projectile_FixedUpdate_HarpoonProjectile
        {
            private static void Prefix(Projectile __instance)
            {
                if (targetItems.Value && __instance.m_statusEffectHash == SE_HarpoonExtended.s_statusEffectHash)
                    Projectile.s_rayMaskSolids = s_rayMaskSolidsAndItem;
            }
            private static void Postfix(ref int ___s_rayMaskSolids)
            {
                if (Projectile.s_rayMaskSolids != s_rayMaskSolids)
                    Projectile.s_rayMaskSolids = s_rayMaskSolids;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        public static class Projectile_OnHit_HarpoonStats
        {
            private static void Prefix(Projectile __instance, Collider collider, Character ___m_owner, Vector3 hitPoint, ZNetView ___m_nview, ref int ___s_rayMaskSolids, ref int ___m_statusEffectHash)
            {
                if (__instance.m_statusEffectHash != SE_HarpoonExtended.s_statusEffectHash)
                    return;

                if (!__instance.m_nview.IsOwner())
                    return;

                if (collider == null || ___m_owner != Player.m_localPlayer)
                    return;

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

                if (targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() && colliderHitObject.TryGetComponent(out Rigidbody rbody) && rbody.isKinematic)
                {
                    rbody.isKinematic = false;

                    if (colliderHitObject.TryGetComponent(out ZSyncTransform zSyncTransform))
                        zSyncTransform.m_isKinematicBody = rbody.isKinematic;
                }

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


    }
}
