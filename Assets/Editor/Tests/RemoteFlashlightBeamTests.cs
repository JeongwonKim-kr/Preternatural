using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class RemoteFlashlightBeamTests
{
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";
    const string BeamTypeName = "Game.Net.RemoteFlashlightBeam, Assembly-CSharp";

    [Test]
    public void GameScene_DoesNotShipFlashlightBeamValidationPreview()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                Assert.That(root.name, Is.Not.EqualTo("CodexBeamPreviewRoot"),
                    "Editor-only flashlight beam validation objects must never ship in GameScene.");
        }
        finally
        {
            if (openedForTest) EditorSceneManager.CloseScene(scene, true);
        }
    }
    const string DazzleTypeName = "Game.UI.FlashlightDazzleEffect, Assembly-CSharp";
    const string PlayerPrefabPath = "Assets/Prefabs/NetPlayer.prefab";

    [Test]
    public void ScreenDazzleType_IsRemoved()
    {
        Assert.That(Type.GetType(DazzleTypeName), Is.Null,
            "Direct flashlight hits must not cover the local player's screen anymore.");
    }

    [Test]
    public void VisibleBeam_StopsJustBeforeTheNearestWall()
    {
        float length = InvokeFloat("CalculateTargetLength", 12f, 5f, 0.05f);

        Assert.That(length, Is.EqualTo(4.95f).Within(0.001f));
    }

    [Test]
    public void VisibleBeam_UsesANarrowerConeThanTheGameplaySpotLight()
    {
        float radius = InvokeFloat("CalculateBeamRadius", 10f, 60f);

        Assert.That(radius, Is.EqualTo(1.5838f).Within(0.001f));
    }

    [Test]
    public void VisibleBeam_StartsFromATransparentPointInsteadOfABrightPlate()
    {
        var beamType = Type.GetType(BeamTypeName);
        Assert.That(beamType, Is.Not.Null);

        var root = new GameObject("RuntimeBeamApexTest");
        try
        {
            root.AddComponent<Light>();
            var beam = root.AddComponent(beamType);
            beamType.GetMethod("EnsureVisuals", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(beam, null);

            var filter = root.transform.Find("VisibleFlashlightBeam/BeamVolume")
                ?.GetComponent<MeshFilter>();
            Assert.That(filter, Is.Not.Null);

            Vector3[] vertices = filter.sharedMesh.vertices;
            Color[] colors = filter.sharedMesh.colors;
            float minimumZ = float.MaxValue;
            foreach (Vector3 vertex in vertices)
                minimumZ = Mathf.Min(minimumZ, vertex.z);

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!Mathf.Approximately(vertices[i].z, minimumZ)) continue;

                Assert.That(new Vector2(vertices[i].x, vertices[i].y).magnitude,
                    Is.LessThan(0.0001f),
                    "The beam origin must be a point so its near edge cannot appear as a thin plate.");
                Assert.That(colors[i].a, Is.EqualTo(0f).Within(0.0001f),
                    "The beam must fade in from transparent at the flashlight lens.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NetPlayerPrefab_DoesNotAttachGeneratedBeamGeometryToRemoteFlashlight()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var remoteLight = prefab.transform.Find("RemoteModel")
            ?.GetComponentInChildren<Light>(true);

        Assert.That(remoteLight, Is.Not.Null);
        Assert.That(remoteLight.GetComponent("RemoteFlashlightBeam"), Is.Null,
            "The remote flashlight must illuminate the scene without rendering a cone or plate.");
    }

    [Test]
    public void VisibleBeam_UsesASupportedAlwaysIncludedShaderInStandaloneBuilds()
    {
        var beamType = Type.GetType(BeamTypeName);
        Assert.That(beamType, Is.Not.Null);

        var method = beamType.GetMethod("FindRuntimeShader", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null,
            "The beam must deliberately choose a shader that survives standalone build stripping.");

        var shader = method.Invoke(null, null) as Shader;
        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.isSupported, Is.True);

        var settings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var alwaysIncluded = settings.FindProperty("m_AlwaysIncludedShaders");
        bool found = false;
        for (int i = 0; alwaysIncluded != null && i < alwaysIncluded.arraySize; i++)
        {
            if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True,
            "Dynamically created beam materials must use an always-included shader in the player build.");
    }

    [Test]
    public void RuntimeBeamVisuals_CannotBeSerializedIntoGameScene()
    {
        var beamType = Type.GetType(BeamTypeName);
        Assert.That(beamType, Is.Not.Null);

        var root = new GameObject("RuntimeBeamSerializationTest");
        try
        {
            root.AddComponent<Light>();
            var beam = root.AddComponent(beamType);
            beamType.GetMethod("EnsureVisuals", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(beam, null);

            Transform visuals = root.transform.Find("VisibleFlashlightBeam");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.gameObject.hideFlags & HideFlags.DontSave,
                Is.EqualTo(HideFlags.DontSave));

            foreach (var filter in visuals.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(filter.sharedMesh.hideFlags & HideFlags.DontSave,
                    Is.EqualTo(HideFlags.DontSave));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static float InvokeFloat(string methodName, params object[] arguments)
    {
        var beamType = Type.GetType(BeamTypeName);
        Assert.That(beamType, Is.Not.Null, "RemoteFlashlightBeam must provide the visible cone effect.");

        var method = beamType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (float)method.Invoke(null, arguments);
    }
}
