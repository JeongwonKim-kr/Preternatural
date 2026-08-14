using UnityEngine;

public class LeverInteractable : MonoBehaviour, IInteractable
{
    [Header("Controller")]
    public LeverDoorController controller;

    [Header("Lever")]
    public int leverNumber;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;


    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }


    void Update()
    {
        if (controller == null)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        if (playerCamera == null)
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


        LeverInteractable leverHit =
            hit.collider.GetComponentInParent<LeverInteractable>();


        if (leverHit != this)
            return;


        OnInteract();
    }


    public void OnInteract()
    {
        if (controller == null)
            return;

        controller.PullLever(
            leverNumber);
    }
}