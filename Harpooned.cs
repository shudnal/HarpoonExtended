using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarpoonExtended
{
    public class Harpooned : MonoBehaviour, IMonoUpdater
    {
        public class TargetState
        {
            public GameObject m_gameObject;
            public ZNetView m_nview;

            public Character m_character;
            public Rigidbody m_rigidbody;

            public Ship m_ship;
            public BaseAI m_baseAI;
            public Vagon m_vagon;
            public Leviathan m_leviathan;

            public float m_objectMass;

            public ItemDrop m_itemDrop;
            public Rigidbody[] m_allRBodies;
            public Container[] m_allContainers;

            public bool IsShip {  get; private set; }

            public TargetState(GameObject gameObject)
            {
                m_gameObject = gameObject;
                Initialize();
            }

            public void Initialize()
            {
                m_nview = m_gameObject.GetComponentInParent<ZNetView>();
                m_character = m_gameObject.GetComponent<Character>();
                m_rigidbody = m_gameObject.GetComponent<Rigidbody>() ?? m_gameObject.GetComponentInChildren<Rigidbody>();

                m_ship = m_gameObject.GetComponentInParent<Ship>();
                m_baseAI = m_gameObject.GetComponentInParent<BaseAI>();
                m_vagon = m_gameObject.GetComponentInParent<Vagon>();
                m_leviathan = m_gameObject.GetComponentInParent<Leviathan>();

                m_itemDrop = m_gameObject.GetComponentInParent<ItemDrop>();
                m_allRBodies = m_gameObject.GetComponentsInParent<Rigidbody>();
                m_allContainers = m_gameObject.GetComponentsInParent<Container>();
                
                IsShip = m_ship || (bool)m_gameObject.GetComponent("ShipMan");
            }

            public float GetMass()
            {
                m_objectMass = 0f;

                m_allRBodies.Do(rb => m_objectMass += rb?.mass ?? 0);

                if (m_ship == null && m_vagon == null)
                    m_allContainers.Do(cont => m_objectMass += cont.GetInventory().GetTotalWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value);

                if (m_itemDrop)
                    m_objectMass += m_itemDrop.m_itemData.GetWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value;

                if (m_character is Humanoid human)
                    m_objectMass += human.GetInventory().GetTotalWeight() * HarpoonExtended.containerInventoryWeightMassFactor.Value;

                return m_objectMass;
            }
        }

        public class HarpoonTarget
        {
            //public KeyValuePair<int, int> m_peerID;

            public Harpooned m_harpooned;

            public GameObject m_gameObject;

            public bool m_pullToHarpoonedAlways;

            public float m_minDistance = 2f;
            public float m_targetDistance;

            public GameObject m_harpoonedVFX;
            public LineConnect m_line;
            public LineRenderer m_lineRenderer;

            public Vector3 m_hitPoint;
            public Vector3 m_hitNormal;

            public string m_targetName;

            public float m_breakDistance = 15f;
            public float m_maxDistance = 50f;
            public float m_pullSpeed = 1000f;
            public float m_smoothDistance = 2f;
            public float m_staminaDrain = 0.1f;
            public float m_pullForce;
            public float m_pullSpeedMultiplier;

            public bool m_broken;
            public float m_timeBeforeStop;
            public bool m_onlyHorizontalForce;

            public TargetState m_state;
            public Character m_attacker;

            public bool IsAlwaysPullingToHarpooned => m_pullToHarpoonedAlways || m_state.m_nview == null || HarpoonExtended.alwaysPullTo.Value;

            public bool IsPullingToHarpooned { get; private set; }

            public bool IsDynamicAlwaysPullToHarpooned()
            {
                // If harpooned character is attached to something (ship probably)
                if (m_harpooned.m_state.m_character is Character characterTarget && characterTarget.IsAttached())
                    return true;

                // If harpooned ship is controlled by other player
                if (m_harpooned.m_state.m_ship && m_harpooned.m_state.m_ship.HaveControllingPlayer())
                    return true;

                // If harpooned target mass exceeds the maximum
                if (m_harpooned.m_state.GetMass() > HarpoonExtended.maxBodyMassToPull.Value)
                    return true;

                // If harpooned vagon is in use
                if (m_harpooned.m_state.m_vagon && m_harpooned.m_state.m_vagon.InUse())
                    return true;

                // if harpooned target is more massive, TODO tweak conditions
                /*if (m_harpooned.m_state.GetMass() > m_state.GetMass())
                    return true;*/

                return false;
            }

            public HarpoonTarget(Harpooned harpooned, GameObject gameObject, Character attacker)
            {
                m_harpooned = harpooned;
                m_gameObject = gameObject;
                m_attacker = attacker;

                m_state = new TargetState(m_gameObject);

                Initialize();
            }

            public void Update(float dt)
            {
                UpdateHarpoonedVFX();
                UpdatePullingDirection();
                UpdateHarpoonEffect(dt);

                if (IsDone())
                    Destroy();
            }

            public void UpdatePullingDirection()
            {
                IsPullingToHarpooned = IsAlwaysPullingToHarpooned || IsDynamicAlwaysPullToHarpooned();
            }

            public void UpdateHarpoonedVFX()
            {
                // Create child game object in object with Harpooned component
                // Initialize line connection from hitpoint in Harpooned object to this Harpooned object
                // Line owner is Harpooned object and line peer is target object

                if (!(bool)m_harpoonedVFX)
                {
                    m_harpoonedVFX = HarpoonedVFX.CreateEffect(m_hitPoint, m_harpooned.transform);

                    m_line = m_harpoonedVFX.GetComponent<LineConnect>();
                    if (m_line)
                    {
                        m_line.m_netViewPrefix = $"hrpnext_{m_harpooned.harpoonTargets.IndexOf(this)}_";
                        m_line.Awake();

                        m_line.m_maxDistance = m_maxDistance;
                        m_line.m_dynamicThickness = true;
                        m_line.m_minThickness = 0.04f;
                        m_line.SetPeer(m_state.m_nview);

                        if (m_gameObject.TryGetComponent<Turret>(out _))
                            m_line.m_childObject = "Neck";
                        else if (m_pullToHarpoonedAlways && m_state.m_character && m_state.m_character.IsPlayer())
                            m_line.m_childObject = "RightArm";

                        m_lineRenderer = m_harpoonedVFX.GetComponent<LineRenderer>();
                    }
                }
            }

            public bool IsDone()
            {
                if (m_broken)
                {
                    LogInfo("Is broken");
                    return true;
                }

                if (m_state.m_nview != null && !m_state.m_nview.IsValid())
                {
                    LogInfo("No m_nview");
                    return true;
                }

                if (!m_state.m_gameObject)
                {
                    LogInfo("No target");
                    return true;
                }

                if (m_attacker)
                {
                    if (m_attacker == Player.m_localPlayer && m_timeBeforeStop < 0f && (HarpoonExtended.KeyPressStopHarpoon() || m_attacker.IsBlocking()))
                    {
                        LogInfo("$msg_harpoon_released");
                        return true;

                    }

                    if (m_attacker.IsDead() || m_attacker.IsTeleporting() || m_attacker.InCutscene() || m_attacker.IsEncumbered())
                    {
                        return true;
                    }
                }

                if (m_state.m_character && (m_state.m_character.IsDead() || m_state.m_character.IsTeleporting() || m_state.m_character.InCutscene()))
                    return true;

                /*if (IsPullingToHarpooned && m_attacker.IsAttached())
                {
                    m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                    return true;
                }*/

                /*if (Ship.GetLocalShip() != null && Ship.GetLocalShip() == m_state.m_ship)
                {
                    m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                    return true;
                }*/

                /*if (IsPullingToHarpooned && !HarpoonExtended.pullUnderWater.Value && TargetPosition().y < ZoneSystem.instance.m_waterLevel)
                {
                    m_attacker.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                    return true;
                }*/

                return false;
            }

            public void Destroy()
            {
                HarpoonedTargets.Remove(m_gameObject);
                UnityEngine.Object.DestroyImmediate(m_harpoonedVFX);
                m_harpooned.harpoonTargets.Remove(this);
            }

            internal void Initialize()
            {
                if (m_gameObject)
                    HarpoonedTargets[m_gameObject] = this;

                m_breakDistance = HarpoonExtended.breakDistance.Value;
                m_maxDistance = HarpoonExtended.maxDistance.Value;
                m_staminaDrain = 0.1f * HarpoonExtended.drainStamina.Value;
                m_pullSpeed = HarpoonExtended.pullSpeed.Value;
                m_smoothDistance = HarpoonExtended.smoothDistance.Value;
                m_pullForce = HarpoonExtended.pullForceMultiplier.Value;
                m_pullSpeedMultiplier = HarpoonExtended.pullSpeedMultiplier.Value;

                m_timeBeforeStop = HarpoonExtended.timeBeforeStop.Value;

                m_harpooned.harpoonTargets.Add(this);
            }

            public TargetState StateToPull => IsPullingToHarpooned ? m_state : m_harpooned.m_state;
            public TargetState StateTarget => IsPullingToHarpooned ? m_harpooned.m_state : m_state;

            public Character CharacterToPull => StateToPull.m_character;
            public Vector3 LineStart => m_lineRenderer == null || m_lineRenderer.positionCount == 0 ? Vector3.zero : m_lineRenderer.transform.TransformPoint(m_lineRenderer.GetPosition(0));
            public Vector3 LineEnd => m_lineRenderer == null || m_lineRenderer.positionCount == 0 ? Vector3.zero : m_lineRenderer.transform.TransformPoint(m_lineRenderer.GetPosition(m_lineRenderer.positionCount - 1));
            public float LineLength => Vector3.Distance(LineStart, LineEnd);
            public void UpdateHarpoonEffect(float dt)
            {
                if (m_timeBeforeStop > 0)
                    m_timeBeforeStop -= dt;

                if (m_lineRenderer.positionCount == 0)
                    return;

                if (m_broken)
                    return;

                // If is this target is pulling to harpooned object
                // RBody to apply force (who is moving)         - that object rbody
                // Target point (direction)                     - hit point of harpooned
                // Force point to apply force (where to pull)   - this target object end of line

                // Otherwise if harpooned object is pulling to this target
                // RBody to apply force (who is moving)         - harpooned object rbody
                // Target point (direction)                     - this target object end of line
                // Force point to apply force (where to pull)   - hit point of harpooned

                Vector3 target = IsPullingToHarpooned ? LineStart : LineEnd;
                Vector3 forcePoint = IsPullingToHarpooned ? LineEnd: LineStart;

                float distance = LineLength;

                if (distance < m_minDistance)
                {
                    //HarpoonMessage("$msg_harpoon_released");
                    //Destroy();// Harpooned("Too close");
                    m_broken = true;
                    return;
                }

                float massToPull = StateToPull.GetMass();

                float pullForce;
                if (IsPullingToHarpooned)
                    pullForce = 1f;
                else if (massToPull > 999)
                    pullForce = 1f;
                else if (massToPull <= 1f)
                    pullForce = 0.05f * m_pullForce;
                else
                {
                    float mass = StateTarget.GetMass();
                    if (mass == 0f)
                        mass = massToPull;
                    
                    pullForce = (1f - (1f / Mathf.Sqrt(massToPull))) * (mass / massToPull) * m_pullForce;
                }

                float pullSpeed = (m_attacker != null && m_attacker.IsAttachedToShip()) ? 10000f : m_pullSpeed;

                float num2 = Pull(StateToPull.m_rigidbody, target, m_targetDistance, pullSpeed, pullForce, m_smoothDistance, forcePoint, StateToPull.m_character != null && !StateToPull.m_character.IsFlying(), m_onlyHorizontalForce, HarpoonExtended.useForce.Value, HarpoonExtended.forcePower.Value);
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

                LogDeepInfo(data: $"dist: {distance,-5:F3} " +
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

                /*if (!IsDone())
                {
                    if (m_attacker == Player.m_localPlayer && HarpoonExtended.targetPulling.Value && (HarpoonExtended.KeyPressPullHarpoon() || HarpoonExtended.KeyPressReleaseHarpoon()) && !HarpoonExtended.KeyPressStopHarpoon())
                    {
                    }
                }*/
            }

            public void PullLine() => ChangeDistance(-Time.fixedDeltaTime);

            public void ReleaseLine() => ChangeDistance(Time.fixedDeltaTime * 2f);

            private void ChangeDistance(float dt)
            {
                float factorMass = IsPullingToHarpooned ? 4f : 2f;

                m_targetDistance += factorMass * dt * 2f * m_pullSpeedMultiplier;

                m_targetDistance = Mathf.Max(m_targetDistance, m_minDistance + 0.5f);
            }
            
            public string GetStateString() => $"Target distance: {m_targetDistance:F2}, Break {Mathf.Clamp01((LineLength - m_targetDistance) / m_breakDistance):P0}";
        }

        public static float m_maxLineSlack = 0.3f;

        private TargetState m_state;

        private List<HarpoonTarget> harpoonTargets = new List<HarpoonTarget>();

        public static Dictionary<BaseAI, Harpooned> HarpoonedAI = new Dictionary<BaseAI, Harpooned>();
        public static Dictionary<GameObject, HarpoonTarget> HarpoonedTargets = new Dictionary<GameObject, HarpoonTarget>();
        public static List<IMonoUpdater> Instances { get; } = new List<IMonoUpdater>();

        public void Awake()
        {
            m_state = new TargetState(gameObject);
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
            HarpoonedAI.Remove(m_state.m_baseAI);
            for (int i = harpoonTargets.Count - 1; i >= 0; i--)
                harpoonTargets[i]?.Destroy();
        }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(this);
        }

        public void CustomFixedUpdate(float deltaTime)
        {
            for (int i = harpoonTargets.Count - 1; i >= 0; i--)
            {
                HarpoonTarget target = harpoonTargets[i];

                target?.Update(deltaTime);
            }

            if (harpoonTargets.Count == 0)
                Destroy();
        }

        public void CustomUpdate(float deltaTime, float time)
        {
        }

        public void CustomLateUpdate(float deltaTime)
        {
        }

        public HarpoonTarget AddHarpoonTarget(GameObject gameObject, Character attacker) => new HarpoonTarget(this, gameObject, attacker);

        internal static bool IsValidTarget(GameObject attacker, GameObject hitObject)
        {
            if (!HarpoonExtended.targetBosses.Value && hitObject.TryGetComponent(out Humanoid human) && human.IsBoss())
            {
                attacker?.GetComponent<Player>()?.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return false;
            }

            return true;
        }

        internal void Initialize()
        {
            if (m_state.m_baseAI)
                HarpoonedAI[m_state.m_baseAI] = this;
        }

        public Vector3 GetAveragePosition()
        {
            if (harpoonTargets == null || harpoonTargets.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            
            foreach (var target in harpoonTargets)
                sum += target.m_gameObject.transform.position;

            return sum / harpoonTargets.Count;
        }

        internal static void SetHarpooned(GameObject attacker, GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsValidTarget(attacker, hitObject))
                return;

            bool isAlwaysPulling = IsAttackerPullingToTarget(attacker, hitObject);

            GameObject harpoonedObject = isAlwaysPulling ? attacker : hitObject;
            GameObject harpoonTarget = isAlwaysPulling ? hitObject : attacker;

            HarpoonExtended.LogInfo($"SetHarpooned {isAlwaysPulling} {attacker} {hitObject} {hitPoint} {hitNormal}");

            if (!harpoonedObject.TryGetComponent(out Harpooned harpooned))
                harpooned = harpoonedObject.AddComponent<Harpooned>();

            HarpoonTarget target = harpooned.AddHarpoonTarget(harpoonTarget, attacker.GetComponent<Character>());

            target.m_pullToHarpoonedAlways = isAlwaysPulling;
            target.m_hitNormal = hitNormal;
            target.m_hitPoint = hitObject.TryGetComponent(out Character hitCharacter) ? Vector3.Lerp(hitPoint, hitCharacter.GetCenterPoint(), 0.1f) : hitPoint;

            if (harpooned.m_state.m_itemDrop || target.m_state.m_itemDrop)
                target.m_pullSpeed = 100f;

            target.m_targetDistance = Vector3.Distance(hitPoint, attacker.transform.position);
            target.m_targetName = HarpoonExtended.GetHarpoonedTargetName(hitObject);

            if (harpooned.m_state.m_leviathan || target.m_state.m_leviathan) // Just in case because colliding with Levi will launch you in the sky
                target.m_minDistance = 20f;
            else if (target.m_onlyHorizontalForce = target.m_state.IsShip)
                target.m_minDistance = HarpoonExtended.minDistanceShip.Value;
            else if (harpooned.m_state.m_character || target.m_state.m_character)
                target.m_minDistance = harpooned.m_state.m_character?.GetRadius() ?? 0 + target.m_state.m_character?.GetRadius() ?? 0 + HarpoonExtended.minDistanceCreature.Value;
            else if (harpooned.m_state.m_itemDrop || target.m_state.m_itemDrop)
                target.m_minDistance = HarpoonExtended.minDistanceItem.Value;
            else if (isAlwaysPulling)
                target.m_minDistance = HarpoonExtended.minDistancePullToTarget.Value;
            else
                target.m_minDistance = HarpoonExtended.minDistancePullToPlayer.Value;

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

        public static void LogInfo(object data) => HarpoonExtended.LogInfo(data);

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


        internal static void LogDeepInfo(GameObject attacker = null, object data = null)
        {
            if (!HarpoonExtended.deepLoggingEnabled.Value)
                return;

            HarpoonExtended.LogInfo(attacker ? $"{Utils.GetPrefabName(attacker)}: {data}" : data);
        }
    }
}
