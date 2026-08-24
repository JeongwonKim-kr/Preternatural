using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Net;

public class RemoteFlashlightPrefabTests
{
    const string NetPlayerPrefabPath = "Assets/Prefabs/NetPlayer.prefab";

    [Test]
    public void NetPlayerPrefab_AttachesFlashlightModelToRemoteRightHand()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);

        Assert.That(prefab, Is.Not.Null, "NetPlayer prefab must exist.");

        var remoteModel = prefab.transform.Find("RemoteModel");
        var rightHand = FindDescendant(remoteModel, "hand_r");
        var flashlightModel = FindDescendant(remoteModel, "RemoteFlashlightModel");

        Assert.That(rightHand, Is.Not.Null, "The remote player rig must expose its right-hand bone.");
        Assert.That(flashlightModel, Is.Not.Null,
            "The Asset Store flashlight model must be present on the remote player.");
        Assert.That(flashlightModel.IsChildOf(rightHand), Is.True,
            "The flashlight model must follow the remote player's right hand, not the first-person camera.");
    }

    [Test]
    public void NetPlayerPrefab_RemoteFlashlightMatchesOwnerLightShapeAndBrightness()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var netPlayer = prefab.GetComponent<NetPlayer>();
        var serializedPlayer = new SerializedObject(netPlayer);
        var remoteLight = serializedPlayer.FindProperty("remoteFlashlight").objectReferenceValue as Light;
        var ownerLight = serializedPlayer.FindProperty("ownerFlashlight").objectReferenceValue as Light;

        Assert.That(remoteLight, Is.Not.Null);
        Assert.That(ownerLight, Is.Not.Null);
        Assert.That(remoteLight.intensity, Is.EqualTo(ownerLight.intensity).Within(0.001f));
        Assert.That(remoteLight.range, Is.EqualTo(ownerLight.range).Within(0.001f));
        Assert.That(remoteLight.spotAngle, Is.EqualTo(ownerLight.spotAngle).Within(0.001f));
    }

    [Test]
    public void NetPlayerPrefab_RemoteFlashlightAimsFromSyncedHeadRotation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var netPlayer = prefab.GetComponent<NetPlayer>();
        var serializedPlayer = new SerializedObject(netPlayer);
        var remoteLight = serializedPlayer.FindProperty("remoteFlashlight").objectReferenceValue as Light;
        var ownerCameraObject = serializedPlayer.FindProperty("ownerCameraObject").objectReferenceValue as GameObject;

        Assert.That(remoteLight, Is.Not.Null);
        Assert.That(ownerCameraObject, Is.Not.Null);

        var aimComponent = remoteLight.GetComponent("RemoteFlashlightAim");
        Assert.That(aimComponent, Is.Not.Null,
            "The remote beam must follow the already-networked head camera rotation.");

        var aimSource = new SerializedObject(aimComponent).FindProperty("aimSource");
        Assert.That(aimSource, Is.Not.Null);
        Assert.That(aimSource.objectReferenceValue, Is.SameAs(ownerCameraObject.transform));
    }

    [Test]
    public void NetPlayerPrefab_RemoteFlashlightUsesOnlyTheUnitySpotLight()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var netPlayer = prefab.GetComponent<NetPlayer>();
        var serializedPlayer = new SerializedObject(netPlayer);
        var remoteLight = serializedPlayer.FindProperty("remoteFlashlight").objectReferenceValue as Light;

        Assert.That(remoteLight, Is.Not.Null);
        Assert.That(remoteLight.type, Is.EqualTo(LightType.Spot));
        Assert.That(remoteLight.GetComponent("RemoteFlashlightBeam"), Is.Null,
            "The flashlight must emit only real light, without a generated beam mesh or white plate.");
        Assert.That(remoteLight.GetComponentInChildren<MeshFilter>(true), Is.Null);
        Assert.That(remoteLight.GetComponentInChildren<MeshRenderer>(true), Is.Null);
    }

    [Test]
    public void NetPlayerPrefab_HidesTheFlashlightLensPlate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var remoteModel = prefab.transform.Find("RemoteModel");
        var flashlightModel = FindDescendant(remoteModel, "RemoteFlashlightModel");
        var renderer = flashlightModel != null
            ? flashlightModel.GetComponentInChildren<MeshRenderer>(true)
            : null;

        Assert.That(renderer, Is.Not.Null,
            "The remote flashlight model must have a renderer whose front lens can be hidden.");
        Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(2),
            "The imported flashlight stores its flat front lens in material slot 3.");

        var lensMaterial = renderer.sharedMaterials[2];
        Assert.That(lensMaterial, Is.Not.Null);
        Assert.That(lensMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000),
            "The front lens override must use transparent rendering instead of the opaque white FBX material.");
        Assert.That(lensMaterial.color.a, Is.EqualTo(0f).Within(0.001f),
            "The flat white lens plate must be fully invisible while the Spot Light remains active.");
    }

    [Test]
    public void RemoteOwnership_HidesEveryLocalCameraChild()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var instance = Object.Instantiate(prefab);

        try
        {
            var netPlayer = instance.GetComponent<NetPlayer>();
            var ownerCameraObject = GetObjectReference<GameObject>(netPlayer, "ownerCameraObject");

            Assert.That(ownerCameraObject, Is.Not.Null);
            InvokeOwnershipPresentation(netPlayer, false);

            foreach (Transform child in ownerCameraObject.transform)
                Assert.That(child.gameObject.activeSelf, Is.False,
                    $"Remote players must not expose the local camera child '{child.name}'.");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void RemoteOwnership_DisablesEveryLightOutsideTheRemoteModel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        var instance = Object.Instantiate(prefab);

        try
        {
            var netPlayer = instance.GetComponent<NetPlayer>();
            var remoteModelRoot = GetObjectReference<GameObject>(netPlayer, "remoteModelRoot");

            Assert.That(remoteModelRoot, Is.Not.Null);
            InvokeOwnershipPresentation(netPlayer, false);

            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                if (light.transform == remoteModelRoot.transform ||
                    light.transform.IsChildOf(remoteModelRoot.transform))
                    continue;

                Assert.That(light.enabled, Is.False,
                    $"Remote replicas must disable owner-only light '{light.name}'.");
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static void InvokeOwnershipPresentation(NetPlayer netPlayer, bool owner)
    {
        var method = typeof(NetPlayer).GetMethod(
            "ApplyOwnershipPresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null,
            "NetPlayer must apply ownership to local-only visuals and lights as one presentation step.");
        method.Invoke(netPlayer, new object[] { owner });
    }

    static T GetObjectReference<T>(NetPlayer netPlayer, string propertyName) where T : Object
    {
        var property = new SerializedObject(netPlayer).FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Missing serialized field '{propertyName}'.");
        return property.objectReferenceValue as T;
    }

    static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        foreach (Transform child in root)
        {
            var found = FindDescendant(child, objectName);
            if (found != null) return found;
        }

        return null;
    }

}
