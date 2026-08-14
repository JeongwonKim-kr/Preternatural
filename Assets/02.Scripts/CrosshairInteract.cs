using UnityEngine;

public class CrosshairInteract : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public GameObject interactCrosshair;

    [Header("Settings")]
    public float interactDistance = 3f;

    void Update()
{
    if (playerCamera == null ||
        interactCrosshair == null)
        return;

    // Hide the interact icon while hiding.
    if (CabinetHide.AnyCabinetHidden)
    {
        if (interactCrosshair.activeSelf)
            interactCrosshair.SetActive(false);

        return;
    }

    bool show = false;

    Ray ray = playerCamera.ViewportPointToRay(
        new Vector3(0.5f, 0.5f, 0f));

    RaycastHit hit;

    if (Physics.Raycast(
        ray,
        out hit,
        interactDistance))
    {
        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        show = interactable != null;
    }

    if (interactCrosshair.activeSelf != show)
        interactCrosshair.SetActive(show);
}
}