using TMPro;
using UnityEngine;

public class TextGlitch : MonoBehaviour
{
    public TMP_Text text;

    [Header("Glitch Settings")]
    public float minDelay = 1f;
    public float maxDelay = 3f;

    public float minDuration = 0.05f;
    public float maxDuration = 0.4f;

    public float glitchAmount = 5f;


    private TMP_MeshInfo[] originalMesh;
    private float timer;
    private float nextGlitch;
    private bool glitching;


    void Start()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        text.ForceMeshUpdate();

        originalMesh = text.textInfo.CopyMeshInfoVertexData();

        nextGlitch = Random.Range(minDelay, maxDelay);
    }


    void Update()
    {
        timer += Time.deltaTime;

        if (timer > nextGlitch)
        {
            timer = 0;

            StartGlitch();
        }


        if (glitching)
        {
            ApplyGlitch();
        }
    }


    void StartGlitch()
    {
        glitching = true;

        Invoke(nameof(StopGlitch),
            Random.Range(minDuration, maxDuration));
    }


    void StopGlitch()
    {
        glitching = false;
        ResetText();

        nextGlitch = Random.Range(minDelay, maxDelay);
    }


    void ApplyGlitch()
    {
        text.ForceMeshUpdate();

        TMP_TextInfo info = text.textInfo;


        for (int i = 0; i < info.characterCount; i++)
        {
            if (!info.characterInfo[i].isVisible)
                continue;


            int matIndex = info.characterInfo[i].materialReferenceIndex;

            int vertexIndex = info.characterInfo[i].vertexIndex;


            Vector3[] vertices =
                info.meshInfo[matIndex].vertices;


            Vector3 offset = new Vector3(
                Random.Range(-glitchAmount, glitchAmount),
                Random.Range(-2f, 2f),
                0
            );


            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }


        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            info.meshInfo[i].mesh.vertices =
                info.meshInfo[i].vertices;

            text.UpdateGeometry(
                info.meshInfo[i].mesh,
                i
            );
        }
    }


    void ResetText()
    {
        text.ForceMeshUpdate();

        for (int i = 0; i < text.textInfo.meshInfo.Length; i++)
        {
            text.textInfo.meshInfo[i].vertices =
                originalMesh[i].vertices;

            text.UpdateGeometry(
                text.textInfo.meshInfo[i].mesh,
                i
            );
        }
    }
}