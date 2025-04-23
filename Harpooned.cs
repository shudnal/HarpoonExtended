using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarpoonExtended
{
    public class Harpooned : MonoBehaviour, IMonoUpdater
    {
        private bool m_alwaysPullTo;

        //private Harpooned m_harpooned;
        private float m_maxLineSlack = 0.3f;

        private float m_minDistance = 2f;
        private float m_targetDistance;

        private GameObject m_harpoonedVFX;
        private LineConnect m_line;
        private LineRenderer m_lineRenderer;

        private Vector3 m_hitPoint;
        private Vector3 m_hitNormal;

        private ZNetView m_nview;

        private GameObject m_target;
        private string m_targetName;

        private float m_breakDistance = 15f;
        private float m_maxDistance = 50f;
        private float m_pullSpeed = 1000f;
        private float m_smoothDistance = 2f;
        private float m_staminaDrain = 0.1f;
        private float m_pullForce;
        private float m_pullSpeedMultiplier;

        private Ship m_ship;
        private BaseAI m_baseAI;

        private Character m_attacker;
        private Character m_targetCharacter;

        private Rigidbody m_rigidbody;
        private Rigidbody m_targetRigidbody;

        private float m_objectMass;

        private bool m_broken;
        private float m_timeBeforeStop;
        private bool m_onlyHorizontalForce;

        public static Dictionary<BaseAI, Harpooned> HarpoonedAI = new Dictionary<BaseAI, Harpooned>();
        public static List<GameObject> HarpoonedTargets = new List<GameObject>();
        public static List<IMonoUpdater> Instances { get; } = new List<IMonoUpdater>();

        public void Awake()
        {
            m_nview = GetComponentInParent<ZNetView>();
            m_ship = GetComponentInParent<Ship>();
            m_baseAI = GetComponentInParent<BaseAI>();
        }

        public void OnEnable()
        {
            Instances.Add(this);
        }

        public void OnDisable()
        {
            Instances.Remove(this);
        }

        public void OnDestroy()
        {
            HarpoonedTargets.Remove(m_target);
            HarpoonedAI.Remove(m_baseAI);
            UnityEngine.Object.Destroy(m_harpoonedVFX);
        }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(this);
        }

        public void CustomFixedUpdate(float deltaTime)
        {
            if (IsDone())
            {
                Destroy();
                return;
            }

            UpdateHarpoonedVFX();

            UpdateHarpoonEffect(deltaTime);
        }

        public void CustomUpdate(float deltaTime, float time)
        {
        }

        public void CustomLateUpdate(float deltaTime)
        {
        }

        private void UpdateHarpoonedVFX()
        {
            if (!(bool)m_harpoonedVFX)
            {
                m_harpoonedVFX = HarpoonedVFX.CreateEffect(TargetPosition(), transform);

                m_line = m_harpoonedVFX.GetComponent<LineConnect>();
                if (m_line)
                {
                    m_line.m_netViewPrefix = "hrpnext_1_";
                    m_line.Awake();

                    m_line.m_maxDistance = m_maxDistance;
                    m_line.m_dynamicThickness = true;
                    m_line.m_minThickness = 0.04f;
                    m_line.SetPeer(m_target.GetComponentInParent<ZNetView>());

                    if (m_target.TryGetComponent<Turret>(out _))
                        m_line.m_childObject = "Neck";

                    m_lineRenderer = m_harpoonedVFX.GetComponent<LineRenderer>();
                    m_lineRenderer.transform.position = m_hitPoint;
                }
            }
        }

        internal static bool IsValidTarget(GameObject attacker, GameObject hitObject)
        {
            if (!HarpoonExtended.targetBosses.Value && hitObject.TryGetComponent(out Humanoid human) && human.IsBoss())
            {
                attacker?.GetComponent<Player>()?.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return false;
            }

            return true;
        }

        internal static void SetHarpooned(GameObject attacker, GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsValidTarget(attacker, hitObject))
                return;

            bool isAlwaysPulling = IsAttackerPullingToTarget(attacker, hitObject);

            GameObject harpoonedObject = isAlwaysPulling ? attacker : hitObject;

            HarpoonExtended.LogInfo($"SetHarpooned {isAlwaysPulling} {attacker} {hitObject} {hitPoint} {hitNormal}");
            if (harpoonedObject.TryGetComponent<Harpooned>(out _))
                return;

            Harpooned harpooned = harpoonedObject.AddComponent<Harpooned>();
            harpooned.m_alwaysPullTo = isAlwaysPulling;
            harpooned.m_target = isAlwaysPulling ? hitObject : attacker;
            harpooned.m_hitNormal = hitNormal;
            harpooned.m_hitPoint = hitObject.TryGetComponent(out Character hitCharacter) ? Vector3.Lerp(hitPoint, hitCharacter.GetCenterPoint(), 0.5f) : hitPoint;

            if (hitObject.TryGetComponent<ItemDrop>(out _))
                harpooned.m_pullSpeed = 100f;

            harpooned.m_targetDistance = Vector3.Distance(hitPoint, attacker.transform.position);
            harpooned.m_attacker = attacker.GetComponent<Character>();
            harpooned.m_targetName = HarpoonExtended.GetHarpoonedTargetName(hitObject);

            harpooned.m_rigidbody = hitObject.GetComponent<Rigidbody>() ?? hitObject.GetComponentInChildren<Rigidbody>();
            harpooned.m_targetRigidbody = attacker.GetComponent<Rigidbody>() ?? attacker.GetComponentInChildren<Rigidbody>();

            if (hitObject.TryGetComponent<Leviathan>(out _))
                harpooned.m_minDistance = 20f;  // Just in case because colliding with Levi will launch you in the sky
            else if (harpooned.m_onlyHorizontalForce = (hitObject.TryGetComponent(out harpooned.m_ship) || (bool)hitObject.GetComponent("ShipMan")))
                harpooned.m_minDistance = HarpoonExtended.minDistanceShip.Value;
            else if (hitObject.TryGetComponent<Character>(out _))
                harpooned.m_minDistance = HarpoonExtended.minDistanceCreature.Value;
            else if (hitObject.TryGetComponent<ItemDrop>(out _))
                harpooned.m_minDistance = HarpoonExtended.minDistanceItem.Value;
            else if (isAlwaysPulling)
                harpooned.m_minDistance = HarpoonExtended.minDistancePullToTarget.Value;
            else
                harpooned.m_minDistance = HarpoonExtended.minDistancePullToPlayer.Value;

            harpooned.Initialize();
        }

        private static bool IsAttackerPullingToTarget(GameObject attacker, GameObject hitObject)
        {
            if (attacker == Player.m_localPlayer.gameObject && HarpoonExtended.IsKeyPressPullTo())
            {
                LogDeepInfo(attacker, "Pull player to target intentional");
                return true; 
            }

            if ((bool)hitObject.GetComponent<RandomFlyingBird>())
            {
                // Bird doesn't have rigidbody but is not stational
                LogDeepInfo(attacker, "Pull to bird");
                return true;
            }

            var objectRbody = hitObject.GetComponent<Rigidbody>() ?? hitObject.GetComponentInChildren<Rigidbody>();
            if (!(bool)objectRbody)
            {
                // If the target has no rigidbody we should pull to it - set stational target hit point
                LogDeepInfo(attacker, "Pull to object");
                return true;
            }
            
            if (objectRbody.isKinematic)
            {
                // If target has kinematic rigidbody we should pull to it - set stational target hit point
                LogDeepInfo(attacker, "Pull to kinematic rigidbody");
                return true;
            }

            /*if (hitObject.GetComponent<Character>() is Character characterTarget && characterTarget.IsAttached())
            {
                // You can't move attached Character
                LogDeepInfo(attacker, "Can't pull attached");
                return true;
            }

            if (hitObject.GetComponent<Ship>() is Ship shipTarget) 
            {
                if (shipTarget.HaveControllingPlayer())
                {
                    // You can't move already moving ship
                    LogDeepInfo(attacker, "Can't pull already controlled ship");
                    return true;
                }

                float objectMass = CalculateHitObjectMass(hitObject);
                if (shipTarget == null && objectMass > HarpoonExtended.maxBodyMassToPull.Value)
                {
                    LogDeepInfo(attacker, $"Can't pull object {objectRbody} with mass {objectMass} more that {HarpoonExtended.maxBodyMassToPull.Value}");
                    return true;
                }
            }

            if (hitObject.TryGetComponent(out Vagon vagon) && vagon.InUse())
            {
                // You can't move already moving vagon
                LogDeepInfo(attacker, "Can't pull already moving vagon");
                return true;
            }*/

            if (!(bool)hitObject.GetComponent<ZSyncTransform>())
            {
                LogDeepInfo(attacker, "Can't pull not netsynchronized object");
                return true;
            }
            /*
            var m_nview = hitObject.GetComponent<ZNetView>();
            if (m_nview.IsOwner())
                LogDeepInfo(attacker, "Move owned");
            else
            {
                // screw it take ownership and move
                LogDeepInfo(attacker, "Claim ownership and movе");
                m_nview.ClaimOwnership();
            }*/

            return false;
        }

        internal void Initialize()
        {
            if (m_target)
                HarpoonedTargets.Add(m_target);

            if (m_baseAI)
                HarpoonedAI.Add(m_baseAI, this);

            m_breakDistance = HarpoonExtended.breakDistance.Value;
            m_maxDistance = HarpoonExtended.maxDistance.Value;
            m_staminaDrain = 0.1f * HarpoonExtended.drainStamina.Value;
            m_pullSpeed = HarpoonExtended.pullSpeed.Value;
            m_smoothDistance = HarpoonExtended.smoothDistance.Value;
            m_pullForce = HarpoonExtended.pullForceMultiplier.Value;
            m_pullSpeedMultiplier = HarpoonExtended.pullSpeedMultiplier.Value;

            m_targetCharacter = m_target?.GetComponent<Character>();
            m_timeBeforeStop = HarpoonExtended.timeBeforeStop.Value;

            UpdateObjectMass();
        }

        public bool IsPullingTo()
        {
            return m_alwaysPullTo || m_nview == null || !m_nview.IsOwner() || HarpoonExtended.alwaysPullTo.Value;
        }

        public Rigidbody RBody()
        {
            return IsPullingTo() ? m_targetRigidbody : m_rigidbody;
        }

        public float Mass()
        {
            return IsPullingTo() ? m_targetRigidbody.mass + GetInventoryWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value : m_objectMass;
        }

        public float GetInventoryWeight() => m_attacker is Humanoid human ? human.GetInventory().GetTotalWeight() : 0f;

        public Vector3 TargetPosition()
        {
            return IsPullingTo() ? m_lineRenderer.transform.position : m_target.transform.position;
        }

        public void UpdateHarpoonEffect(float dt)
        {
            if (m_timeBeforeStop > 0)
                m_timeBeforeStop -= dt;

            float distance = Vector3.Distance(TargetPosition(), RBody().transform.position);

            if (distance < m_minDistance)
            {
                //HarpoonMessage("$msg_harpoon_released");
                Destroy();// Harpooned("Too close");
                return;
            }

            Vector3 forcePoint = m_lineRenderer.transform.position;

            float pullForce;
            if (m_alwaysPullTo)
                pullForce = 1f;
            else if (Mass() > 999)
                pullForce = 1f;
            else if (Mass() <= 1f)
                pullForce = 0.05f * m_pullForce;
            else
                pullForce = (1f - (1f / Mathf.Sqrt(Mass()))) * (RBody().mass / Mass()) * m_pullForce;

            float pullSpeed = (m_attacker != null && m_attacker.IsAttachedToShip() && (bool)m_ship) ? 10000f : m_pullSpeed;

            float num2 = Pull(RBody(), TargetPosition(), m_targetDistance, pullSpeed, pullForce, m_smoothDistance, IsPullingTo() ? Vector3.zero : forcePoint, m_targetCharacter != null, m_onlyHorizontalForce, HarpoonExtended.useForce.Value, HarpoonExtended.forcePower.Value);
            /*m_drainStaminaTimer += dt; float stamina = 0f;
            if (m_drainStaminaTimer > m_staminaDrainInterval && num2 > 0f)
            {
                m_drainStaminaTimer = 0f;
                if (!attachedShipStamina.Value || IsPullingTo() || !m_attacker.IsAttachedToShip())
                {
                    stamina = m_staminaDrain * num2 * (IsPullingTo() ? 10f : Mass() > 999 ? 20f : 10f + 20f * pullForce); // Mathf.Clamp(Mathf.Sqrt(mass), 10f, 30f));
                    m_attacker.UseStamina(stamina);
                }
            }*/

            if ((bool)m_line)
            {
                m_line.SetSlack((1f - Utils.LerpStep(m_targetDistance / 2f, m_targetDistance, distance)) * m_maxLineSlack);
            }

            LogDeepInfo(data:$"dist: {distance,-5:F3} " +
                                                  $"targetDist: {m_targetDistance,-5:F3} " +
                                                  $"force: {pullForce,-5:F3} " +
                                                  $"dt: {num2,-5:F3} " +
                                                  //$"stam: {stamina,-5:F3} " +
                                                  $"break: {distance - m_targetDistance,-5:F3} < {m_breakDistance}");

            if (distance - m_targetDistance > m_breakDistance)
            {
                m_broken = true;
                //HarpoonMessage("$msg_harpoon_linebroke");
                //LogInfo("Line broke");
            }

            if (m_attacker != null && !m_attacker.HaveStamina())
            {
                m_broken = true;
                //HarpoonMessage("$msg_harpoon_released");
                //LogInfo("Stamina depleted");
            }

            if (!IsDone())
            {
                if (m_attacker == Player.m_localPlayer && HarpoonExtended.targetPulling.Value && (HarpoonExtended.KeyPressPullHarpoon() || HarpoonExtended.KeyPressReleaseHarpoon()) && !HarpoonExtended.KeyPressStopHarpoon())
                {
                    float factorMass = IsPullingTo() ? 4f : 2f;

                    if (HarpoonExtended.KeyPressReleaseHarpoon())
                        m_targetDistance += factorMass * dt * 2f * m_pullSpeedMultiplier;
                    else if (HarpoonExtended.KeyPressPullHarpoon())
                        m_targetDistance -= factorMass * dt * m_pullSpeedMultiplier;

                    m_targetDistance = Mathf.Max(m_targetDistance, m_minDistance + 0.5f);
                }
            }
        }

        public static void LogInfo(object data) => HarpoonExtended.LogInfo(data);

        public bool IsDone()
        {
            if (m_broken)
            {
                LogInfo("Is broken");
                return true;
            }

            if (m_nview == null || !m_nview.IsValid())
            {
                LogInfo("No m_nview");
                return true;
            }

            if (!m_target)
            {
                LogInfo("No target");
                return true;
            }

            if (m_attacker)
            {
                if (m_timeBeforeStop < 0f && (HarpoonExtended.KeyPressStopHarpoon() || m_attacker.IsBlocking()))
                {
                    LogInfo("$msg_harpoon_released");
                    return true;
                }

                if (m_attacker.IsDead() || m_attacker.IsTeleporting() || m_attacker.InCutscene() || m_attacker.IsEncumbered())
                {

                    return true;
                }

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

                if (IsPullingTo() && !HarpoonExtended.pullUnderWater.Value && TargetPosition().y < ZoneSystem.instance.m_waterLevel)
                {
                    m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                    return true;
                }
            }

            return false;
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
            Vector3 b = Vector3.Project(body.velocity, normalized.normalized);
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

            body.velocity = Vector3.ClampMagnitude(body.velocity, HarpoonExtended.maximumVelocity.Value);

            return num;
        }

        public float UpdateObjectMass()
        {
            if (!m_target)
                return 0f;

            float objectMass = 0f;

            m_target.GetComponentsInChildren<Rigidbody>().Do(rb => objectMass += rb.mass);

            if (m_ship == null && !m_target.GetComponent<Vagon>())
                m_target.GetComponentsInChildren<Container>().Do(cont => objectMass += cont.GetInventory().GetTotalWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value);

            if (m_target.TryGetComponent(out ItemDrop item))
                objectMass += item.m_itemData.GetWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value;

            return objectMass;
        }

        internal static void LogDeepInfo(GameObject attacker = null, object data = null)
        {
            if (!HarpoonExtended.deepLoggingEnabled.Value)
                return;

            HarpoonExtended.LogInfo(attacker ? $"{Utils.GetPrefabName(attacker)}: {data}" : data);
        }
    }
}
