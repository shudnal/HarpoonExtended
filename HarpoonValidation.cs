using System;
using HarmonyLib;
using UnityEngine;

namespace HarpoonExtended
{
    public partial class HarpoonExtended
    {
        private static bool harpoonActive;
        private static bool destroyingHarpoon;
        private static bool hadTargetView;
        private static bool hadTargetBody;
        private static bool hadTargetCharacter;
        private static Collider targetCollider;
        private static ZNetView attackerView;
        private static ZNetView ropeView;
        private static ZNetScene connectionScene;
        private static ZDOID ropeID = ZDOID.None;
        private static HarpoonLine lineVisual;
        private static Rigidbody temporarilyDynamicBody;

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        internal static bool HasValidTransform(Transform value)
        {
            if (value == null || !IsFinite(value.position))
                return false;
            Quaternion rotation = value.rotation;
            Vector3 scale = value.lossyScale;
            return IsFinite(rotation.x) && IsFinite(rotation.y) && IsFinite(rotation.z) && IsFinite(rotation.w) &&
                IsFinite(scale) && Mathf.Abs(scale.x) > Mathf.Epsilon && Mathf.Abs(scale.y) > Mathf.Epsilon && Mathf.Abs(scale.z) > Mathf.Epsilon;
        }

        private static bool ValidateConnection(out string reason)
        {
            reason = null;
            if (!harpoonActive || connectionScene == null || connectionScene != ZNetScene.instance)
                reason = "Harpoon scene is no longer available";
            else if (m_attacker == null || m_attacker != Player.m_localPlayer || !m_attacker.gameObject.activeInHierarchy || attackerView == null || !attackerView.IsValid() || !attackerView.IsOwner())
                reason = "Harpoon attacker is no longer available";
            else if (targetHarpooned == null || !targetHarpooned.activeInHierarchy || targetCollider == null || !targetCollider.enabled)
                reason = "Harpoon target or hit collider is no longer available";
            else if (hadTargetView && (m_nview == null || !m_nview.IsValid()))
                reason = "Harpoon target network view was lost";
            else if ((hadTargetBody && objectRbody == null) || (hadTargetCharacter && m_character == null))
                reason = "Harpoon target component was destroyed";
            else if (harpooned == null || !harpooned.activeInHierarchy || m_line == null || m_lineRenderer == null || lineVisual == null)
                reason = "Harpoon line component was destroyed";
            else if (ropeView == null || !ropeView.IsValid() || !ropeView.IsOwner() || m_line.m_nview != ropeView || ropeView.GetZDO().m_uid != ropeID)
                reason = "Harpoon line network state was lost";
            else if (harpooned.transform.parent != targetHarpooned.transform || !m_line.isActiveAndEnabled || !lineVisual.IsValid())
                reason = "Harpoon line attachment or visual state was lost";
            else if (!HasValidTransform(targetHarpooned.transform) || !HasValidTransform(m_attacker.transform) || !HasValidTransform(harpooned.transform))
                reason = "Harpoon participant transform is invalid";
            else if (attackerRbody == null || !IsFinite(attackerRbody.position) || !IsFinite(attackerRbody.linearVelocity))
                reason = "Harpoon attacker body is invalid";
            else if (RBody() == null || RBody().isKinematic || !RBody().gameObject.activeInHierarchy || !IsFinite(RBody().position) || !IsFinite(RBody().linearVelocity) || !IsFinite(RBody().angularVelocity) || !HasValidTransform(RBody().transform) || !IsFinite(RBody().mass) || RBody().mass <= 0f)
                reason = "Harpoon moved body is invalid";
            else if (!IsFinite(targetDistance) || targetDistance <= 0f || !IsFinite(m_minDistance) || m_minDistance < 0f || !IsFinite(m_breakDistance) || m_breakDistance < 0f || !IsFinite(m_maxDistance) || m_maxDistance <= 0f)
                reason = "Harpoon distance parameters are invalid";
            else if (!IsFinite(m_smoothDistance) || m_smoothDistance <= 0f || !IsFinite(m_pullSpeed) || !IsFinite(m_staminaDrain) || !IsFinite(objectMass) || !IsFinite(Mass()) || !IsFinite(m_maxLineSlack))
                reason = "Harpoon force parameters are invalid";
            else if (!IsFinite(pullForceMultiplier.Value) || !IsFinite(forcePower.Value) || forcePower.Value < 0f || !IsFinite(maximumVelocity.Value) || maximumVelocity.Value < 0f || !IsFinite(pullSpeedMultiplier.Value))
                reason = "Harpoon live force settings are invalid";
            return reason == null;
        }

        private static bool CheckDistance(float distance)
        {
            if (!IsFinite(distance))
            {
                DestroyHarpooned("Invalid distance");
                return false;
            }
            if (distance < m_minDistance)
            {
                HarpoonMessage("$msg_harpoon_released");
                DestroyHarpooned("Too close");
                return false;
            }
            if (distance - targetDistance > m_breakDistance)
            {
                HarpoonMessage("$msg_harpoon_linebroke");
                DestroyHarpooned("Line broke");
                return false;
            }
            return true;
        }

        internal static void LogVisualFailure(Exception exception)
        {
            if (instance != null)
                instance.Logger.LogError($"Harpoon rendering failed; releasing the rope. {exception}");
        }

        internal static bool IsLocalLine(HarpoonLine line) => harpoonActive && ReferenceEquals(lineVisual, line);

        internal static bool ValidateLineBeforeRendering(HarpoonLine line)
        {
            if (!IsLocalLine(line))
                return false;
            if (IsDone())
            {
                DestroyHarpooned("Invalid state before rendering");
                return false;
            }
            return CheckDistance(Vector3.Distance(TargetPosition(), RBody().transform.position));
        }

        internal static void OnLineDestroyed(HarpoonLine line)
        {
            if (IsLocalLine(line))
                DestroyHarpooned("Line destroyed or disabled");
        }

        public static void DestroyHarpooned(string logEntry = "")
        {
            if (destroyingHarpoon)
                return;
            destroyingHarpoon = true;
            GameObject line = harpooned;
            ZNetScene scene = connectionScene;
            ZDOID id = ropeID;
            Rigidbody dynamicBody = temporarilyDynamicBody;
            bool removedThroughView = false;
            // Clear ownership of all references before invoking Unity/network destruction callbacks.
            harpoonActive = false;
            harpooned = null;
            lineVisual = null;
            ropeView = null;
            ropeID = ZDOID.None;
            connectionScene = null;
            m_line = null;
            m_lineRenderer = null;
            targetHarpooned = null;
            targetCollider = null;
            m_attacker = null;
            attackerView = null;
            attackerRbody = null;
            objectRbody = null;
            m_nview = null;
            m_ship = null;
            m_character = null;
            temporarilyDynamicBody = null;
            hadTargetView = false;
            hadTargetBody = false;
            hadTargetCharacter = false;
            m_broken = false;
            m_time = 0f;
            m_drainStaminaTimer = 0f;
            targetName = string.Empty;
            targetDistance = 999999f;
            objectMass = 0f;
            noUpForce = false;
            isPullingTo = false;
            // Do not clear a pending or active fall effect here: a broken rope is not a safe landing.
            try
            {
                LogInfo(logEntry);
                if (dynamicBody != null)
                    dynamicBody.isKinematic = true;
                if (line != null)
                {
                    LineRenderer renderer = line.GetComponent<LineRenderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                    LineConnect connector = line.GetComponent<LineConnect>();
                    if (connector != null)
                        connector.enabled = false;
                    if (scene != null && scene == ZNetScene.instance && ZDOMan.instance != null)
                    {
                        ZNetView oldView = line.GetComponent<ZNetView>();
                        removedThroughView = oldView != null && oldView.IsValid() && oldView.GetZDO().m_uid == id;
                        scene.Destroy(line);
                    }
                    else
                        UnityEngine.Object.Destroy(line);
                }
                // Unity may already have destroyed the child rope together with a non-networked target.
                // In that case its GameObject is null, but its owned ZDO still needs to be retired.
                if (!removedThroughView && scene != null && scene == ZNetScene.instance && !id.IsNone() && ZDOMan.instance != null)
                {
                    ZDO zdo = ZDOMan.instance.GetZDO(id);
                    if (zdo != null && zdo.IsOwner())
                        ZDOMan.instance.DestroyZDO(zdo);
                }
            }
            catch (Exception exception)
            {
                if (line != null)
                    UnityEngine.Object.Destroy(line);
                if (instance != null)
                    instance.Logger.LogError($"Harpoon cleanup failed after clearing the connection state. {exception}");
            }
            finally
            {
                destroyingHarpoon = false;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
        public static class ZNetScene_Destroy_CheckParentDestroy
        {
            private static void Prefix(GameObject go)
            {
                if (!harpoonActive || destroyingHarpoon || go == null || go == harpooned)
                    return;
                if ((targetHarpooned != null && (go == targetHarpooned || targetHarpooned.transform.IsChildOf(go.transform))) ||
                    (m_attacker != null && (go == m_attacker.gameObject || m_attacker.transform.IsChildOf(go.transform))))
                    DestroyHarpooned("Participant destroyed");
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnDestroy))]
        public static class Player_OnDestroy_HarpoonCleanup
        {
            private static void Prefix(Player __instance)
            {
                if (harpoonActive && m_attacker == __instance)
                    DestroyHarpooned("Attacker destroyed");
                if (slowFallPlayer == __instance || __instance == Player.m_localPlayer)
                    ResetSlowFallState(false);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown))]
        public static class ZNetScene_Shutdown_HarpoonCleanup
        {
            private static void Prefix()
            {
                DestroyHarpooned("World shutdown");
                ResetSlowFallState(true);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy))]
        public static class ZNetScene_OnDestroy_HarpoonCleanup
        {
            private static void Prefix(ZNetScene __instance)
            {
                if (connectionScene == __instance)
                    DestroyHarpooned("World destroyed");
                ResetSlowFallState(false);
            }
        }
    }
}
