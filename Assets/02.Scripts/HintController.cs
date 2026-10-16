
using UnityEngine;

public class HintController : MonoBehaviour
{
    [SerializeField] private GameObject[] hintObjects;
    [SerializeField] private float hintDelaySeconds = 420f;

    private float elapsed;
    private float nextLog = 1f;
    private bool hintsActivated;
    private int frameCount;

    private void Start()
    {


        if (hintObjects == null || hintObjects.Length == 0)
        {

            enabled = false;
            return;
        }

        foreach (GameObject hint in hintObjects)
        {
            if (hint == null)
            {

                continue;
            }

            hint.SetActive(false);

        }


    }

    private void Update()
    {
        frameCount++;

        if (frameCount == 1)
        {

        }

        if (hintsActivated)
            return;

        elapsed += Time.unscaledDeltaTime;

        if (elapsed >= nextLog)
        {

            nextLog += 1f;
        }

        if (elapsed >= hintDelaySeconds)
        {
            hintsActivated = true;



            foreach (GameObject hint in hintObjects)
            {
                if (hint == null)
                    continue;

                hint.SetActive(true);

            }
        }
    }
}