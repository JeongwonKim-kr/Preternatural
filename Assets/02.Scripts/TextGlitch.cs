using TMPro;
using UnityEngine;

public class TextGlitch : MonoBehaviour
{
    public TMP_Text text;

    [Header("Glitch Timing")]
    public float minDelay = 1f;
    public float maxDelay = 3f;

    public float minDuration = 0.05f;
    public float maxDuration = 0.35f;

    [Header("Horizontal Slice")]
    public float sliceAmount = 15f;
    public float sliceHeight = 0.25f;

    [Header("Character Corruption")]
    [Range(0f, 1f)]
    public float characterDisappearChance = 0.15f;

    [Range(0f, 1f)]
    public float characterShiftChance = 0.35f;

    [Header("Strong Glitch")]
    public float strongGlitchChance = 0.15f;
    public float strongGlitchAmount = 35f;

    private TMP_MeshInfo[] originalMesh;

    private float timer;
    private float nextGlitch;

    private bool glitching;


    void Start()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        text.ForceMeshUpdate();

        originalMesh =
            text.textInfo.CopyMeshInfoVertexData();

        nextGlitch =
            Random.Range(
                minDelay,
                maxDelay);
    }


    void Update()
    {
        timer += Time.deltaTime;


        // ==========================================
        // START RANDOM GLITCH
        // ==========================================

        if (!glitching &&
            timer >= nextGlitch)
        {
            timer = 0f;

            StartGlitch();
        }


        // ==========================================
        // APPLY GLITCH
        // ==========================================

        if (glitching)
        {
            ApplyGlitch();
        }
    }


    // =========================================================
    // START GLITCH
    // =========================================================

    void StartGlitch()
    {
        glitching = true;

        CancelInvoke(nameof(StopGlitch));

        Invoke(
            nameof(StopGlitch),
            Random.Range(
                minDuration,
                maxDuration));
    }


    // =========================================================
    // STOP GLITCH
    // =========================================================

    void StopGlitch()
    {
        glitching = false;

        ResetText();

        nextGlitch =
            Random.Range(
                minDelay,
                maxDelay);
    }


    // =========================================================
    // APPLY GLITCH
    // =========================================================

    void ApplyGlitch()
    {
        text.ForceMeshUpdate();

        TMP_TextInfo info =
            text.textInfo;


        // ------------------------------------------
        // Reset to original every frame
        // ------------------------------------------

        for (int i = 0;
             i < info.meshInfo.Length;
             i++)
        {
            info.meshInfo[i].vertices =
                originalMesh[i].vertices.Clone()
                as Vector3[];
        }


        // ------------------------------------------
        // More natural distortion while keeping
        // the original values and timing.
        // ------------------------------------------

        for (int i = 0;
             i < info.characterCount;
             i++)
        {
            TMP_CharacterInfo character =
                info.characterInfo[i];

            if (!character.isVisible)
                continue;


            int materialIndex =
                character.materialReferenceIndex;

            int vertexIndex =
                character.vertexIndex;


            Vector3[] vertices =
                info.meshInfo[
                    materialIndex].vertices;

            float bottom =
                vertices[vertexIndex].y;

            float top =
                vertices[vertexIndex + 2].y;

            float wave =
                Mathf.Sin(
                    Time.time * 28f +
                    i * 0.55f) *
                sliceAmount * 0.18f;


            // ======================================
            // RANDOM HORIZONTAL CUT
            // ======================================

            float randomSlice =
                Random.Range(0f, 1f);


            if (randomSlice < 0.45f)
            {
                float offset =
                    Random.Range(
                        -sliceAmount * 0.4f,
                        sliceAmount * 0.4f) +
                    wave;

                for (int v = 0; v < 4; v++)
                {
                    float y =
                        vertices[
                            vertexIndex + v].y;

                    float normalized =
                        Mathf.InverseLerp(
                            bottom,
                            top,
                            y);

                    if (normalized >
                        sliceHeight * 0.65f &&
                        normalized < 0.98f)
                    {
                        vertices[
                            vertexIndex + v].x +=
                            offset *
                            (0.4f + normalized);
                    }
                }
            }


            // ======================================
            // CHARACTER SHIFT
            // ======================================

            if (Random.value <
                characterShiftChance)
            {
                float shift =
                    Random.Range(
                        -sliceAmount * 0.2f,
                        sliceAmount * 0.2f) +
                    wave * 0.5f;

                for (int v = 0; v < 4; v++)
                {
                    vertices[
                        vertexIndex + v].x +=
                        shift;
                }
            }


            // ======================================
            // MICRO DRIFT
            // ======================================

            if (Random.value <
                characterDisappearChance * 0.6f)
            {
                float microOffset =
                    Random.Range(
                        -sliceAmount * 0.12f,
                        sliceAmount * 0.12f);

                for (int v = 0; v < 4; v++)
                {
                    vertices[
                        vertexIndex + v].x +=
                        microOffset;
                    vertices[
                        vertexIndex + v].y +=
                        Random.Range(
                            -0.4f,
                            0.4f);
                }
            }
        }


        // =====================================================
        // STRONG RANDOM TEAR
        // =====================================================

        if (Random.value <
            strongGlitchChance)
        {
            ApplyStrongTear(info);
        }


        // =====================================================
        // UPDATE MESH
        // =====================================================

        for (int i = 0;
             i < info.meshInfo.Length;
             i++)
        {
            info.meshInfo[i].mesh.vertices =
                info.meshInfo[i].vertices;

            text.UpdateGeometry(
                info.meshInfo[i].mesh,
                i);
        }
    }


    // =========================================================
    // STRONG TEAR
    // =========================================================

    void ApplyStrongTear(TMP_TextInfo info)
    {
        if (info.characterCount == 0)
            return;


        float tearBand =
            Random.Range(
                0.2f,
                0.8f);


        for (int i = 0;
             i < info.characterCount;
             i++)
        {
            TMP_CharacterInfo character =
                info.characterInfo[i];

            if (!character.isVisible)
                continue;


            int materialIndex =
                character.materialReferenceIndex;

            int vertexIndex =
                character.vertexIndex;


            Vector3[] vertices =
                info.meshInfo[
                    materialIndex].vertices;


            float centerY =
                (vertices[vertexIndex].y +
                 vertices[vertexIndex + 2].y) *
                0.5f;


            if (Mathf.Abs(
                centerY -
                tearBand * 100f) < 12f)
            {
                float offset =
                    Random.Range(
                        -strongGlitchAmount * 0.4f,
                        strongGlitchAmount * 0.4f);

                float drift =
                    Mathf.Sin(
                        Time.time * 20f +
                        i * 0.7f) *
                    2.5f;


                for (int v = 0; v < 4; v++)
                {
                    vertices[
                        vertexIndex + v].x +=
                        offset + drift;
                    vertices[
                        vertexIndex + v].y +=
                        Random.Range(
                            -0.5f,
                            0.5f);
                }
            }
        }
    }


    // =========================================================
    // RESET TEXT
    // =========================================================

    void ResetText()
    {
        text.ForceMeshUpdate();


        for (int i = 0;
             i < text.textInfo.meshInfo.Length;
             i++)
        {
            text.textInfo.meshInfo[i].vertices =
                originalMesh[i].vertices.Clone()
                as Vector3[];

            text.UpdateGeometry(
                text.textInfo.meshInfo[i].mesh,
                i);
        }
    }
}