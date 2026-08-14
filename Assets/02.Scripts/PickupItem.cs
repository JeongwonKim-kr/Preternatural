
using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Tool")]
    public ToolType toolType;

    [Header("Interaction")]
    public Camera playerCamera;
    public ToolHolder toolHolder;

    public float pickupDistance = 3f;
    public KeyCode pickupKey = KeyCode.E;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pickupSound;
    public AudioClip dropSound;

    [Header("Drop Sound")]
    public float minImpactVelocity = 1f;

    [Header("Spawn Points")]
    public Transform spawnPoint1;
    public Transform spawnPoint2;
    public Transform spawnPoint3;

    private Rigidbody rb;

    private bool pickedUp;
    private bool canPlayDropSound;

    // The spawn point this item was placed at
    private Transform currentSpawnPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        Transform[] spawnPoints =
        {
            spawnPoint1,
            spawnPoint2,
            spawnPoint3
        };

        // Remove empty spawn points
        System.Collections.Generic.List<Transform> validSpawnPoints =
            new System.Collections.Generic.List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                validSpawnPoints.Add(point);
        }

        if (validSpawnPoints.Count > 0)
        {
            currentSpawnPoint =
                validSpawnPoints[
                    Random.Range(0, validSpawnPoints.Count)
                ];

            transform.position = currentSpawnPoint.position;
            transform.rotation = currentSpawnPoint.rotation;

            // Follow the spawn point
            transform.SetParent(currentSpawnPoint, true);
        }

        // Rigidbody should not fight against the spawn point
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (pickedUp)
            return;

        if (toolHolder == null)
            return;

        if (toolHolder.HasTool())
            return;

        if (!Input.GetKeyDown(pickupKey))
            return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, pickupDistance))
            return;

        PickupItem item =
            hit.collider.GetComponentInParent<PickupItem>();

        if (item != this)
            return;

        PickUp();
    }

    void PickUp()
    {
        pickedUp = true;
        canPlayDropSound = false;

        // Stop following the spawn point
        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (audioSource != null &&
            pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        toolHolder.PickupTool(
            toolType,
            gameObject
        );
    }

    public void SetPickedUp(bool value)
    {
        pickedUp = value;

        if (!value)
        {
            canPlayDropSound = true;

            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!canPlayDropSound)
            return;

        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (collision.relativeVelocity.magnitude < minImpactVelocity)
            return;

        if (audioSource != null &&
            dropSound != null)
        {
            audioSource.PlayOneShot(dropSound);
        }

        canPlayDropSound = false;
    }
}

