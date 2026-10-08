using UnityEngine;

public class HintThroughWall : MonoBehaviour
{
    [Header("Hint Material")]
    public Material throughWallMaterial;

    private Renderer[] renderers;

    private Material[][] originalMaterials;

    private Material[][] hintMaterials;

    private bool hintEnabled;


    void Awake()
    {
        renderers =
            GetComponentsInChildren<Renderer>(true);

        StoreOriginalMaterials();

        CreateHintMaterials();

        DisableHint();
    }


    // =========================================================
    // STORE ORIGINAL MATERIALS
    // =========================================================

    void StoreOriginalMaterials()
    {
        originalMaterials =
            new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            originalMaterials[i] =
                renderers[i].sharedMaterials;
        }
    }


    // =========================================================
    // CREATE HINT MATERIALS
    // =========================================================

    void CreateHintMaterials()
    {
        if (throughWallMaterial == null)
        {
            Debug.LogWarning(
                "HintThroughWall: Through Wall Material is missing on "
                + gameObject.name
            );

            return;
        }

        hintMaterials =
            new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            int materialCount =
                originalMaterials[i].Length;

            hintMaterials[i] =
                new Material[materialCount];

            for (int j = 0;
                 j < materialCount;
                 j++)
            {
                Material material =
                    new Material(
                        throughWallMaterial
                    );

                material.name =
                    throughWallMaterial.name +
                    " (Hint Instance)";

                SetMaterialEnabled(
                    material,
                    false
                );

                hintMaterials[i][j] =
                    material;
            }
        }
    }


    // =========================================================
    // ENABLE
    // =========================================================

    public void EnableHint()
    {
        if (hintEnabled)
            return;

        if (hintMaterials == null)
            return;

        hintEnabled = true;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Material[] materials =
                new Material[
                    originalMaterials[i].Length +
                    hintMaterials[i].Length
                ];

            // Original materials
            for (int j = 0;
                 j < originalMaterials[i].Length;
                 j++)
            {
                materials[j] =
                    originalMaterials[i][j];
            }

            // Hint materials
            for (int j = 0;
                 j < hintMaterials[i].Length;
                 j++)
            {
                materials[
                    originalMaterials[i].Length + j
                ] =
                    hintMaterials[i][j];
            }

            renderers[i].sharedMaterials =
                materials;

            for (int j = 0;
                 j < hintMaterials[i].Length;
                 j++)
            {
                SetMaterialEnabled(
                    hintMaterials[i][j],
                    true
                );
            }
        }
    }


    // =========================================================
    // DISABLE
    // =========================================================

    public void DisableHint()
    {
        hintEnabled = false;

        if (originalMaterials == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            // Restore ONLY the original materials.
            renderers[i].sharedMaterials =
                originalMaterials[i];
        }

        if (hintMaterials == null)
            return;

        for (int i = 0; i < hintMaterials.Length; i++)
        {
            if (hintMaterials[i] == null)
                continue;

            for (int j = 0;
                 j < hintMaterials[i].Length;
                 j++)
            {
                SetMaterialEnabled(
                    hintMaterials[i][j],
                    false
                );
            }
        }
    }


    // =========================================================
    // SHADER ENABLE/DISABLE
    // =========================================================

    void SetMaterialEnabled(
        Material material,
        bool enabled)
    {
        if (material == null)
            return;

        if (!material.HasProperty("_Color"))
            return;

        Color color =
            material.GetColor("_Color");

        color.a =
            enabled
                ? 0.5f
                : 0f;

        material.SetColor(
            "_Color",
            color
        );
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    void OnDestroy()
    {
        if (hintMaterials == null)
            return;

        for (int i = 0;
             i < hintMaterials.Length;
             i++)
        {
            if (hintMaterials[i] == null)
                continue;

            for (int j = 0;
                 j < hintMaterials[i].Length;
                 j++)
            {
                if (hintMaterials[i][j] != null)
                {
                    Destroy(
                        hintMaterials[i][j]
                    );
                }
            }
        }
    }
}