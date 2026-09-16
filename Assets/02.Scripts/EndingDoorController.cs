
using UnityEngine;

public class EndingDoorController : MonoBehaviour
{
    [Header("References")]
    public Transform door;
    public Transform endpoint;

    [Header("Door Movement")]
    public float openSpeed = 3.5f;

    [Header("Audio")]
    public AudioSource doorAudioSource;
    public AudioClip doorOpenSound;

    private bool opening = false;
    private bool opened = false;

    private void OnTriggerEnter(Collider other)
    {
        if (opened || opening)
            return;

        // Make sure the object touching the trigger is the player
        if (!other.CompareTag("Player"))
            return;

        opening = true;

        // Play door sound once
        if (doorAudioSource != null && doorOpenSound != null)
        {
            doorAudioSource.PlayOneShot(doorOpenSound);
        }
    }

    private void Update()
    {
        if (!opening || door == null || endpoint == null)
            return;

        // Move the door toward the endpoint
        door.position = Vector3.MoveTowards(
            door.position,
            endpoint.position,
            openSpeed * Time.deltaTime
        );

        // Stop when the endpoint is reached
        if (Vector3.Distance(door.position, endpoint.position) < 0.01f)
        {
            door.position = endpoint.position;

            opening = false;
            opened = true;
        }
    }
}

