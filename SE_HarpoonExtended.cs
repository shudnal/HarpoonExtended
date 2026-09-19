using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Attack;
using static HarpoonExtended.HarpoonExtended;

namespace HarpoonExtended
{
    internal class SE_HarpoonExtended : StatusEffect
    {
        internal const string s_statusEffectName = "SE_HarpoonExtended";
        internal static int s_statusEffectHash = s_statusEffectName.GetStableHashCode();

        public float m_baseDistance = 999999f;

        public float m_pullForce;

        public float m_forcePower = 2f;

        public float m_pullSpeed = 1000f;

        public float m_smoothDistance = 2f;

        public float m_maxLineSlack = 0.3f;

        public float m_breakDistance = 15f;

        public float m_minDistance = 2f;

        public float m_maxDistance = 50f;

        public float m_staminaDrain = 0.1f;

        public float m_staminaDrainInterval = 0.1f;

        public bool m_broken;

        public Character m_attacker;

        public Vector3 m_hitPoint;

        public float targetDistance = 999999f;

        public LineConnect m_line;

        public float m_drainStaminaTimer;

        public Rigidbody objectRbody;
        public Rigidbody attackerRbody;

        public GameObject targetHarpooned;

        public string targetName;

        public bool noUpForce;

        public ZNetView m_nview;

        public Ship m_ship;

        public float objectMass;

        public LineRenderer m_lineRenderer;

        public bool isPullingTo;

        public void SetHarpooned(Player attacker, GameObject hitObject, Vector3 hitPoint, bool pullTo, Collider collider)
        {
            m_attacker = attacker;
            targetName = "";
            m_ship = hitObject.GetComponent<Ship>();

            objectRbody = hitObject.GetComponent<Rigidbody>();
            attackerRbody = attacker.GetComponent<Rigidbody>();

            objectMass = CalculateHitObjectMass(hitObject);

            m_nview = hitObject.GetComponent<ZNetView>();
            m_character = hitObject.GetComponent<Character>();

            if ((bool)hitObject.GetComponent<RandomFlyingBird>())
            {
                // Bird doesn't have rigidbody but is not stational
                if (deepLoggingEnabled.Value) LogInfo("Pull to bird");
                isPullingTo = true;
            }
            else if (!(bool)objectRbody)
            {
                // If the target has no rigidbody we should pull to it - set stational target hit point
                if (deepLoggingEnabled.Value) LogInfo("Pull to object");
                isPullingTo = true;
            }
            else if (objectRbody.isKinematic)
            {
                // If target has kinematic rigidbody we should pull to it - set stational target hit point
                if (deepLoggingEnabled.Value) LogInfo("Pull to kinematic rigidbody");
                isPullingTo = true;
            }
            else if (pullTo)
            {
                // if the target has rigidbody yet we should pull to it - set target transform
                if (deepLoggingEnabled.Value) LogInfo("Pull to target intentional");
                isPullingTo = true;
            }
            else if (m_character != null && (m_character.IsAttached()))
            {
                // You can't move attached Character
                if (deepLoggingEnabled.Value) LogInfo("Can't pull attached");
                isPullingTo = true;
            }
            else if (m_ship != null && m_ship.HaveControllingPlayer())
            {
                // You can't move already moving ship
                if (deepLoggingEnabled.Value) LogInfo("Can't pull already moving ship");
                isPullingTo = true;
            }
            else if (hitObject.TryGetComponent<Vagon>(out Vagon vagon) && vagon.InUse())
            {
                // You can't move already moving vagon
                if (deepLoggingEnabled.Value) LogInfo("Can't pull already moving vagon");
                isPullingTo = true;
            }
            else if (m_ship == null && objectMass > maxBodyMassToPull.Value)
            {
                if (deepLoggingEnabled.Value) LogInfo($"Can't pull object {objectRbody} with mass {objectMass} more that {maxBodyMassToPull.Value}");
                isPullingTo = true;
            }
            else if (!(bool)hitObject.GetComponent<ZSyncTransform>())
            {
                if (deepLoggingEnabled.Value) LogInfo("Can't pull not netsynchronized object");
                isPullingTo = true;
            }
            else if (m_nview.IsOwner())
            {
                if (deepLoggingEnabled.Value) LogInfo("Move owned");
            }
            else
            {
                // screw it take ownership and move
                if (deepLoggingEnabled.Value) LogInfo("Claim ownership and movе");
                m_nview.ClaimOwnership();
            }

            if ((bool)hitObject.GetComponent<ItemDrop>())
                m_pullSpeed = 100f;

            targetHarpooned = hitObject;
            m_hitPoint = hitPoint;
            targetDistance = Vector3.Distance(hitPoint, attacker.transform.position);

            targetName = GetHarpoonedTargetName(hitObject, collider);

            LogInfo($"Attacker: {attacker.m_name}, target: {hitObject.name}, name: {targetName}, mass: {objectMass}, pull to: {isPullingTo}");

            noUpForce = (bool)hitObject.GetComponent<Ship>();

            if (hitObject.TryGetComponent<Leviathan>(out _))
                m_minDistance = 20f;  // Just in case because colliding with Levi will launch you in the sky
            else if (m_ship != null)
                m_minDistance = minDistanceShip.Value;
            else if (m_character != null)
                m_minDistance = minDistanceCreature.Value;
            else if (hitObject.TryGetComponent<ItemDrop>(out _))
                m_minDistance = minDistanceItem.Value;
            else if (isPullingTo)
                m_minDistance = minDistancePullToTarget.Value;
            else
                m_minDistance = minDistancePullToPlayer.Value;

            if (applySlowFall.Value)
                castSlowFall = true;

            HarpoonMessage("$msg_harpoon_harpooned");
        }

        public bool IsPullingTo()
        {
            return isPullingTo || m_nview == null || !m_nview.IsOwner() || alwaysPullTo.Value;
        }

        public Rigidbody RBody()
        {
            return IsPullingTo() ? attackerRbody : objectRbody;
        }

        public float Mass()
        {
            return IsPullingTo() ? attackerRbody.mass + (m_attacker as Player).GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value : objectMass;
        }

        public Vector3 TargetPosition()
        {
            return IsPullingTo() ? m_lineRenderer.transform.position : m_attacker.transform.position;
        }

        public float CalculateHitObjectMass(GameObject hitObject)
        {
            float objectMass = 0f;

            hitObject.GetComponentsInChildren<Rigidbody>().Do(rb => objectMass += rb.mass);

            if (m_ship == null && !hitObject.GetComponent<Vagon>())
                hitObject.GetComponentsInChildren<Container>().Do(cont => objectMass += cont.GetInventory().GetTotalWeight() * containerInventoryWeightMassFactor.Value);

            if (hitObject.TryGetComponent(out ItemDrop item))
                objectMass += item.m_itemData.GetWeight() * containerInventoryWeightMassFactor.Value;

            return objectMass;
        }

        public void HarpoonMessage(string message)
        {
            if (!messagesEnabled.Value)
                return;

            if (String.IsNullOrEmpty(message))
                return;

            string showMessage = targetName + " " + message;
            if (String.IsNullOrEmpty(targetName))
                showMessage = message;

            showMessage = Localization.instance.Localize(showMessage);

            if (showMessage.Length > 1)
                m_attacker.Message(MessageHud.MessageType.Center, showMessage.ToUpper().Substring(0, 1) + showMessage.Substring(1));
            else
                m_attacker.Message(MessageHud.MessageType.Center, showMessage);
        }

        public override void Setup(Character character)
        {
            base.Setup(character);

            m_breakDistance = breakDistance.Value;
            m_maxDistance = maxDistance.Value;
            m_staminaDrain = 0.1f * drainStamina.Value;
            m_pullSpeed = pullSpeed.Value;
            m_smoothDistance = smoothDistance.Value;
        }

        public override void SetAttacker(Character attacker)
        {
            ZLog.Log("Setting attacker " + attacker.m_name);
            m_attacker = attacker;
            m_time = 0f;
            if (!targetBosses.Value && m_character.IsBoss())
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                m_broken = true;
                return;
            }

            float num = Vector3.Distance(m_attacker.transform.position, m_character.transform.position);
            if (num > m_maxDistance)
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_harpoon_targettoofar");
                m_broken = true;
                return;
            }

            m_baseDistance = num;
            m_attacker.Message(MessageHud.MessageType.Center, m_character.m_name + " $msg_harpoon_harpooned");
            GameObject[] startEffectInstances = m_startEffectInstances;
            foreach (GameObject gameObject in startEffectInstances)
            {
                if ((bool)gameObject)
                {
                    LineConnect component = gameObject.GetComponent<LineConnect>();
                    if ((bool)component)
                    {
                        component.SetPeer(m_attacker.GetComponent<ZNetView>());
                        m_line = component;
                        m_line.m_maxDistance = m_maxDistance;
                        m_line.m_dynamicThickness = true;
                        m_line.m_minThickness = 0.04f;

                        m_lineRenderer = gameObject.GetComponent<LineRenderer>();
                        m_lineRenderer.transform.position = m_hitPoint;
                    }

                }
            }
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            float distance = Vector3.Distance(TargetPosition(), RBody().transform.position);

            if (distance < m_minDistance)
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_released");
                return;
            }

            if ((bool)m_line)
            {
                m_line.SetSlack((1f - Utils.LerpStep(targetDistance / 2f, targetDistance, distance)) * m_maxLineSlack);
            }

            if (distance - targetDistance > m_breakDistance)
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_linebroke");
                LogInfo("Line broke");
                return;
            }

            if (!m_attacker.HaveStamina())
            {
                m_broken = true;
                HarpoonMessage("$msg_harpoon_released");
                LogInfo("Stamina depleted");
                return;
            }

            if (targetPulling.Value && (KeyPressPullHarpoon() || KeyPressReleaseHarpoon()) && !KeyPressStopHarpoon())
            {
                float factorMass = IsPullingTo() ? 4f : 2f;

                if (KeyPressReleaseHarpoon())
                    targetDistance += factorMass * dt * 2f * pullSpeedMultiplier.Value;
                else if (KeyPressPullHarpoon())
                    targetDistance -= factorMass * dt * pullSpeedMultiplier.Value;

                targetDistance = Mathf.Max(targetDistance, m_minDistance + 0.5f);
            }

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

            float pullSpeed = (m_attacker.IsAttachedToShip() && (bool)m_ship) ? 10000f : m_pullSpeed;

            float num2 = Pull(RBody(), TargetPosition(), targetDistance, pullSpeed, pullForce, m_smoothDistance, IsPullingTo() ? Vector3.zero : forcePoint, m_character != null, noUpForce, useForce.Value, forcePower.Value);
            m_drainStaminaTimer += dt; float stamina = 0f;
            if (m_drainStaminaTimer > m_staminaDrainInterval && num2 > 0f)
            {
                m_drainStaminaTimer = 0f;
                if (!attachedShipStamina.Value || IsPullingTo() || !m_attacker.IsAttachedToShip())
                {
                    stamina = m_staminaDrain * num2 * (IsPullingTo() ? 10f : Mass() > 999 ? 20f : 10f + 20f * pullForce); // Mathf.Clamp(Mathf.Sqrt(mass), 10f, 30f));
                    m_attacker.UseStamina(stamina);
                }
            }


            if (deepLoggingEnabled.Value) LogInfo($"dist: {distance,-5:F3} " +
                                                  $"targetDist: {targetDistance,-5:F3} " +
                                                  $"force: {pullForce,-5:F3} " +
                                                  $"dt: {num2,-5:F3} " +
                                                  $"stam: {stamina,-5:F3} " +
                                                  $"break: {distance - targetDistance,-5:F3} < {m_breakDistance}");

        }

        public override bool IsDone()
        {
            if (base.IsDone())
                return true;

            if (m_broken)
                return true;

            if (!IsPullingTo() && (m_nview == null || !m_nview.IsValid()))
                return true;

            if (!m_attacker)
                return true;

            if (m_time > timeBeforeStop.Value && (KeyPressStopHarpoon() || m_attacker.IsBlocking()))
            {
                HarpoonMessage("$msg_harpoon_released");
                return true;
            }

            if (m_attacker.IsDead() || m_attacker.IsTeleporting() || m_attacker.InCutscene() || m_attacker.IsEncumbered())
                return true;

            if (IsPullingTo() && m_attacker.IsAttached())
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            if (Ship.GetLocalShip() != null && Ship.GetLocalShip() == m_ship)
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            if (IsPullingTo() && !pullUnderWater.Value && TargetPosition().y < ZoneSystem.instance.m_waterLevel)
            {
                m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return true;
            }

            return false;
        }

        public override void Stop()
        {
            base.Stop();

            if (targetLeviathan.Value && targetHarpooned != null && (bool)targetHarpooned.GetComponent<Leviathan>() && targetHarpooned.TryGetComponent(out Rigidbody rbody) && rbody.isKinematic == false)
            {
                rbody.isKinematic = false;

                if (targetHarpooned.TryGetComponent(out ZSyncTransform zSyncTransform))
                    zSyncTransform.m_isKinematicBody = rbody.isKinematic;
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

        public static bool LocalPlayerHarpooned()
        {
            return Player.m_localPlayer != null && Player.m_localPlayer.GetSEMan().HaveStatusEffect(s_statusEffectHash);
        }

        private static bool KeyPressStopHarpoon()
        {
            return shortcutStop.Value.IsDown() || ZInput.GetButton("Block") || ZInput.GetButton("JoyBlock");
        }

        private static bool KeyPressReleaseHarpoon()
        {
            return targetPulling.Value && ((KeyPressPullHarpoon() && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch"))) || shortcutRelease.Value.IsPressed());
        }

        private static bool KeyPressPullHarpoon()
        {
            return targetPulling.Value && (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse") || shortcutPull.Value.IsPressed());
        }

        private static bool KeyPressPullTo()
        {
            return ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace") || shortcutPullTo.Value.IsPressed();
        }

        private static string GetHarpoonedTargetName(GameObject hitObject, Collider collider)
        {
            if (hitObject.TryGetComponent(out HoverText text))
                return text.m_text;

            if (hitObject.TryGetComponent(out ItemDrop item))
                return item.m_itemData.m_shared.m_name;

            if (hitObject.TryGetComponent<Location>(out _))
                return "$piece_lorestone";

            if (hitObject.TryGetComponent(out ResourceRoot root))
                return root.m_name;

            if (hitObject.TryGetComponent<Ship>(out _))
                return hitObject.GetComponent<Piece>().m_name;

            if (targetMessagesEnabled.Value)
            {
                string defaultName = hitObject.name;
                if (hitObject.TryGetComponent(out Piece piece))
                    return piece.m_name;

                if (hitObject.TryGetComponent(out Destructible destr))
                    defaultName = destr.name;

                if (collider.name.StartsWith("Terrain"))
                    return collider.name;

                if (defaultName.Length > 1)
                    return defaultName.ToUpper().Substring(0, 1) + defaultName.Substring(1, defaultName.IndexOf("(") - 1);

                return defaultName;
            }

            return String.Empty;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateCrouch))]
        public static class Player_UpdateCrouch_DisableCrouchOnHarpooning
        {
            private static void Prefix(Player __instance, ref bool ___m_crouchToggled)
            {
                if (Player.m_localPlayer == __instance)
                    ___m_crouchToggled = ___m_crouchToggled && !LocalPlayerHarpooned();
            }
        }
    }
}
