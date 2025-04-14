using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;

        [Header("Wwise Audio")]
        [Tooltip("Wwise event to post when the enemy is alerted")]
        public AK.Wwise.Event EnemyAlertedEvent;

        [Tooltip("Looping movement event")]
        public AK.Wwise.Event MovementLoopEvent;

        public MinMaxFloat PitchDistortionMovementSpeed;

        public AIState AiState { get; private set; }

        EnemyController m_EnemyController;

        const string k_AnimMoveSpeedParameter = "MoveSpeed";
        const string k_AnimAttackParameter = "Attack";
        const string k_AnimAlertedParameter = "Alerted";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        void Start()
        {
            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyMobile>(m_EnemyController, this, gameObject);

            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;

            AiState = AIState.Patrol;

            // Start looping movement sound (Wwise)
            Wwise3DEmitter.PlayOnGameObject(MovementLoopEvent, gameObject);
        }

        void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_EnemyController.NavMeshAgent.velocity.magnitude;

            // Update animator speed parameter
            Animator.SetFloat(k_AnimMoveSpeedParameter, moveSpeed);

            // Optional RTPC for movement speed
            // AkSoundEngine.SetRTPCValue("MoveSpeed", moveSpeed, gameObject);
        }

        void UpdateAiStateTransitions()
        {
            switch (AiState)
            {
                case AIState.Follow:
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_EnemyController.SetNavDestination(transform.position);
                    }
                    break;

                case AIState.Attack:
                    if (!m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }
                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            switch (AiState)
            {
                case AIState.Patrol:
                    m_EnemyController.UpdatePathDestination();
                    m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationOnPath());
                    break;

                case AIState.Follow:
                    m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientWeaponsTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;

                case AIState.Attack:
                    float distanceToTarget = Vector3.Distance(
                        m_EnemyController.KnownDetectedTarget.transform.position,
                        m_EnemyController.DetectionModule.DetectionSourcePoint.position
                    );

                    if (distanceToTarget >= (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange))
                    {
                        m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.TryAtack(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
            }
        }

        void OnAttack()
        {
            Animator.SetTrigger(k_AnimAttackParameter);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            foreach (var vfx in OnDetectVfx)
            {
                vfx.Play();
            }

            // Play Wwise "EnemyAlerted" event
            Wwise3DEmitter.PlayOnGameObject(EnemyAlertedEvent, gameObject);

            Animator.SetBool(k_AnimAlertedParameter, true);
        }

        void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }

            foreach (var vfx in OnDetectVfx)
            {
                vfx.Stop();
            }

            Animator.SetBool(k_AnimAlertedParameter, false);
        }

        void OnDamaged()
        {
            if (RandomHitSparks.Length > 0)
            {
                int index = Random.Range(0, RandomHitSparks.Length);
                RandomHitSparks[index].Play();
            }

            Animator.SetTrigger(k_AnimOnDamagedParameter);
        }
    }
}