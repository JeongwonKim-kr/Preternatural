using System.Collections;
using UnityEngine;

public class MonsterChaseTrigger : MonoBehaviour
{
    [Header("Monster")]
    public GameObject monster;
    public Transform endPoint;

    [Header("Monster Animation")]
    public Animation monsterAnimation;
    public AnimationClip walkAnimation;
    public float walkAnimationSpeed = 0.5f;

    [Header("Monster Movement")]
    public float monsterSpeed = 3.5f;

    [Header("Footsteps")]
    public AudioSource footstepAudioSource;
    public AudioClip footstepSound;
    public float footstepDistance = 3f;

    [Header("Background Music")]
    public AudioSource originalMusic;
    public AudioSource chaseMusic;
    public float musicFadeSpeed = 1f;

    [Header("Objects To Enable")]
    public GameObject[] objectsToEnable;
    public GameObject[] objectsToDisable;

    [Header("Double Door")]
    public Transform rightDoor;
    public Transform leftDoor;

    [Header("Door Movement")]
    public float moveDistance = 0.06f;
    public float openSpeed = 1f;

    [Header("Right Door Audio")]
    public AudioSource rightDoorAudioSource;
    public AudioClip rightDoorOpenSound;

    [Header("Left Door Audio")]
    public AudioSource leftDoorAudioSource;
    public AudioClip leftDoorOpenSound;

    [Header("Door Delay")]
    public float doorOpenDelay = 6f;

    private Vector3 rightDoorClosedPosition;
    private Vector3 leftDoorClosedPosition;

    private Vector3 rightDoorOpenedPosition;
    private Vector3 leftDoorOpenedPosition;

    private Vector3 lastFootstepPosition;

    private bool triggered;
    private bool doorsOpened;


    void Start()
    {
        // -----------------------------------------
        // Disable objects at the beginning
        // -----------------------------------------

        foreach (GameObject obj in objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(true);
        }


        // -----------------------------------------
        // Monster starts disabled
        // -----------------------------------------

        if (monster != null)
            monster.SetActive(false);


        // -----------------------------------------
        // Save door positions
        // -----------------------------------------

        if (rightDoor != null)
        {
            rightDoorClosedPosition =
                rightDoor.localPosition;

            rightDoorOpenedPosition =
                rightDoorClosedPosition +
                new Vector3(
                    -moveDistance,
                    0f,
                    0f);
        }


        if (leftDoor != null)
        {
            leftDoorClosedPosition =
                leftDoor.localPosition;

            leftDoorOpenedPosition =
                leftDoorClosedPosition +
                new Vector3(
                    moveDistance,
                    0f,
                    0f);
        }
    }


    // =========================================================
    // PLAYER ENTERS TRIGGER
    // =========================================================

    void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        triggered = true;

        StartCoroutine(
            StartChase());
    }


    // =========================================================
    // START CHASE
    // =========================================================

    IEnumerator StartChase()
    {
        // -----------------------------------------
        // Enable objects
        // -----------------------------------------

        foreach (GameObject obj in objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(true);
        }
                foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }


        // -----------------------------------------
        // Enable monster
        // -----------------------------------------

        if (monster != null)
        {
            monster.SetActive(true);

            lastFootstepPosition =
                monster.transform.position;
        }


        // -----------------------------------------
        // Start chase music
        // -----------------------------------------

        if (chaseMusic != null)
        {
            chaseMusic.volume = 0f;
            chaseMusic.loop = true;
            chaseMusic.Play();

            StartCoroutine(
                FadeMusic());
        }


        // -----------------------------------------
        // Start walking animation
        // -----------------------------------------

        if (monsterAnimation != null &&
            walkAnimation != null)
        {
            AnimationState state =
                monsterAnimation[
                    walkAnimation.name];

            if (state != null)
            {
                state.speed =
                    walkAnimationSpeed;

                state.time = 0f;

                monsterAnimation.Play(
                    walkAnimation.name);
            }
        }


        // -----------------------------------------
        // Start monster movement
        // -----------------------------------------

        StartCoroutine(
            MoveMonster());


        // -----------------------------------------
        // Wait 7 seconds
        // -----------------------------------------

        yield return new WaitForSeconds(
            doorOpenDelay);


        // -----------------------------------------
        // Open doors
        // -----------------------------------------

        StartCoroutine(
            OpenDoors());
    }


    // =========================================================
    // MONSTER MOVEMENT
    // =========================================================

    IEnumerator MoveMonster()
    {
        if (monster == null ||
            endPoint == null)
            yield break;


        Transform monsterTransform =
            monster.transform;


        while (Vector3.Distance(
            monsterTransform.position,
            endPoint.position) > 0.05f)
        {
            // Move toward endpoint
            monsterTransform.position =
                Vector3.MoveTowards(
                    monsterTransform.position,
                    endPoint.position,
                    monsterSpeed *
                    Time.deltaTime);


            // Face movement direction
            Vector3 direction =
                endPoint.position -
                monsterTransform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        direction);

                monsterTransform.rotation =
                    Quaternion.Slerp(
                        monsterTransform.rotation,
                        targetRotation,
                        10f *
                        Time.deltaTime);
            }


            // Footsteps
            PlayFootsteps();


            yield return null;
        }


        monsterTransform.position =
            endPoint.position;
    }


    // =========================================================
    // FOOTSTEPS
    // =========================================================

    void PlayFootsteps()
    {
        if (footstepAudioSource == null ||
            footstepSound == null ||
            monster == null)
            return;


        float distance =
            Vector3.Distance(
                monster.transform.position,
                lastFootstepPosition);


        if (distance >= footstepDistance)
        {
            footstepAudioSource.PlayOneShot(
                footstepSound);

            lastFootstepPosition =
                monster.transform.position;
        }
    }


    // =========================================================
    // MUSIC
    // =========================================================

    IEnumerator FadeMusic()
    {
        float originalStartVolume = 0f;

        if (originalMusic != null)
            originalStartVolume =
                originalMusic.volume;


        float timer = 0f;


        while (timer < 1f)
        {
            timer +=
                Time.deltaTime *
                musicFadeSpeed;


            float t =
                1f -
                Mathf.Pow(
                    1f - timer,
                    3f);


            // Original music fades OUT
            if (originalMusic != null)
            {
                originalMusic.volume =
                    Mathf.Lerp(
                        originalStartVolume,
                        0f,
                        t);
            }


            // Chase music fades IN
            if (chaseMusic != null)
            {
                chaseMusic.volume =
                    Mathf.Lerp(
                        0f,
                        1f,
                        t);
            }


            yield return null;
        }


        if (originalMusic != null)
            originalMusic.Stop();


        if (chaseMusic != null)
            chaseMusic.volume = 1f;
    }


    // =========================================================
    // OPEN DOUBLE DOORS
    // =========================================================IEnumerator OpenDoors()
// =========================================================
// OPEN DOUBLE DOORS
// =========================================================

IEnumerator OpenDoors()
{
    if (doorsOpened)
        yield break;

    doorsOpened = true;

    // ==========================================
    // OPENING SOUNDS
    // ==========================================

    if (rightDoorAudioSource != null &&
        rightDoorOpenSound != null)
    {
        rightDoorAudioSource.PlayOneShot(
            rightDoorOpenSound);
    }

    if (leftDoorAudioSource != null &&
        leftDoorOpenSound != null)
    {
        leftDoorAudioSource.PlayOneShot(
            leftDoorOpenSound);
    }


    // ==========================================
    // OPEN DOORS
    // ==========================================

    float timer = 0f;

    while (timer < 1f)
    {
        timer += Time.deltaTime * openSpeed;

        float t = Mathf.SmoothStep(
            0f,
            1f,
            timer);

        if (rightDoor != null)
        {
            rightDoor.localPosition =
                Vector3.Lerp(
                    rightDoorClosedPosition,
                    rightDoorOpenedPosition,
                    t);
        }

        if (leftDoor != null)
        {
            leftDoor.localPosition =
                Vector3.Lerp(
                    leftDoorClosedPosition,
                    leftDoorOpenedPosition,
                    t);
        }

        yield return null;
    }


    // ==========================================
    // MAKE SURE DOORS ARE FULLY OPEN
    // ==========================================

    if (rightDoor != null)
        rightDoor.localPosition =
            rightDoorOpenedPosition;

    if (leftDoor != null)
        leftDoor.localPosition =
            leftDoorOpenedPosition;


    // ==========================================
    // STAY OPEN
    // ==========================================

    yield return new WaitForSeconds(6f);

// ==========================================
// CLOSE DOORS + CLOSING SOUND TOGETHER
// ==========================================

if (rightDoorAudioSource != null &&
    rightDoorOpenSound != null)
{
    rightDoorAudioSource.PlayOneShot(
        rightDoorOpenSound);
}

if (leftDoorAudioSource != null &&
    leftDoorOpenSound != null)
{
    leftDoorAudioSource.PlayOneShot(
        leftDoorOpenSound);
}

timer = 0f;

while (timer < 1f)
{
    timer += Time.deltaTime * openSpeed;

    float t = Mathf.SmoothStep(
        0f,
        1f,
        timer);

    if (rightDoor != null)
    {
        rightDoor.localPosition =
            Vector3.Lerp(
                rightDoorOpenedPosition,
                rightDoorClosedPosition,
                t);
    }

    if (leftDoor != null)
    {
        leftDoor.localPosition =
            Vector3.Lerp(
                leftDoorOpenedPosition,
                leftDoorClosedPosition,
                t);
    }

    yield return null;
}
}
}