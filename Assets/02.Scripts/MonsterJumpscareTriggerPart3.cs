using System.Collections;
using UnityEngine;

public class MonsterJumpscareTrigger : MonoBehaviour
{
    [Header("Monster")]
    public Transform monster;
    public Transform endPoint;

    [Header("Monster Animation")]
    public Animation monsterAnimation;
    public AnimationClip jumpscareAnimation;
    public float animationSpeed = 3f;

    [Header("Movement")]
    public float moveSpeed = 40f;

    [Header("Monster Sound")]
    public AudioSource monsterAudioSource;
    public AudioClip monsterMoveSound;

    [Header("Impact SFX")]
    public GameObject sfxGameObject;
    public AudioSource impactAudioSource;
    public AudioClip impactSound;

    [Header("Settings")]
    public float impactDelay = 0f;

    private bool triggered = false;
void Start()
    {
                if (monsterAnimation != null &&
            jumpscareAnimation != null)
        {
            monsterAnimation[jumpscareAnimation.name].speed =
                animationSpeed;

            monsterAnimation.Play(
                jumpscareAnimation.name);
        }
    }
    void OnTriggerEnter(Collider other)
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
        // ==========================================
        // PLAY JUMPSCARE ANIMATION AT 3X SPEED
        // ==========================================




        // ==========================================
        // PLAY MONSTER MOVEMENT SOUND
        // ==========================================

        if (monsterAudioSource != null &&
            monsterMoveSound != null)
        {
            monsterAudioSource.PlayOneShot(
                monsterMoveSound);
        }


        // ==========================================
        // MOVE MONSTER SUPER FAST
        // ==========================================

        if (monster != null &&
            endPoint != null)
        {
            while (Vector3.Distance(
                monster.position,
                endPoint.position) > 0.05f)
            {
                monster.position =
                    Vector3.MoveTowards(
                        monster.position,
                        endPoint.position,
                        moveSpeed *
                        Time.deltaTime);

                yield return null;
            }

            monster.position =
                endPoint.position;
        }


        // ==========================================
        // IMPACT DELAY
        // ==========================================

        if (impactDelay > 0f)
        {
            yield return new WaitForSeconds(
                impactDelay);
        }


        // ==========================================
        // IMPACT SOUND
        // ==========================================

        if (impactAudioSource != null &&
            impactSound != null)
        {
            impactAudioSource.PlayOneShot(
                impactSound);
        }
    }
}