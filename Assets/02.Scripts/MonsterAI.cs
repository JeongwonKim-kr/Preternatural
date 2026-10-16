
using UnityEngine;
using UnityEngine.AI;

public class MonsterAI : MonoBehaviour
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
    public float attackDistance = 3f;

    [Header("Vision Detection")]
    [Tooltip("Assign the monster's main body collider here.")]
    public Collider monsterCollider;

    [Tooltip("Layers that can block the player's vision.")]
    public LayerMask visionBlockingLayers = Physics.DefaultRaycastLayers;

    [Tooltip("If true, the monster freezes whenever visible and watched.")]
    public bool freezeWhenWatched = true;

    [Header("Speed")]
    public float patrolSpeed = 7f;
    public float runSpeed = 8f;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    private int patrolIndex;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip patrolFootstep;
    public AudioClip runFootstep;

    [Header("Footstep Distance")]
    public float patrolStepDistance = 5f;
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

    [Header("Animation Movement Threshold")]
    public float movementThreshold = 1f;

    [Header("Debug")]
    public bool showVisionDebug = true;

    private bool playerLooking;
    private bool chasing;
    private bool jumpscaring;
    private bool wasHiding;

    private Vector3 lastRunStepPosition;
    private Vector3 lastPatrolStepPosition;
[Header("Wide Cornering")]
public float cornerLookAhead = 3f;
public float cornerTurnDistance = 2f;
    private AnimationClip currentlyPlayingClip;

    private enum MonsterState
    {
        Patrol,
        Chase,
        Jumpscare
    }

    private MonsterState currentState;

    // =====================================================
    // START
    // =====================================================

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (monsterAnimation == null)
            monsterAnimation = GetComponent<Animation>();

        if (agent == null)
        {
            Debug.LogError(
                "MonsterAI: NavMeshAgent is missing!", this);

            enabled = false;
            return;
        }

        if (monsterCollider == null)
        {
            Collider[] colliders =
                GetComponentsInChildren<Collider>();

            foreach (Collider col in colliders)
            {
                if (col != null && !col.isTrigger)
                {
                    monsterCollider = col;
                    break;
                }
            }
        }

        lastRunStepPosition = transform.position;
        lastPatrolStepPosition = transform.position;

        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (objectDisabledAtStart != null)
            objectDisabledAtStart.SetActive(false);

        if (jumpscareObject != null)
            jumpscareObject.SetActive(false);

        RegisterAnimationClip(idleAnimation);
        RegisterAnimationClip(runAnimation);
        RegisterAnimationClip(jumpscareAnimation);

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = 0;

            if (patrolPoints[patrolIndex] != null)
            {
                agent.SetDestination(
                    patrolPoints[patrolIndex].position);
            }
        }

        currentState = MonsterState.Patrol;
        ChangeState(MonsterState.Patrol);
    }

    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        if (jumpscaring)
            return;

        if (player == null || agent == null)
            return;

        // PLAYER IS HIDING
        if (CabinetHide.AnyCabinetHidden)
        {
            playerLooking = false;

            if (!wasHiding)
            {
                wasHiding = true;
                chasing = false;

                agent.isStopped = false;
                agent.speed = patrolSpeed;

                SetNextPatrolDestination();

                ChangeState(MonsterState.Patrol);
            }

            Patrol();
            Footsteps();
            UpdateMovementAnimation();
            return;
        }
        else if (wasHiding)
        {
            wasHiding = false;

            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position);

        // CHECK VISION BEFORE CHASE OR ATTACK.
        CheckLooking();

        // WATCHING THE MONSTER TAKES PRIORITY.
        if (freezeWhenWatched && playerLooking)
        {
            StopMonster();

            Footsteps();
            UpdateMovementAnimation();
            return;
        }

        // CHASE OR PATROL.
        if (distance <= chaseDistance)
        {
            Chase();
        }
        else
        {
            Patrol();
        }

        // ATTACK ONLY IF NOT CURRENTLY WATCHED.
        if (distance <= attackDistance)
        {
            Jumpscare();
            return;
        }

        Footsteps();
        UpdateMovementAnimation();
    }

    // =====================================================
    // VISION DETECTION
    // =====================================================

    void CheckLooking()
    {
        playerLooking = false;

        if (playerCamera == null)
        {
            if (showVisionDebug)
                Debug.LogWarning(
                    "MonsterAI: Player Camera is not assigned.",
                    this);

            return;
        }

        Vector3 cameraPosition =
            playerCamera.transform.position;

        Vector3 cameraForward =
            playerCamera.transform.forward;

        // Try the main collider first.
        Vector3 targetPoint = transform.position;

        if (monsterCollider != null)
        {
            targetPoint = monsterCollider.bounds.center;
        }
        else
        {
            Collider[] colliders =
                GetComponentsInChildren<Collider>();

            foreach (Collider col in colliders)
            {
                if (col != null && !col.isTrigger)
                {
                    targetPoint = col.bounds.center;
                    break;
                }
            }
        }

        Vector3 direction = targetPoint - cameraPosition;
        float distance = direction.magnitude;

        if (distance <= 0.001f || distance > lookDistance)
            return;

        direction /= distance;

        // Check whether the camera is aimed at the monster.
        float angle = Vector3.Angle(
            cameraForward,
            direction);

        if (angle > lookAngle)
            return;

        // Ignore the player's own colliders when checking
        // line of sight. RaycastAll lets us find the first
        // relevant object along the ray.
        RaycastHit[] hits = Physics.RaycastAll(
            cameraPosition,
            direction,
            distance + 0.1f,
            visionBlockingLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;

            // Ignore the player and its child colliders.
            if (hitTransform == player ||
                hitTransform.IsChildOf(player))
            {
                continue;
            }

            // If the ray reaches the monster, it is visible.
            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                playerLooking = true;

                if (showVisionDebug)
                {
                    Debug.DrawLine(
                        cameraPosition,
                        hit.point,
                        Color.green);

                    Debug.DrawRay(
                        cameraPosition,
                        direction * distance,
                        Color.green);
                }

                return;
            }

            // Any other collider blocks vision.
            // Make sure visionBlockingLayers includes
            // the environment and monster layers.
            if (showVisionDebug)
            {
                Debug.DrawLine(
                    cameraPosition,
                    hit.point,
                    Color.red);
            }

            return;
        }

        // If the target point is unobstructed but its collider
        // was not hit, check whether the collider bounds are
        // close enough to the viewing ray.
        if (monsterCollider != null)
        {
            Ray ray = new Ray(cameraPosition, direction);

            if (monsterCollider.Raycast(
                ray,
                out RaycastHit monsterHit,
                distance + 0.1f))
            {
                playerLooking = true;

                if (showVisionDebug)
                {
                    Debug.DrawLine(
                        cameraPosition,
                        monsterHit.point,
                        Color.green);
                }
            }
        }
    }

    // =====================================================
    // STOP WHEN WATCHED
    // =====================================================

    void StopMonster()
    {
        chasing = false;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        PlayClip(idleAnimation, 0.2f);
    }

    // =====================================================
    // PATROL
    // =====================================================

    void Patrol()
    {
        chasing = false;

        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            ChangeState(MonsterState.Patrol);
            return;
        }

        if (!agent.pathPending &&
            (!agent.hasPath ||
             agent.remainingDistance < 0.5f))
        {
            SetNextPatrolDestination();
        }

        ChangeState(MonsterState.Patrol);
    }

    void SetNextPatrolDestination()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
            return;

        int validPointCount = 0;

        foreach (Transform point in patrolPoints)
        {
            if (point != null)
                validPointCount++;
        }

        if (validPointCount == 0)
            return;

        int newIndex = patrolIndex;

        // Find a valid patrol point.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            newIndex = Random.Range(0, patrolPoints.Length);

            if (patrolPoints[newIndex] != null &&
                (patrolPoints.Length == 1 ||
                 newIndex != patrolIndex))
            {
                break;
            }
        }

        if (patrolPoints[newIndex] == null)
            return;

        patrolIndex = newIndex;

        agent.SetDestination(
            patrolPoints[patrolIndex].position);
    }

    // =====================================================
    // CHASE
    // =====================================================

    void Chase()
    {
        chasing = true;

        agent.speed = runSpeed;
        agent.isStopped = false;

        agent.SetDestination(player.position);

        ChangeState(MonsterState.Chase);
    }

    // =====================================================
    // FOOTSTEPS
    // =====================================================

    void Footsteps()
    {
        if (audioSource == null || agent == null)
            return;

        if (agent.isStopped)
            return;

        if (agent.velocity.magnitude < movementThreshold)
            return;

        if (chasing)
        {
            float movedDistance = Vector3.Distance(
                transform.position,
                lastRunStepPosition);

            if (movedDistance >= runStepDistance)
            {
                if (runFootstep != null)
                    audioSource.PlayOneShot(runFootstep);

                lastRunStepPosition = transform.position;
            }

            lastPatrolStepPosition = transform.position;
        }
        else
        {
            float movedDistance = Vector3.Distance(
                transform.position,
                lastPatrolStepPosition);

            if (movedDistance >= patrolStepDistance)
            {
                if (patrolFootstep != null)
                    audioSource.PlayOneShot(patrolFootstep);

                lastPatrolStepPosition = transform.position;
            }

            lastRunStepPosition = transform.position;
        }
    }

    // =====================================================
    // TRIGGER
    // =====================================================

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (objectDisabledAtStart != null)
                objectDisabledAtStart.SetActive(true);
        }
    }

    // =====================================================
    // JUMPSCARE
    // =====================================================

void ApplyWideCornering()
{
    if (agent == null || agent.isStopped ||
        !agent.hasPath || agent.pathPending)
        return;

    Vector3[] corners = agent.path.corners;

    if (corners.Length < 3)
        return;

    Vector3 position = transform.position;
    position.y = 0f;

    // Find the next corner ahead of the monster.
    int nextCorner = 1;

    for (int i = 1; i < corners.Length; i++)
    {
        Vector3 cornerPosition = corners[i];
        cornerPosition.y = 0f;

        if (Vector3.Distance(position, cornerPosition) >
            cornerTurnDistance)
        {
            nextCorner = i;
            break;
        }
    }

    // Aim beyond the corner instead of directly at it.
    int targetCorner = Mathf.Min(
        nextCorner + 1,
        corners.Length - 1
    );

    Vector3 target = corners[targetCorner];

    Vector3 direction = target - transform.position;
    direction.y = 0f;

    if (direction.sqrMagnitude > 0.01f)
    {
        agent.updateRotation = false;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            agent.angularSpeed * Time.deltaTime
        );
    }
}
    void Jumpscare()
    {
        if (jumpscaring)
            return;

        jumpscaring = true;
        chasing = false;

        if (backgroundMusic != null)
            backgroundMusic.Stop();

        if (flashlightController != null)
            flashlightController.enabled = false;

        if (flashlightObject != null)
            flashlightObject.SetActive(false);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (cameraMovement != null)
            cameraMovement.enabled = false;

        if (jumpscareObject != null)
            jumpscareObject.SetActive(true);

        ChangeState(MonsterState.Jumpscare);

        if (jumpscareSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }
    }

    // =====================================================
    // REGISTER ANIMATION CLIPS
    // =====================================================

    void RegisterAnimationClip(AnimationClip clip)
    {
        if (monsterAnimation == null || clip == null)
            return;

        if (monsterAnimation.GetClip(clip.name) == null)
        {
            monsterAnimation.AddClip(clip, clip.name);
        }
    }

    // =====================================================
    // PLAY ANIMATION CLIP
    // =====================================================

    void PlayClip(AnimationClip clip, float fadeTime)
    {
        if (monsterAnimation == null || clip == null)
            return;

        RegisterAnimationClip(clip);

        if (currentlyPlayingClip == clip &&
            monsterAnimation.IsPlaying(clip.name))
        {
            return;
        }

        currentlyPlayingClip = clip;

        AnimationState state = monsterAnimation[clip.name];

        if (state != null)
        {
            state.speed = 1f;
            state.wrapMode = WrapMode.Loop;
        }

        monsterAnimation.CrossFade(clip.name, fadeTime);
    }

    // =====================================================
    // ANIMATION STATE
    // =====================================================

    void ChangeState(MonsterState newState)
    {
        currentState = newState;

        if (newState == MonsterState.Jumpscare)
        {
            PlayClip(jumpscareAnimation, 0.1f);
        }
    }

    // =====================================================
    // ANIMATION BASED ON ACTUAL MOVEMENT
    // =====================================================

    void UpdateMovementAnimation()
    {
        if (monsterAnimation == null ||
            agent == null ||
            jumpscaring)
        {
            return;
        }

        if (agent.isStopped ||
            agent.velocity.magnitude < movementThreshold)
        {
            PlayClip(idleAnimation, 0.2f);
        }
        else
        {
            PlayClip(runAnimation, 0.2f);
        }
    }

    // =====================================================
    // DEBUG GIZMOS
    // =====================================================

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lookDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        if (playerCamera != null)
        {
            Vector3 origin = playerCamera.transform.position;

            Gizmos.color = playerLooking
                ? Color.green
                : Color.red;

            Vector3 target = monsterCollider != null
                ? monsterCollider.bounds.center
                : transform.position;

            Gizmos.DrawLine(origin, target);
        }
    }
}