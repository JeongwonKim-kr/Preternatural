using UnityEngine;
using UnityEngine.AI;

public class MonsterLookAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera playerCamera;
    public NavMeshAgent agent;
    public Animation monsterAnimation;

    public AnimationClip idleAnimation;
    public AnimationClip runAnimation;
    public AnimationClip jumpscareAnimation;

    [Header("Objects")]
    public GameObject objectDisabledAtStart;
    public GameObject jumpscareObject;

    [Header("Detection")]
    public float chaseDistance = 30f;
    public float lookDistance = 20f;
    public float lookStopDistance = 19f;
    public float lookAngle = 35f;
    public float attackDistance = 2f;

    [Header("Speed")]
    public float patrolSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    private int patrolIndex;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip patrolFootstep;
    public AudioClip runFootstep;

    [Header("Footstep Distance")]
    public float patrolStepDistance = 4f;
    public float runStepDistance = 3f;

    [Header("Jumpscare")]
    public AudioClip jumpscareSound;
    public MonoBehaviour playerMovement;
    public MonoBehaviour cameraMovement;

    [Header("Background Music")]
    public AudioSource backgroundMusic;

    [Header("Flashlight")]
    public FlashlightController flashlightController;
    public GameObject flashlightObject;

    private bool playerLooking;
    private bool chasing;
    private bool jumpscaring;

    private bool wasHiding;

    private Vector3 lastRunStepPosition;
    private Vector3 lastPatrolStepPosition;

    private enum MonsterState
    {
        Patrol,
        Chase,
        Jumpscare
    }

    private MonsterState currentState;


    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        lastRunStepPosition = transform.position;
        lastPatrolStepPosition = transform.position;

        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (objectDisabledAtStart != null)
            objectDisabledAtStart.SetActive(false);

        if (jumpscareObject != null)
            jumpscareObject.SetActive(false);

        if (patrolPoints != null &&
            patrolPoints.Length > 0)
        {
            patrolIndex = 0;

            agent.SetDestination(
                patrolPoints[patrolIndex].position);
        }

        ChangeState(MonsterState.Patrol);
    }


    void Update()
    {
        if (jumpscaring)
            return;

        if (player == null) return; // 멀티: 타겟 없음(전원 은신/사망 경계) — 몬스터 정지

        // =====================================================
        // PLAYER IS HIDING
        // =====================================================

        if (CabinetHide.AnyCabinetHidden)
        {
            playerLooking = false;

            // If we just entered hiding,
            // forget the chase ONCE.
            if (!wasHiding)
            {
                wasHiding = true;

                chasing = false;

                agent.isStopped = false;
                agent.speed = patrolSpeed;

                // Start patrolling.
                if (patrolPoints != null &&
                    patrolPoints.Length > 0)
                {
                    agent.SetDestination(
                        patrolPoints[patrolIndex].position);
                }

                ChangeState(
                    MonsterState.Patrol);
            }

            // IMPORTANT:
            // Don't return here.
            // Let Patrol() continue running.
        }
        else
        {
            // Player just came out of hiding.
            if (wasHiding)
            {
                wasHiding = false;

                agent.isStopped = false;
                agent.speed = patrolSpeed;
            }
        }


        float distance =
            Vector3.Distance(
                transform.position,
                player.position);


        // =====================================================
        // HIDING
        // =====================================================

        if (CabinetHide.AnyCabinetHidden)
        {
            // Keep patrolling.
            Patrol();

            Footsteps();

            return;
        }


        // =====================================================
        // NORMAL DETECTION
        // =====================================================

        CheckLooking();


        if (playerLooking &&
            distance <= lookStopDistance)
        {
            StopMonster();

            Footsteps();

            return;
        }


        // =====================================================
        // CHASE / PATROL
        // =====================================================

        if (distance <= chaseDistance)
        {
            Chase();
        }
        else
        {
            Patrol();
        }


        // =====================================================
        // ATTACK
        // =====================================================

        if (distance <= attackDistance)
        {
            // 멀티: 호스트 권위 어댑터가 붙어 있고 세션이 가동 중이면 공격을 서버 경로로 위임한다
            // (사망 처리/점프스케어 RPC/게임오버는 MonsterNetAdapter.ServerAttack이 맡는다).
            var adapter = GetComponent<Game.Net.MonsterNetAdapter>();
            if (adapter != null && Game.Net.NetLink.Online)
            {
                var victim = player != null ? player.GetComponentInParent<Game.Net.NetPlayer>() : null;
                if (victim != null)
                {
                    adapter.ServerAttack(victim);
                    return;
                }
            }

            Jumpscare();
        }


        Footsteps();
    }


    // =========================================================
    // PATROL
    // =========================================================

    void Patrol()
    {
        chasing = false;

        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            ChangeState(
                MonsterState.Patrol);

            return;
        }


        if (!agent.pathPending &&
            agent.remainingDistance < 0.5f)
        {
            int newIndex = patrolIndex;

            while (newIndex == patrolIndex &&
                   patrolPoints.Length > 1)
            {
                newIndex =
                    Random.Range(
                        0,
                        patrolPoints.Length);
            }

            patrolIndex = newIndex;

            agent.SetDestination(
                patrolPoints[patrolIndex].position);
        }


        ChangeState(
            MonsterState.Patrol);
    }


    // =========================================================
    // CHASE
    // =========================================================

    void Chase()
    {
        chasing = true;

        agent.speed = runSpeed;
        agent.isStopped = false;

        agent.SetDestination(
            player.position);

        ChangeState(
            MonsterState.Chase);
    }


    // =========================================================
    // STOP WHEN PLAYER LOOKS
    // =========================================================

    void StopMonster()
    {
        chasing = false;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (monsterAnimation != null &&
            idleAnimation != null)
        {
            monsterAnimation.CrossFade(
                idleAnimation.name,
                0.2f);
        }
    }


    // =========================================================
    // CHECK LOOKING
    // =========================================================

    void CheckLooking()
    {
        if (playerCamera == null)
        {
            playerLooking = false;
            return;
        }

        Vector3 direction =
            transform.position -
            playerCamera.transform.position;

        float distance =
            direction.magnitude;

        if (distance > lookDistance)
        {
            playerLooking = false;
            return;
        }

        float angle =
            Vector3.Angle(
                playerCamera.transform.forward,
                direction);

        if (angle > lookAngle)
        {
            playerLooking = false;
            return;
        }

        RaycastHit hit;

        if (Physics.Raycast(
            playerCamera.transform.position,
            direction.normalized,
            out hit,
            lookDistance))
        {
            if (hit.transform.name ==
                "MonsterPart1")
            {
                playerLooking = true;
                return;
            }
        }

        playerLooking = false;
    }


    // =========================================================
    // FOOTSTEPS
    // =========================================================

    void Footsteps()
    {
        if (audioSource == null)
            return;

        if (agent.isStopped)
            return;

        if (chasing)
        {
            float movedDistance =
                Vector3.Distance(
                    transform.position,
                    lastRunStepPosition);

            if (movedDistance >= runStepDistance)
            {
                if (runFootstep != null)
                    audioSource.PlayOneShot(
                        runFootstep);

                lastRunStepPosition =
                    transform.position;
            }

            lastPatrolStepPosition =
                transform.position;
        }
        else
        {
            float movedDistance =
                Vector3.Distance(
                    transform.position,
                    lastPatrolStepPosition);

            if (movedDistance >= patrolStepDistance)
            {
                if (patrolFootstep != null)
                    audioSource.PlayOneShot(
                        patrolFootstep);

                lastPatrolStepPosition =
                    transform.position;
            }

            lastRunStepPosition =
                transform.position;
        }
    }


    // =========================================================
    // TRIGGER
    // =========================================================

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 멀티: 이 씬에서 objectDisabledAtStart는 DeadScreenController가 붙은 오브젝트와 동일하다
            // (씬 배선 확인됨) — 온라인에서는 씬 리로드를 유발하는 레거시 사망 화면을 건너뛴다.
            if (objectDisabledAtStart != null && !Game.Net.NetLink.Online)
                objectDisabledAtStart.SetActive(true);
        }
    }


    // =========================================================
    // JUMPSCARE
    // =========================================================

    public void Jumpscare()
    {
        if (jumpscaring)
            return;

        jumpscaring = true;

        if (backgroundMusic != null)
            backgroundMusic.Stop();

        if (flashlightController != null)
            flashlightController.enabled = false;

        if (flashlightObject != null)
            flashlightObject.SetActive(false);

        // 멀티 원격 희생자 클라이언트에서는 adapter가 agent.enabled=false로 꺼둔 상태로 이 메서드를
        // 직접 호출한다 — NavMesh 밖(비활성) 에이전트에 isStopped/velocity를 대입하면 예외가 나므로 방어.
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (cameraMovement != null)
            cameraMovement.enabled = false;

        // 멀티: jumpscareObject는 이 씬에서 DeadScreenController가 붙은 오브젝트와 동일하다(씬 배선
        // 확인됨) — 온라인에서 활성화하면 씬 리로드를 유발하는 레거시 사망 화면이 떠버리므로 건너뛴다.
        // 관전 전환/게임오버는 MonsterNetAdapter + NetPlayer가 대신 처리한다.
        if (jumpscareObject != null && !Game.Net.NetLink.Online)
            jumpscareObject.SetActive(true);

        ChangeState(
            MonsterState.Jumpscare);

        if (jumpscareSound != null &&
            audioSource != null)
        {
            audioSource.PlayOneShot(
                jumpscareSound);
        }
    }


    // =========================================================
    // ANIMATION
    // =========================================================

    void ChangeState(MonsterState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        if (monsterAnimation == null)
            return;

        switch (currentState)
        {
            case MonsterState.Patrol:

                if (runAnimation != null)
                {
                    monsterAnimation.CrossFade(
                        runAnimation.name,
                        0.2f);
                }

                break;


            case MonsterState.Chase:

                if (runAnimation != null)
                {
                    monsterAnimation.CrossFade(
                        runAnimation.name,
                        0.2f);
                }

                break;


            case MonsterState.Jumpscare:

                if (jumpscareAnimation != null)
                {
                    monsterAnimation.CrossFade(
                        jumpscareAnimation.name,
                        0.1f);
                }

                break;
        }
    }
}