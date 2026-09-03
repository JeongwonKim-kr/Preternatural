using UnityEngine;

public class JumpscareChase : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject monster;

    [Header("Jumpscare")]
    public GameObject jumpscareObject;
    public Animation monsterAnimation;
    public AnimationClip jumpscareAnimation;
    public float jumpscareDistance = 3f;

    [Header("Player")]
    public MonoBehaviour playerMovement;
    public MonoBehaviour cameraMovement;

    [Header("Flashlight")]
    public FlashlightController flashlightController;
    public GameObject flashlightObject;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip jumpscareSound;
    public AudioSource backgroundMusic;

    [Header("Other Sounds To Disable")]
    public AudioSource[] soundsToDisable;

    [Header("Objects")]
    public GameObject objectToEnable;

    private bool triggered = false;


    void Start()
    {
        // Jumpscare object starts disabled
        if (jumpscareObject != null)
            jumpscareObject.SetActive(true);
    }


    void Update()
    {
        if (triggered)
            return;

        if (player == null)
            return;


        // ==========================================
        // CHECK DISTANCE
        // ==========================================

        float distance =
            Vector3.Distance(
                transform.position,
                player.position);


        if (distance <= jumpscareDistance)
        {
            StartJumpscare();
        }
    }


    // ==========================================
    // START JUMPSCARE
    // ==========================================

    void StartJumpscare()
    {
        if (triggered)
            return;

        triggered = true;


        // ==========================================
        // ENABLE JUMPSCARE OBJECT
        // ==========================================

        if (objectToEnable != null)
            objectToEnable.SetActive(true);

        if (jumpscareObject != null)
            jumpscareObject.SetActive(true);


        // ==========================================
        // STOP BACKGROUND MUSIC
        // ==========================================

        if (backgroundMusic != null)
            backgroundMusic.Stop();


        // ==========================================
        // DISABLE OTHER SOUNDS
        // ==========================================

        foreach (AudioSource sound in soundsToDisable)
        {
            if (sound != null)
            {
                sound.Stop();
                sound.enabled = false;
            }
        }


        // ==========================================
        // DISABLE FLASHLIGHT
        // ==========================================

        if (flashlightController != null)
            flashlightController.enabled = false;

        if (flashlightObject != null)
            flashlightObject.SetActive(false);


        // ==========================================
        // DISABLE PLAYER MOVEMENT
        // ==========================================

        if (playerMovement != null)
            playerMovement.enabled = false;


        // ==========================================
        // DISABLE CAMERA MOVEMENT
        // ==========================================

        if (cameraMovement != null)
            cameraMovement.enabled = false;


        // ==========================================
        // PLAY JUMPSCARE ANIMATION
        // ==========================================

        if (monsterAnimation != null &&
            jumpscareAnimation != null)
        {
            monsterAnimation.Stop();

            monsterAnimation.clip =
                jumpscareAnimation;

            monsterAnimation.Play(
                jumpscareAnimation.name);
        }


        // ==========================================
        // PLAY JUMPSCARE SOUND
        // ==========================================

        if (audioSource != null &&
            jumpscareSound != null)
        {
            audioSource.PlayOneShot(
                jumpscareSound);
        }
    }
}
