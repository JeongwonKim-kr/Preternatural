using UnityEngine;

public class JumpscareTriggerpart1 : MonoBehaviour
{
    [Header("Sound")]
    public AudioSource soundSource;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        triggered = true;

        if (soundSource != null)
            soundSource.Play();
    }
}