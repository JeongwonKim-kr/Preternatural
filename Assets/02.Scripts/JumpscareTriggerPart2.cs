using System.Collections;
using UnityEngine;

public class JumpscareTriggerPart2 : MonoBehaviour
{
    [Header("References")]
    public Transform ghost;
    public Transform player;

    [Header("Sounds")]
    public AudioSource screamSource;
    public AudioSource hitSource;

    [Header("Movement")]
    public float rushSpeed = 50f;
    public float stopDistance = 0.4f;
    public float disappearDelay = 0.05f;

    private bool triggered = false;

    void Start()
    {
        if (ghost != null)
            ghost.gameObject.SetActive(false);

        if (screamSource != null)
            screamSource.Stop();

        if (hitSource != null)
            hitSource.Stop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        triggered = true;

        StartCoroutine(Jumpscare());
    }

    IEnumerator Jumpscare()
    {
        ghost.gameObject.SetActive(true);

        if (screamSource != null)
            screamSource.Play();

        while (Vector3.Distance(
            new Vector3(ghost.position.x, 0f, ghost.position.z),
            new Vector3(player.position.x, 0f, player.position.z))
            > stopDistance)
        {
            Vector3 target = new Vector3(
                player.position.x,
                ghost.position.y,
                player.position.z
            );

            Vector3 dir = (target - ghost.position).normalized;

            ghost.position += dir * rushSpeed * Time.deltaTime;

            ghost.LookAt(target);

            yield return null;
        }

        yield return new WaitForSeconds(disappearDelay);

        if (hitSource != null)
            hitSource.Play();

        if (ghost != null)
            ghost.gameObject.SetActive(false);

        if (screamSource != null)
            screamSource.Stop();

        if (hitSource != null && hitSource.clip != null)
            yield return new WaitForSeconds(hitSource.clip.length);

        Destroy(gameObject);
    }
}