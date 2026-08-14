using System.Collections;
using UnityEngine;

public class BoardedDoor : MonoBehaviour, IInteractable
{
    [Header("Door")]
    public Transform door;
    public float openAngle = -90f;
    public float openSpeed = 1.5f;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Boards")]
    public int totalBoards = 3;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;

    [Header("Disable After Open")]
    public GameObject[] toolsToDisable;

    private int boardsRemoved;

    private bool opened;
    private bool opening;

    private Quaternion closedRotation;
    private Quaternion openedRotation;

    Game.Net.NetToggleSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetToggleSync>();
        if (_sync != null) _sync.OnStateChanged += _ => StartCoroutine(OpenDoor());
    }

    void Start()
    {
        closedRotation = door.localRotation;

        openedRotation =
            closedRotation *
            Quaternion.Euler(
                0f,
                0f,
                openAngle);
    }

    void Update()
    {
        if (opened || opening)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        Ray ray =
            playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f));

        RaycastHit hit;

        if (!Physics.Raycast(
            ray,
            out hit,
            interactDistance))
            return;

        BoardedDoor doorHit =
            hit.collider.GetComponentInParent<BoardedDoor>();

        if (doorHit != this)
            return;

        if (boardsRemoved < totalBoards)
            return;

        if (_sync != null) _sync.RequestSet(true);
        else StartCoroutine(OpenDoor()); // 어댑터 없으면 기존 경로
    }

    public void RemoveBoard()
    {
        boardsRemoved++;
    }

    IEnumerator OpenDoor()
    {
        opening = true;

        if (audioSource != null &&
            openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }
foreach (GameObject tool in toolsToDisable)
        {
            if (tool != null)
                tool.SetActive(false);
        }
        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime * openSpeed;

            float t =
                1f -
                Mathf.Pow(
                    1f - timer,
                    3f);

            door.localRotation =
                Quaternion.Slerp(
                    closedRotation,
                    openedRotation,
                    t);

            yield return null;
        }

        door.localRotation = openedRotation;

        

        opened = true;
        opening = false;
    }
}