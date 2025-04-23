using HarmonyLib;
using UnityEngine;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    internal class HarpoonProjectile : Projectile
    {
        internal static int s_rayMaskSolidsAndItem;
        private Turret m_turret;

        public bool IsPlayerOwner => m_owner is Player;

        private GameObject OwnerObject => m_turret?.gameObject ?? m_owner?.gameObject;

        private bool HasOwner => OwnerObject != null;

        private Vector3 OwnerPosition => HasOwner ? OwnerObject.transform.position : Vector3.zero;

        public void SetOwner(Turret turret)
        {
            m_turret = turret;
        }

        public new void Awake()
        {
            base.Awake();

            m_gravity *= projectileGravityMiltiplier.Value;
            m_rayRadius = Mathf.Clamp(hitboxSize.Value, 0.0f, 0.5f);

            if (s_rayMaskSolidsAndItem == 0)
                s_rayMaskSolidsAndItem = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle", "item");
        }

        public new void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            if (disableDamage.Value)
                m_damage.Modify(0f);

            m_vel *= projectileVelocityMultiplier.Value;
        }

        public new void OnHit(Collider collider, Vector3 hitPoint, bool water, Vector3 normal)
        {
            if (!m_didHit)
                return;

            if (!HasOwner)
                return;

            if (collider == null || FindHitObject(collider) is not GameObject colliderHitObject)// || colliderHitObject.GetComponentInParent<FollowPlayer>() != null) 
                return;

            /*if (targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() && colliderHitObject.TryGetComponent(out Rigidbody rigidbody) && rigidbody.isKinematic)
                rigidbody.isKinematic = false;*/

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
                ((bool)colliderHitObject.GetComponent<Location>() ? " : Location" : "") +
                ((bool)colliderHitObject.GetComponent("ShipMan") ? " : ShipMan" : ""));

            if (targetGround.Value ||
                targetShip.Value && ((bool)colliderHitObject.GetComponent<Ship>() || (bool)colliderHitObject.GetComponent("ShipMan")) ||
                targetCreatures.Value && (bool)colliderHitObject.GetComponent<Character>() ||
                targetTreeLog.Value && (bool)colliderHitObject.GetComponent<TreeLog>() ||
                targetTreeBase.Value && (bool)colliderHitObject.GetComponent<TreeBase>() ||
                targetPiece.Value && (bool)colliderHitObject.GetComponent<Piece>() ||
                targetDestructibles.Value && ((bool)colliderHitObject.GetComponent<Destructible>() || (bool)colliderHitObject.GetComponent<MineRock>() || (bool)colliderHitObject.GetComponent<MineRock5>()) ||
                targetFish.Value && (bool)colliderHitObject.GetComponent<Fish>() ||
                targetLeviathan.Value && (bool)colliderHitObject.GetComponent<Leviathan>() ||
                targetItems.Value && (bool)colliderHitObject.GetComponent<ItemDrop>())
            {
                float hitDistance = Vector3.Distance(hitPoint, OwnerPosition);
                if (hitDistance > m_maxDistance)
                {
                    LogInfo($"{OwnerObject.name} {OwnerPosition} is too far {hitDistance} to {hitPoint}");
                    return;
                }

                Harpooned.SetHarpooned(OwnerObject, colliderHitObject, hitPoint, normal);
            }
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
    public static class Turret_ShootProjectile_SetTurretOwner
    {
        private static bool Prefix(Turret __instance) => !Harpooned.HarpoonedTargets.Contains(__instance.gameObject);

        private static void Postfix(Turret __instance)
        {
            if (!__instance.m_nview.IsOwner())
                return;

            if (!__instance.m_lastProjectile || !__instance.m_lastProjectile.TryGetComponent(out HarpoonProjectile harpoonProjectile))
                return;

            harpoonProjectile.SetOwner(__instance);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    public static class Projectile_Setup_HarpoonProjectile
    {
        private static void Prefix(Projectile __instance, ref bool __state)
        {
            if (!__instance.m_nview.IsOwner())
                return;

            __state = __instance is HarpoonProjectile;
        }

        private static void Postfix(Projectile __instance, Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo, bool __state)
        {
            if (!__state)
                return;

            (__instance as HarpoonProjectile).Setup(owner, velocity, hitNoise, hitData, item, ammo);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    public static class Projectile_OnHit_HarpoonProjectile
    {
        private static void Prefix(Projectile __instance, ref bool __state)
        {
            if (!__instance.m_nview.IsOwner())
                return;

            if (__instance is HarpoonProjectile harpoon)
            {
                __state = true;

                if (targetItems.Value && harpoon.IsPlayerOwner)
                    (s_rayMaskSolids, s_rayMaskSolidsAndItem) = (s_rayMaskSolidsAndItem, s_rayMaskSolids);
            }
        }

        private static void Postfix(Projectile __instance, Collider collider, Vector3 hitPoint, bool water, Vector3 normal, bool __state)
        {
            if (!__state)
                return;

            if (__instance is HarpoonProjectile harpoon)
            {
                if (targetItems.Value && harpoon.IsPlayerOwner)
                    (s_rayMaskSolids, s_rayMaskSolidsAndItem) = (s_rayMaskSolidsAndItem, s_rayMaskSolids);

                harpoon.OnHit(collider, hitPoint, water, normal);
            }
        }
    }
}
