
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class UpDoor : MonoBehaviour, IInteractable
{
    [Header("Door")]
    public float interactDistance = 3f;
    public float openHeight = 4f;
    public float openDuration = 1f;

    [Header("Camera")]
    public Camera playerCamera;

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip openSound;

    private Vector3 closedPosition;
    private Vector3 openPosition;

    private bool isOpen = false;
    private bool isOpening = false;

    private void Start()
    {
        closedPosition = transform.localPosition;

        openPosition =
            closedPosition +
            new Vector3(
                0f,
                openHeight,
                0f
            );
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (isOpen || isOpening)
            return;

        if (playerCamera == null)
            return;

        // -----------------------------------------------------
        // E KEY
        // -----------------------------------------------------

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    // =========================================================
    // RAYCAST INTERACTION
    // =========================================================

    private void TryInteract()
    {
        Ray ray =
            new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance
        ))
        {
            // Check if the raycast hit this door
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                OnInteract();
            }
        }
    }

    // =========================================================
    // INTERACTION
    // =========================================================

    public void OnInteract()
    {
        if (isOpen || isOpening)
            return;

        StartCoroutine(OpenDoorRoutine());
    }

    // =========================================================
    // OPEN DOOR
    // =========================================================

    private IEnumerator OpenDoorRoutine()
    {
        isOpening = true;

        // -----------------------------------------------------
        // SOUND
        // -----------------------------------------------------

        if (
            audioSource != null &&
            openSound != null
        )
        {
            audioSource.PlayOneShot(openSound);
        }

        // -----------------------------------------------------
        // MOVE DOOR
        // -----------------------------------------------------

        Vector3 startPosition =
            transform.localPosition;

        float timer = 0f;

        while (timer < openDuration)
        {
            timer += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer / openDuration
                );

            // Smooth opening
            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            transform.localPosition =
                Vector3.Lerp(
                    startPosition,
                    openPosition,
                    t
                );

            yield return null;
        }

        // -----------------------------------------------------
        // GUARANTEE FINAL POSITION
        // -----------------------------------------------------

        transform.localPosition =
            openPosition;

        isOpen = true;
        isOpening = false;
    }

    // =========================================================
    // DEBUG RAY
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * interactDistance
        );
    }
}