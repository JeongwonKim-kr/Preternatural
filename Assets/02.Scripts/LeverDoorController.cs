using System.Collections;
using UnityEngine;

public class LeverDoorController : MonoBehaviour
{
    [System.Serializable]
    public class Lever
    {
        [Header("Lever")]
        public GameObject leverObject;
        public Transform leverHandle;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip leverSound;

        [HideInInspector]
        public bool pulled;

        [HideInInspector]
        public Quaternion closedRotation;

        [HideInInspector]
        public Quaternion pulledRotation;
    }


    [Header("Levers")]
    public Lever lever1;
    public Lever lever2;
    public Lever lever3;


    [Header("Lever Settings")]
    public float leverPullAngle = 60f;
    public float leverPullSpeed = 5f;


    [Header("Door")]
    public GameObject door;
    public float openAngle = -120f;
    public float openSpeed = 0.5f;


    [Header("Door Audio")]
    public AudioSource doorAudioSource;
    public AudioClip lockedSound;
    public AudioClip unlockSound;
    public AudioClip doorOpenSound;


    [Header("Locked Door Shake")]
    public float shakeAmount = 0.01f;
    public float shakeDuration = 0.3f;
    public float shakeSpeed = 20f;
    public float lockedCooldown = 0.6f;


    private bool doorUnlocked;
    private bool doorOpening;
    private bool doorOpened;
    private bool lockedCooldownActive;

    private Quaternion doorClosedRotation;
    private Quaternion doorOpenedRotation;


    void Start()
    {
        // =====================================================
        // LEVER 1
        // =====================================================

        SetupLever(lever1);


        // =====================================================
        // LEVER 2
        // =====================================================

        SetupLever(lever2);


        // =====================================================
        // LEVER 3
        // =====================================================

        SetupLever(lever3);


        // =====================================================
        // DOOR
        // =====================================================

        if (door != null)
        {
            doorClosedRotation =
                door.transform.localRotation;

            doorOpenedRotation =
                doorClosedRotation *
                Quaternion.Euler(
                    0f,
                    openAngle,
                    0f);
        }
    }


    void SetupLever(Lever lever)
    {
        if (lever == null ||
            lever.leverHandle == null)
            return;


        lever.closedRotation =
            lever.leverHandle.localRotation;


        lever.pulledRotation =
            lever.closedRotation *
            Quaternion.Euler(
                leverPullAngle,
                0f,
                0f);
    }


    // =========================================================
    // LEVER
    // =========================================================

    public void PullLever(int leverNumber)
    {
        if (doorOpening || doorOpened)
            return;


        if (leverNumber == 1)
        {
            PullSpecificLever(lever1);
        }
        else if (leverNumber == 2)
        {
            PullSpecificLever(lever2);
        }
        else if (leverNumber == 3)
        {
            PullSpecificLever(lever3);
        }


        CheckLevers();
    }


    void PullSpecificLever(Lever lever)
    {
        if (lever == null)
            return;


        if (lever.pulled)
            return;


        lever.pulled = true;


        // Lever sound
        if (lever.audioSource != null &&
            lever.leverSound != null)
        {
            lever.audioSource.PlayOneShot(
                lever.leverSound);
        }


        // Lever animation
        StartCoroutine(
            PullLeverAnimation(
                lever));
    }


    IEnumerator PullLeverAnimation(Lever lever)
    {
        if (lever.leverHandle == null)
            yield break;


        float timer = 0f;


        while (timer < 1f)
        {
            timer +=
                Time.deltaTime *
                leverPullSpeed;


            float t =
                1f -
                Mathf.Pow(
                    1f - timer,
                    3f);


            lever.leverHandle.localRotation =
                Quaternion.Slerp(
                    lever.closedRotation,
                    lever.pulledRotation,
                    t);


            yield return null;
        }


        lever.leverHandle.localRotation =
            lever.pulledRotation;
    }


    // =========================================================
    // CHECK ALL 3 LEVERS
    // =========================================================

    void CheckLevers()
    {
        if (!lever1.pulled)
            return;


        if (!lever2.pulled)
            return;


        if (!lever3.pulled)
            return;


        UnlockDoor();
    }


    // =========================================================
    // UNLOCK
    // =========================================================

    void UnlockDoor()
    {
        if (doorUnlocked)
            return;


        doorUnlocked = true;


        if (doorAudioSource != null &&
            unlockSound != null)
        {
            doorAudioSource.PlayOneShot(
                unlockSound);
        }
    }


    // =========================================================
    // DOOR INTERACTION
    // =========================================================

    public void InteractWithDoor()
    {
        if (doorOpening || doorOpened)
            return;


        if (!doorUnlocked)
        {
            LockedDoor();
            return;
        }


        StartCoroutine(
            OpenDoor());
    }


    // =========================================================
    // LOCKED DOOR
    // =========================================================

    void LockedDoor()
    {
        if (lockedCooldownActive)
            return;


        lockedCooldownActive = true;


        // Locked sound
        if (doorAudioSource != null &&
            lockedSound != null)
        {
            doorAudioSource.PlayOneShot(
                lockedSound);
        }


        // Shake
        StartCoroutine(
            ShakeDoor());


        // Prevent sound/shake spam
        StartCoroutine(
            LockedCooldown());
    }


    IEnumerator LockedCooldown()
    {
        yield return new WaitForSeconds(
            lockedCooldown);


        lockedCooldownActive = false;
    }


    // =========================================================
    // SHAKE DOOR
    // =========================================================

    IEnumerator ShakeDoor()
    {
        if (door == null)
            yield break;


        Vector3 originalPosition =
            door.transform.localPosition;


        float timer = 0f;


        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;


            float x =
                Mathf.Sin(
                    timer * shakeSpeed) *
                shakeAmount;


            door.transform.localPosition =
                originalPosition +
                new Vector3(
                    x,
                    0f,
                    0f);


            yield return null;
        }


        door.transform.localPosition =
            originalPosition;
    }


    // =========================================================
    // OPEN DOOR
    // =========================================================

    IEnumerator OpenDoor()
    {
        if (doorOpening ||
            doorOpened)
            yield break;


        doorOpening = true;


        // Door opening sound
        if (doorAudioSource != null &&
            doorOpenSound != null)
        {
            doorAudioSource.PlayOneShot(
                doorOpenSound);
        }


        float timer = 0f;


        while (timer < 1f)
        {
            timer +=
                Time.deltaTime *
                openSpeed;


            float t =
                1f -
                Mathf.Pow(
                    1f - timer,
                    3f);


            door.transform.localRotation =
                Quaternion.Slerp(
                    doorClosedRotation,
                    doorOpenedRotation,
                    t);


            yield return null;
        }


        door.transform.localRotation =
            doorOpenedRotation;


        doorOpened = true;
        doorOpening = false;
    }
}