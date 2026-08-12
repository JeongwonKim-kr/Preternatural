using UnityEngine;

public class ToolHolder : MonoBehaviour
{
    [Header("Hold")]
    public Transform holdPoint;
    [Header("Options")]
public bool toolsEnabled = true;

    [Header("Movement")]
public float swayAmount = 0.03f;
public float swaySpeed = 4.5f;
public float sprintSwaySpeed = 6.5f;

    public float breatheAmount = 0.005f;
    public float breatheSpeed = 1f;

    [Header("Drop")]
    public KeyCode dropKey = KeyCode.Space;
    public float throwForce = 2f;

    private GameObject currentTool;
    private Rigidbody currentRb;
    private PickupItem currentPickup;

    private ToolType currentType = ToolType.None;

    private Vector3 holdStartPos;

    void Start()
    {
        holdStartPos = holdPoint.localPosition;
    }

    void Update()
    {
        if (!toolsEnabled)
    return;
        AnimateHoldPoint();

        if (currentTool == null)
            return;

        if (Input.GetKeyDown(dropKey))
        {
            DropTool();
        }
    }

    void AnimateHoldPoint()
{
    if (CabinetHide.AnyCabinetHidden)
    {
        holdPoint.localPosition =
            Vector3.Lerp(
                holdPoint.localPosition,
                holdStartPos,
                Time.deltaTime * 10f);

        return;
    }

    Vector3 offset = Vector3.zero;

    float move =
        Mathf.Abs(Input.GetAxisRaw("Horizontal")) +
        Mathf.Abs(Input.GetAxisRaw("Vertical"));

    bool sprinting =
        Input.GetKey(KeyCode.LeftShift) &&
        move > 0f;

    float currentSwaySpeed =
        sprinting
        ? sprintSwaySpeed
        : swaySpeed;

    if (move > 0f)
    {
        offset.x =
            Mathf.Sin(Time.time * currentSwaySpeed) *
            swayAmount;

        offset.y =
            Mathf.Cos(Time.time * currentSwaySpeed * 2f) *
            swayAmount * .5f;
    }

    offset.y +=
        Mathf.Sin(Time.time * breatheSpeed) *
        breatheAmount;

    holdPoint.localPosition =
        Vector3.Lerp(
            holdPoint.localPosition,
            holdStartPos + offset,
            Time.deltaTime * 10f);
}

    public void PickupTool(
        
        ToolType type,
        GameObject tool)
    {
        if (currentTool != null)
            return;

        currentTool = tool;
        currentType = type;

        currentPickup =
            currentTool.GetComponent<PickupItem>();

        currentRb =
            currentTool.GetComponent<Rigidbody>();

        if (currentRb != null)
        {
            currentRb.isKinematic = true;
            currentRb.useGravity = false;
            currentRb.linearVelocity = Vector3.zero;
            currentRb.angularVelocity = Vector3.zero;
        }

        Collider[] cols =
            currentTool.GetComponentsInChildren<Collider>();

        foreach (Collider c in cols)
            c.enabled = false;

        currentTool.transform.SetParent(
            holdPoint,
            false);

        currentTool.transform.localPosition =
            Vector3.zero;

        currentTool.transform.localRotation =
            Quaternion.identity;
            if (!toolsEnabled)
    return;
    }

    public void DropTool()
    {
        if (currentTool == null)
            return;

        currentTool.transform.SetParent(null, true);

        Collider[] cols =
            currentTool.GetComponentsInChildren<Collider>();

        foreach (Collider c in cols)
            c.enabled = true;

        if (currentRb != null)
        {
            currentRb.isKinematic = false;
            currentRb.useGravity = true;

            currentRb.AddForce(
                Camera.main.transform.forward *
                throwForce,
                ForceMode.Impulse);
        }

        if (currentPickup != null)
            currentPickup.SetPickedUp(false);

        currentTool = null;
        currentRb = null;
        currentPickup = null;
        currentType = ToolType.None;
    }

    public bool HasTool()
    {
        return currentTool != null;
    }

    public ToolType CurrentTool()
    {
        return currentType;
    }

    public GameObject CurrentToolObject()
    {
        return currentTool;
    }
}