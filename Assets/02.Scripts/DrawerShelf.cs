using System.Collections;
using UnityEngine;

public class DrawerShelf : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Positions")]
    public Transform openPosition;

    [Header("Animation")]
    public float moveSpeed = 4f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    private Vector3 closedPosition;

    private bool opened;
    private bool moving;

    Game.Net.NetToggleSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetToggleSync>();
        if (_sync != null) _sync.OnStateChanged += open => StartCoroutine(MoveDrawer(open));
    }

    void Start()
    {
        closedPosition = transform.localPosition;

        if (openPosition == null)
        {
            Debug.LogError(
                name + " is missing an Open Position reference!",
                this);
        }
    }

    void Update()
{
    if (moving)
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

    // If the player is looking at a pickup item,
    // let the PickupItem script handle the interaction.
    PickupItem item =
        hit.collider.GetComponentInParent<PickupItem>();

    if (item != null)
        return;

    DrawerShelf shelf =
        hit.collider.GetComponentInParent<DrawerShelf>();

    if (shelf != this)
        return;

    if (_sync != null) _sync.RequestSet(!_sync.State);
    else StartCoroutine(MoveDrawer(!opened)); // 어댑터 없으면 기존 경로
}

    IEnumerator MoveDrawer(bool open)
    {
        moving = true;

        if (audioSource != null)
        {
            if (open && openSound != null)
                audioSource.PlayOneShot(openSound);

            if (!open && closeSound != null)
                audioSource.PlayOneShot(closeSound);
        }

        Vector3 start = transform.localPosition;

        Vector3 end =
            open
            ? openPosition.localPosition
            : closedPosition;

        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime * moveSpeed;

            float t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    timer);

            transform.localPosition =
                Vector3.Lerp(
                    start,
                    end,
                    t);

            yield return null;
        }

        transform.localPosition = end;

        opened = open;
        moving = false;
    }
}