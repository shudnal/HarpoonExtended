using System;
using HarmonyLib;
using UnityEngine;

namespace HarpoonExtended
{
    // Presentation and lifetime tracking only. The legacy physics endpoints remain in HarpoonExtended.
    public sealed class HarpoonLine : MonoBehaviour
    {
        internal static readonly int VisualMarker = "shudnal.HarpoonExtended.line".GetStableHashCode();
        private LineConnect line;
        private LineRenderer lineRenderer;
        private ZNetView view;
        private ZNetView peerView;
        private Player peer;
        private Transform hand;
        private ZDOID peerID;
        private bool retired;

        internal bool Initialize(LineConnect connector)
        {
            line = connector;
            if (line == null || ZNetScene.instance == null)
                return false;
            lineRenderer = line.GetComponent<LineRenderer>();
            view = line.m_nview;
            if (view == null || !view.IsValid())
                return false;
            peerID = view.GetZDO().GetZDOID(line.m_linePeerID);
            GameObject peerObject = ZNetScene.instance.FindInstance(peerID);
            if (peerObject == null)
                return false;
            peer = peerObject.GetComponent<Player>();
            peerView = peerObject.GetComponent<ZNetView>();
            if (peer == null)
                return false;
            VisEquipment equipment = peer.GetVisEquipment();
            hand = equipment != null ? equipment.m_leftHand : null;
            return IsValid();
        }

        internal bool IsValid()
        {
            return !retired && line != null && line.isActiveAndEnabled && lineRenderer != null && lineRenderer.positionCount >= 2 && !lineRenderer.useWorldSpace && lineRenderer.sharedMaterial != null &&
                view != null && view.IsValid() && peer != null && peer.gameObject.activeInHierarchy && peerView != null && peerView.IsValid() &&
                peerView.GetZDO().m_uid == peerID && view.GetZDO().GetZDOID(line.m_linePeerID) == peerID &&
                hand != null && hand.gameObject.activeInHierarchy && HarpoonExtended.HasValidTransform(hand) && HarpoonExtended.HasValidTransform(transform) &&
                HarpoonExtended.IsFinite(lineRenderer.GetPosition(0)) && HarpoonExtended.IsFinite(line.m_minDistance) && HarpoonExtended.IsFinite(line.m_maxDistance) &&
                (!line.m_dynamicThickness || line.m_minDistance != line.m_maxDistance);
        }

        private void LateUpdate()
        {
            if (retired)
                return;
            try
            {
                Render();
            }
            catch (Exception exception)
            {
                HarpoonExtended.LogVisualFailure(exception);
                Retire();
            }
        }

        private void Render()
        {
            if (!IsValid())
            {
                Retire();
                return;
            }
            if (view.IsOwner() && !HarpoonExtended.ValidateLineBeforeRendering(this))
            {
                Retire();
                return;
            }
            // LineConnect keeps its local-space geometry, slack and thickness; only the displayed peer endpoint changes.
            line.SetEndpoint(hand.position);
            lineRenderer.enabled = true;
        }

        private void Retire()
        {
            if (retired)
                return;
            retired = true;
            if (HarpoonExtended.IsLocalLine(this))
            {
                HarpoonExtended.DestroyHarpooned("Missing rope rendering state");
                return;
            }
            if (lineRenderer != null)
                lineRenderer.enabled = false;
            if (line != null)
                line.enabled = false;
            // ZNetScene.Destroy only removes the local replica when this peer is not the ZDO owner.
            // Missing observer state must never claim ownership or delete another peer's target.
            if (ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
            else
                UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDisable() => HarpoonExtended.OnLineDestroyed(this);
        private void OnDestroy() => HarpoonExtended.OnLineDestroyed(this);

        [HarmonyPatch(typeof(LineConnect), nameof(LineConnect.LateUpdate))]
        public static class LineConnect_LateUpdate_HarpoonHand
        {
            private static bool Prefix(LineConnect __instance)
            {
                if (__instance.TryGetComponent(out HarpoonLine visual))
                    return false;
                ZNetView view = __instance.m_nview;
                if (view == null || !view.IsValid() || !view.GetZDO().GetBool(VisualMarker))
                    return true;
                visual = __instance.gameObject.AddComponent<HarpoonLine>();
                if (!visual.Initialize(__instance))
                    visual.Retire();
                return false;
            }
        }
    }
}
