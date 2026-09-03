
using UnityEngine;

public class JumpscareChase1 : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject monster;

    [Header("Jumpscare")]
    public GameObject jumpscareObject;

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
        if (jumpscareObject != null)
            jumpscareObject.SetActive(true);
    }


    // Player enters the trigger
    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        StartJumpscare();
    }


    void StartJumpscare()
    {
        if (triggered)
            return;

        triggered = true;


        // ==========================================
        // ACTIVATE OBJECT
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

        if (soundsToDisable != null)
        {
            foreach (AudioSource sound in soundsToDisable)
            {
                if (sound != null)
                {
                    sound.Stop();
                    sound.enabled = false;
                }
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
        // PLAY JUMPSCARE SOUND
        // ==========================================

        if (audioSource != null && jumpscareSound != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }
    }
}
