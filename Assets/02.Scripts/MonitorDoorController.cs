using System.Collections;
using UnityEngine;

public class MonitorDoorController : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Typing")]
    public AudioSource typingAudioSource;
    public AudioClip typingSound;

    [Header("Beep")]
    public AudioSource beepAudioSource;
    public AudioClip beepSound;

    [Header("Doors")]
    public Transform rightDoor;
    public Transform leftDoor;

    [Header("Door Movement")]
    public float moveDistance = 3.5f;
    public float openSpeed = 0.5f;

    [Header("Right Door Audio")]
    public AudioSource rightDoorAudioSource;
    public AudioClip rightDoorOpenSound;

    [Header("Left Door Audio")]
    public AudioSource leftDoorAudioSource;
    public AudioClip leftDoorOpenSound;

    private Vector3 rightDoorClosedPosition;
    private Vector3 leftDoorClosedPosition;

    private Vector3 rightDoorOpenedPosition;
    private Vector3 leftDoorOpenedPosition;

    private bool interacted;
    private bool opening;


    void Start()
    {
        // Right door starting position
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

        // Left door starting position
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


    void Update()
    {
        if (interacted || opening)
            return;

        if (playerCamera == null)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;


        Ray ray =
            playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;


        if (!Physics.Raycast(
            ray,
            out hit,
            interactDistance))
            return;


        MonitorDoorController monitor =
            hit.collider.GetComponentInParent<
                MonitorDoorController>();


        if (monitor != this)
            return;


        OnInteract();
    }


    // =========================================================
    // INTERACT
    // =========================================================

    public void OnInteract()
    {
        if (interacted || opening)
            return;

        interacted = true;

        StartCoroutine(
            KeyboardSequence());
    }


    // =========================================================
    // KEYBOARD SEQUENCE
    // =========================================================

    IEnumerator KeyboardSequence()
    {
        // Typing sound
        if (typingAudioSource != null &&
            typingSound != null)
        {
            typingAudioSource.clip =
                typingSound;

            typingAudioSource.Play();

            // Wait until typing finishes
            yield return new WaitForSeconds(
                typingSound.length);
        }


        // Beep
        if (beepAudioSource != null &&
            beepSound != null)
        {
            beepAudioSource.PlayOneShot(
                beepSound);

            yield return new WaitForSeconds(
                beepSound.length);
        }


        // Open doors
        StartCoroutine(
            OpenDoors());
    }


    // =========================================================
    // OPEN BOTH DOORS
    // =========================================================

    IEnumerator OpenDoors()
    {
        opening = true;


        // Right door sound
        if (rightDoorAudioSource != null &&
            rightDoorOpenSound != null)
        {
            rightDoorAudioSource.PlayOneShot(
                rightDoorOpenSound);
        }


        // Left door sound
        if (leftDoorAudioSource != null &&
            leftDoorOpenSound != null)
        {
            leftDoorAudioSource.PlayOneShot(
                leftDoorOpenSound);
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


            // Right door → X -3.5
            if (rightDoor != null)
            {
                rightDoor.localPosition =
                    Vector3.Lerp(
                        rightDoorClosedPosition,
                        rightDoorOpenedPosition,
                        t);
            }


            // Left door → X +3.5
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


        // Exact final positions
        if (rightDoor != null)
        {
            rightDoor.localPosition =
                rightDoorOpenedPosition;
        }


        if (leftDoor != null)
        {
            leftDoor.localPosition =
                leftDoorOpenedPosition;
        }


        opening = false;
    }
}
