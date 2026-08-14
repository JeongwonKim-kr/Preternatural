using System.Collections;
using UnityEngine;

public class WoodBoard : MonoBehaviour, IInteractable
{
    [Header("Tool")]
    public ToolType requiredTool = ToolType.Crowbar;

    [Header("References")]
    public ToolHolder toolHolder;
    public BoardedDoor boardedDoor;
    public Camera playerCamera;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Animation")]
    public float moveDistance = 0.4f;
    public float rotateAmount = 25f;
    public float animationTime = 0.35f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip removeSound;

    private bool removed;

    void Update()
    {
        if (removed)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        if (!toolHolder.HasTool())
            return;

        if (toolHolder.CurrentTool() != requiredTool)
            return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        

        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        WoodBoard board =
            hit.collider.GetComponentInParent<WoodBoard>();

        if (board != this)
            return;

        StartCoroutine(RemoveBoard());
    }

    IEnumerator RemoveBoard()
    {
        removed = true;

        if (audioSource != null &&
            removeSound != null)
        {
            audioSource.PlayOneShot(removeSound);
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos =
            startPos +
            transform.forward * moveDistance;

        Quaternion endRot =
            startRot *
            Quaternion.Euler(
                rotateAmount,
                0f,
                0f);

        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime / animationTime;

            float t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    timer);

            transform.position =
                Vector3.Lerp(
                    startPos,
                    endPos,
                    t);

            transform.rotation =
                Quaternion.Slerp(
                    startRot,
                    endRot,
                    t);

            yield return null;
        }

        boardedDoor.RemoveBoard();

        Destroy(gameObject);
    }
}