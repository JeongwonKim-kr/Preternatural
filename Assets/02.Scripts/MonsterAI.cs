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

    // 최종 리뷰 Critical 1(성능): 매 프레임 GetComponent 호출 대신 Start()에서 1회 캐시.
    private Game.Net.MonsterNetAdapter _netAdapter;

    // 최종 리뷰 Critical 1: 몬스터 GO는 NGO 스폰 요건상 항상 활성 상태로 씬에 저장된다(더 이상
    // GameObject.SetActive로 "아직 등장 안 함"을 표현할 수 없다). 대신 이 필드가 그 상태를 대신한다 —
    // false면 시각(렌더러)·충돌(콜라이더) 모두 감추고 Update()의 AI 로직도 정지한다. 기본값은 true로
    // 둬서(이 스크립트를 쓰는 다른 씬/프리팹이 있어도) 어댑터가 없는 경로는 기존 그대로 항상 깨어있다 —
    // MonsterNetAdapter가 자신의 Awake()에서 이 GameScene 인스턴스만 명시적으로 false로 되돌린다.
    public bool awake = true;

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

        _netAdapter = GetComponent<Game.Net.MonsterNetAdapter>();

        // objectDisabledAtStart/jumpscareObject 초기화는 awake 여부와 무관하게 항상 즉시 실행한다 —
        // 원래(오프라인 단일 씬) 이 초기화는 게임 시작과 동시에 일어났다(DeathScreenController가 씬에
        // 활성 상태로 저장돼 있어도 즉시 감춰짐). MonsterNetAdapter가 있어 awake가 지연되더라도 이
        // 타이밍만은 어긋나면 안 된다 — 여기서 awake로 감싸면 온라인/지연 웨이크 시나리오에서
        // DeathScreenController 초기화가 늦어져 레이스(코루틴 충돌 등)가 생긴다(회귀 확인됨).
        if (objectDisabledAtStart != null)
            objectDisabledAtStart.SetActive(false);

        if (jumpscareObject != null)
            jumpscareObject.SetActive(false);

        if (patrolPoints != null &&
            patrolPoints.Length > 0)
        {
            patrolIndex = 0;
        }

        // awake일 때만 실제로 순찰을 시작한다 — 잠든 상태에서 agent에 목적지를 주면 Update()가 정지돼
        // 있어도 NavMeshAgent 스스로 이동을 시작해버린다(잠든 몬스터가 걸어다니는 회귀 방지).
        ApplyAwakeVisualAndMotion(awake);

        ChangeState(MonsterState.Patrol);
    }

    /// 최종 리뷰 Critical 1: awake 여부를 렌더러/콜라이더 표시와 NavMeshAgent 이동 상태에 반영한다.
    /// SetAwake/SetVisible이 공유하는 내부 로직 — 이동 관련 부분은 SetVisible에서는 건드리지 않는다.
    void ApplyAwakeVisualAndMotion(bool value)
    {
        SetVisible(value);

        if (agent == null) return;
        if (value)
        {
            agent.speed = patrolSpeed;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }
        else if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    /// 렌더러/콜라이더만 토글 — 원격 클라이언트(로컬 AI를 절대 돌리지 않는 쪽)가 표시 상태만
    /// 맞출 때 사용한다.
    public void SetVisible(bool value)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = value;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = value;
    }

    /// 호스트(또는 오프라인 단독 인스턴스) 전용 — 표시 + AI 이동을 함께 깨우거나 재운다.
    public void SetAwake(bool value)
    {
        if (awake == value) return;
        awake = value;
        ApplyAwakeVisualAndMotion(value);
    }


    void Update()
    {
        if (!awake) return; // 멀티: 서버가 아직 깨우지 않음 — AI 로직 전면 정지

        if (jumpscaring)
            return;

        if (player == null) return; // 멀티: 타겟 없음(전원 은신/사망 경계) — 몬스터 정지

        // =====================================================
        // PLAYER IS HIDING
        // =====================================================

        if (IsTargetHiding())
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

        if (IsTargetHiding())
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
            var adapter = _netAdapter;
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
    // HIDING CHECK (최종 리뷰 Important 3)
    // =========================================================

    /// CabinetHide.AnyCabinetHidden은 static이라 세션 간 오염되고 멀티에서는 프로세스 로컬이라 의미가
    /// 깨진다. 온라인이면 호스트 권위 로직이므로 현재 타겟(player)의 NetPlayer.IsHiding을 본다.
    /// 오프라인은 기존 static 그대로(회귀 없음).
    bool IsTargetHiding()
    {
        if (Game.Net.NetLink.Online)
        {
            var netPlayer = player != null ? player.GetComponentInParent<Game.Net.NetPlayer>() : null;
            return netPlayer != null && netPlayer.IsHiding.Value;
        }
        return CabinetHide.AnyCabinetHidden;
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
            if (hit.transform.GetComponentInParent<MonsterLookAI>() == this)
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
