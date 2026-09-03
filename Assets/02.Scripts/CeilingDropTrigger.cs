using System.Collections;
using UnityEngine;

public class CeilingDropTrigger : MonoBehaviour
{
    [Header("Ceiling")]
    public Transform ceiling;

    [Header("Movement")]
    public float dropDistance = 40f;
    public float dropDuration = 28f;

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip ceilingSound;

    private Vector3 startPosition;
    private Vector3 endPosition;

    private bool triggered;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        if (ceiling != null)
        {
            startPosition =
                ceiling.localPosition;

            endPosition =
                startPosition +
                new Vector3(
                    0f,
                    -dropDistance,
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
            LowerCeiling());
    }


    // =========================================================
    // LOWER CEILING
    // =========================================================

    IEnumerator LowerCeiling()
    {
        // -----------------------------------------
        // Play sound once
        // -----------------------------------------

        if (audioSource != null &&
            ceilingSound != null)
        {
            audioSource.PlayOneShot(
                ceilingSound);
        }


        // -----------------------------------------
        // Move ceiling down
        // -----------------------------------------

        float timer = 0f;

        while (timer < dropDuration)
        {
            timer += Time.deltaTime;

            float t =
                timer / dropDuration;

            // Smooth movement
            t = Mathf.SmoothStep(
                0f,
                1f,
                t);

            if (ceiling != null)
            {
                ceiling.localPosition =
                    Vector3.Lerp(
                        startPosition,
                        endPosition,
                        t);
            }

            yield return null;
        }


        // -----------------------------------------
        // Make sure ceiling reaches final position
        // -----------------------------------------

        if (ceiling != null)
        {
            ceiling.localPosition =
                endPosition;
        }
    }
}