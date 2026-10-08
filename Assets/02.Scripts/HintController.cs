using UnityEngine;

public class HintController : MonoBehaviour
{
    [System.Serializable]
    public class HintTarget
    {
        [Header("Target")]
        public Transform target;

        [Header("Through-Wall Hint")]
        public HintThroughWall throughWallHint;

        [Header("Settings")]
        public bool showHint = true;
    }

    [Header("Hint Targets")]
    public HintTarget item1;
    public HintTarget item2;
    public HintTarget item3;
    public HintTarget exit;

    [Header("Hint Timer")]
    public float hintDelay = 420f; // 7 minutes
    public bool activateOnStart = true;

    private float hintTimer;
    private bool hintActivated;
    private bool hintsShown;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        HideAllHints();

        if (activateOnStart)
            ActivateHintTimer();
    }


    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        if (!hintActivated)
            return;

        if (hintsShown)
            return;

        hintTimer += Time.deltaTime;

        if (hintTimer >= hintDelay)
        {
            ShowAllHints();
        }
    }


    // =========================================================
    // ACTIVATE TIMER
    // =========================================================

    public void ActivateHintTimer()
    {
        hintTimer = 0f;
        hintActivated = true;
        hintsShown = false;

        HideAllHints();
    }


    // =========================================================
    // SHOW ALL HINTS
    // =========================================================

    void ShowAllHints()
    {
        hintsShown = true;

        ShowHint(item1);
        ShowHint(item2);
        ShowHint(item3);
        ShowHint(exit);
    }


    // =========================================================
    // SHOW INDIVIDUAL HINT
    // =========================================================

    void ShowHint(HintTarget hint)
    {
        if (hint == null)
            return;

        if (!hint.showHint)
            return;

        if (hint.target == null)
            return;

        // Automatically find the component if
        // you did not manually assign it.
        if (hint.throughWallHint == null)
        {
            hint.throughWallHint =
                hint.target.GetComponent<HintThroughWall>();
        }

        if (hint.throughWallHint == null)
        {
            Debug.LogWarning(
                "No HintThroughWall component found on: "
                + hint.target.name
            );

            return;
        }

        hint.throughWallHint.EnableHint();
    }


    // =========================================================
    // HIDE ALL HINTS
    // =========================================================

    void HideAllHints()
    {
        HideHint(item1);
        HideHint(item2);
        HideHint(item3);
        HideHint(exit);
    }


    // =========================================================
    // HIDE INDIVIDUAL HINT
    // =========================================================

    void HideHint(HintTarget hint)
    {
        if (hint == null)
            return;

        if (hint.target == null)
            return;

        if (hint.throughWallHint == null)
        {
            hint.throughWallHint =
                hint.target.GetComponent<HintThroughWall>();
        }

        if (hint.throughWallHint == null)
            return;

        hint.throughWallHint.DisableHint();
    }


    // =========================================================
    // RESET
    // =========================================================

    public void ResetHint()
    {
        hintTimer = 0f;
        hintsShown = false;

        HideAllHints();
    }


    // =========================================================
    // FORCE SHOW
    // =========================================================

    public void ShowHintsImmediately()
    {
        hintActivated = true;
        hintTimer = hintDelay;

        ShowAllHints();
    }
}