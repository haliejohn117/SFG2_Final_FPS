using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                Renderer = renderer;
                MaterialIndex = index;
            }
        }

        [Header("Parameters")]
        public float SelfDestructYHeight = -20f;
        public float PathReachingRadius = 2f;
        public float OrientationSpeed = 10f;
        public float DeathDuration = 0f;

        [Header("Weapons Parameters")]
        public bool SwapToNextWeapon = false;
        public float DelayAfterWeaponSwap = 0f;

        [Header("Eye color")]
        public Material EyeColorMaterial;
        [ColorUsageAttribute(true, true)] public Color DefaultEyeColor;
        [ColorUsageAttribute(true, true)] public Color AttackEyeColor;

        [Header("Flash on hit")]
        public Material BodyMaterial;
        [GradientUsageAttribute(true)] public Gradient OnHitBodyGradient;
        public float FlashOnHitDuration = 0.5f;

        [Header("Wwise Audio")]
        [Tooltip("Wwise event played when the enemy is alerted")]
        public AK.Wwise.Event AlertedInEvent;

        [Tooltip("Wwise event played when the enemy loses the player")]
        public AK.Wwise.Event AlertOutEvent;

        [Tooltip("Wwise event played when taking damage while alerted")]
        public AK.Wwise.Event DamageEventActive;

        [Tooltip("Wwise event played when taking damage while idle/passive")]
        public AK.Wwise.Event DamageEventPassive;

        [Tooltip("Wwise event played on death")]
        public AK.Wwise.Event DeathEvent;

        [Tooltip("Wwise event played when the enemy fires a weapon")]
        public AK.Wwise.Event EnemyShootEvent;

        [Tooltip("Looping Wwise event for enemy idle/movement")]
        public AK.Wwise.Event IdleLoopEvent;
        [Tooltip("Stop IdleLoopEvent when enemy detects the player")]
        public bool StopIdleLoopOnDetect = true;

        [Header("VFX")]
        public GameObject DeathVfx;
        public Transform DeathVfxSpawnPoint;

        [Header("Loot")]
        public GameObject LootPrefab;
        [Range(0, 1)] public float DropRate = 1f;

        [Header("Debug Display")]
        public Color PathReachingRangeColor = Color.yellow;
        public Color AttackRangeColor = Color.red;
        public Color DetectionRangeColor = Color.blue;

        public UnityAction onAttack;
        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;
        public UnityAction onDamaged;

        List<RendererIndexData> m_BodyRenderers = new List<RendererIndexData>();
        MaterialPropertyBlock m_BodyFlashMaterialPropertyBlock;
        float m_LastTimeDamaged = float.NegativeInfinity;

        RendererIndexData m_EyeRendererData;
        MaterialPropertyBlock m_EyeColorMaterialPropertyBlock;

        public PatrolPath PatrolPath { get; set; }
        public GameObject KnownDetectedTarget => DetectionModule.KnownDetectedTarget;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public bool HadKnownTarget => DetectionModule.HadKnownTarget;
        public NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }

        int m_PathDestinationNodeIndex;
        EnemyManager m_EnemyManager;
        ActorsManager m_ActorsManager;
        Health m_Health;
        Actor m_Actor;
        Collider[] m_SelfColliders;
        GameFlowManager m_GameFlowManager;
        bool m_WasDamagedThisFrame;
        float m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
        int m_CurrentWeaponIndex;
        WeaponController m_CurrentWeapon;
        WeaponController[] m_Weapons;
        NavigationModule m_NavigationModule;

        EnemyMobile m_EnemyMobile;

        [SerializeField] EnemyState m_DebugStateDisplay;

        public enum EnemyState
        {
            Passive,
            Alerted
        }

        EnemyState m_CurrentState = EnemyState.Passive;


        void Start()
        {
            m_EnemyManager = FindObjectOfType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyController>(m_EnemyManager, this);

            m_ActorsManager = FindObjectOfType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, EnemyController>(m_ActorsManager, this);

            m_EnemyManager.RegisterEnemy(this);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyController>(m_Health, this, gameObject);

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, EnemyController>(m_Actor, this, gameObject);

            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            m_GameFlowManager = FindObjectOfType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, EnemyController>(m_GameFlowManager, this);

            m_EnemyMobile = GetComponent<EnemyMobile>();

            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            // Wwise event for idle sound loop
            if (IdleLoopEvent != null)
            {
                Wwise3DEmitter.PlayOnGameObject(IdleLoopEvent, gameObject);
            }

            FindAndInitializeAllWeapons();
            GetCurrentWeapon().ShowWeapon(true);

            var detectionModules = GetComponentsInChildren<DetectionModule>();
            DebugUtility.HandleErrorIfNoComponentFound<DetectionModule, EnemyController>(detectionModules.Length, this, gameObject);
            DetectionModule = detectionModules[0];
            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
            onAttack += DetectionModule.OnAttack;

            var navigationModules = GetComponentsInChildren<NavigationModule>();
            if (navigationModules.Length > 0)
            {
                m_NavigationModule = navigationModules[0];
                NavMeshAgent.speed = m_NavigationModule.MoveSpeed;
                NavMeshAgent.angularSpeed = m_NavigationModule.AngularSpeed;
                NavMeshAgent.acceleration = m_NavigationModule.Acceleration;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == EyeColorMaterial)
                        m_EyeRendererData = new RendererIndexData(renderer, i);

                    if (renderer.sharedMaterials[i] == BodyMaterial)
                        m_BodyRenderers.Add(new RendererIndexData(renderer, i));
                }
            }

            m_BodyFlashMaterialPropertyBlock = new MaterialPropertyBlock();

            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock = new MaterialPropertyBlock();
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock, m_EyeRendererData.MaterialIndex);
            }
        }

        void Update()
        {
            m_DebugStateDisplay = m_CurrentState;
            EnsureIsWithinLevelBounds();
            DetectionModule.HandleTargetDetection(m_Actor, m_SelfColliders);

            Color currentColor = OnHitBodyGradient.Evaluate((Time.time - m_LastTimeDamaged) / FlashOnHitDuration);
            m_BodyFlashMaterialPropertyBlock.SetColor("_EmissionColor", currentColor);
            foreach (var data in m_BodyRenderers)
            {
                data.Renderer.SetPropertyBlock(m_BodyFlashMaterialPropertyBlock, data.MaterialIndex);
            }

            m_WasDamagedThisFrame = false;
        }

        void StopIdleLoop()
        {
            if (IdleLoopEvent != null)
            {
                IdleLoopEvent.Stop(gameObject);
            }
        }

        void EnsureIsWithinLevelBounds()
        {
            if (transform.position.y < SelfDestructYHeight)
            {
                Destroy(gameObject);
                return;
            }
        }

        void OnLostTarget()
        {
            m_CurrentState = EnemyState.Passive;
            onLostTarget?.Invoke();

            if (AlertOutEvent != null)
            {
                Wwise3DEmitter.PlayOnGameObject(AlertOutEvent, gameObject);
            }

            // Restart idle loop if it stopped
            if (IdleLoopEvent != null)
            {
                Wwise3DEmitter.PlayOnGameObject(IdleLoopEvent, gameObject);
            }

            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock, m_EyeRendererData.MaterialIndex);
            }
        }

        void OnDetectedTarget()
        {
            m_CurrentState = EnemyState.Alerted;
            onDetectedTarget?.Invoke();

            // Stop idle loop when enemy is alerted if box is checked
            if (StopIdleLoopOnDetect)
            {
                StopIdleLoop();
            }

            // Wwise "EnemyAlerted" event
            // Not sure if this needs to be an if statement, if something breaks try removing it
            if (AlertedInEvent != null)
            {
                Wwise3DEmitter.PlayOnGameObject(AlertedInEvent, gameObject);
            }

            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", AttackEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock, m_EyeRendererData.MaterialIndex);
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            if (damageSource && !damageSource.GetComponent<EnemyController>())
            {
                DetectionModule.OnDamaged(damageSource);
                onDamaged?.Invoke();
                m_LastTimeDamaged = Time.time;

                if (!m_WasDamagedThisFrame)
                {
                    if (m_CurrentState == EnemyState.Alerted && DamageEventActive != null)
                    {
                        DamageEventActive.Post(gameObject);
                    }
                    else if (m_CurrentState == EnemyState.Passive && DamageEventPassive != null)
                    {
                        DamageEventPassive.Post(gameObject);
                    }
                }

                m_WasDamagedThisFrame = true;
            }
        }

        void OnDie()
        {
            // Stop looping idle sound if it's playing
            StopIdleLoop();

            if (DeathEvent != null)
            {
                DeathEvent.Post(gameObject);
            }

            var vfx = Instantiate(DeathVfx, DeathVfxSpawnPoint.position, Quaternion.identity);
            Destroy(vfx, 5f);

            m_EnemyManager.UnregisterEnemy(this);

            if (TryDropItem())
            {
                Instantiate(LootPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject, DeathDuration);
        }

        void OnEnable()
        {
            EnemyTracker.Instance?.Register(this);
        }

        void OnDestroy()
        {
            EnemyTracker.Instance?.Unregister(this);
        }

        public void OrientTowards(Vector3 lookPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude != 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
            }
        }

        public void SetPathDestinationToClosestNode()
        {
            if (PatrolPath && PatrolPath.PathNodes.Count > 0)
            {
                int closestIndex = 0;
                for (int i = 0; i < PatrolPath.PathNodes.Count; i++)
                {
                    if (PatrolPath.GetDistanceToNode(transform.position, i) <
                        PatrolPath.GetDistanceToNode(transform.position, closestIndex))
                    {
                        closestIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }

        public void SetNavDestination(Vector3 destination)
        {
            if (NavMeshAgent != null)
            {
                NavMeshAgent.SetDestination(destination);
            }
        }

        public void UpdatePathDestination(bool inverseOrder = false)
        {
            if (PatrolPath && PatrolPath.PathNodes.Count > 0)
            {
                if ((transform.position - GetDestinationOnPath()).magnitude <= PathReachingRadius)
                {
                    m_PathDestinationNodeIndex = inverseOrder
                        ? (m_PathDestinationNodeIndex - 1 + PatrolPath.PathNodes.Count) % PatrolPath.PathNodes.Count
                        : (m_PathDestinationNodeIndex + 1) % PatrolPath.PathNodes.Count;
                }
            }
        }

        public Vector3 GetDestinationOnPath()
        {
            if (PatrolPath && PatrolPath.PathNodes.Count > 0)
            {
                return PatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            return transform.position;
        }

        public bool TryDropItem()
        {
            if (DropRate == 0 || LootPrefab == null)
                return false;
            if (DropRate == 1)
                return true;

            return Random.value <= DropRate;
        }

        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            foreach (var weapon in m_Weapons)
            {
                Vector3 forward = (lookPosition - weapon.WeaponRoot.transform.position).normalized;
                weapon.transform.forward = forward;
            }
        }

        public bool TryAtack(Vector3 enemyPosition)
        {
            if (m_GameFlowManager.GameIsEnding) return false;

            OrientWeaponsTowards(enemyPosition);

            if ((m_LastTimeWeaponSwapped + DelayAfterWeaponSwap) >= Time.time)
                return false;

            bool didFire = GetCurrentWeapon().HandleShootInputs(false, true, false);


            if (didFire)
            {
                onAttack?.Invoke();
                if (EnemyShootEvent != null)
                {
                    Wwise3DEmitter.PlayOnGameObject(EnemyShootEvent, gameObject);
                }

                if (SwapToNextWeapon && m_Weapons.Length > 1)
                {
                    SetCurrentWeapon((m_CurrentWeaponIndex + 1) % m_Weapons.Length);
                }
            }

            return didFire;
        }

        void FindAndInitializeAllWeapons()
        {
            if (m_Weapons == null || m_Weapons.Length == 0)
            {
                m_Weapons = GetComponentsInChildren<WeaponController>();
                DebugUtility.HandleErrorIfNoComponentFound<WeaponController, EnemyController>(m_Weapons.Length, this, gameObject);

                foreach (var weapon in m_Weapons)
                {
                    weapon.Owner = gameObject;
                }
            }
        }

        public WeaponController GetCurrentWeapon()
        {
            FindAndInitializeAllWeapons();

            if (m_CurrentWeapon == null)
            {
                SetCurrentWeapon(0);
            }

            return m_CurrentWeapon;
        }

        void SetCurrentWeapon(int index)
        {
            m_CurrentWeaponIndex = index;
            m_CurrentWeapon = m_Weapons[index];

            m_LastTimeWeaponSwapped = SwapToNextWeapon ? Time.time : Mathf.NegativeInfinity;
        }

        public EnemyState CurrentState => m_CurrentState;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = PathReachingRangeColor;
            Gizmos.DrawWireSphere(transform.position, PathReachingRadius);

            if (DetectionModule != null)
            {
                Gizmos.color = DetectionRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.DetectionRange);

                Gizmos.color = AttackRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.AttackRange);
            }
        }
    }
}

