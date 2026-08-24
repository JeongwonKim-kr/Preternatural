using System.Reflection;
using Game.Net;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class BossEnemyTests
{
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";

    [Test]
    public void BossChildCollider_IsRecognizedAsTheViewedMonster()
    {
        var cameraObject = new GameObject("BossEnemyTests_Camera");
        var boss = new GameObject("BossPart2");

        try
        {
            cameraObject.transform.position = new Vector3(10000f, 10000f, 10000f);
            cameraObject.transform.forward = Vector3.forward;
            var camera = cameraObject.AddComponent<Camera>();

            boss.transform.position = cameraObject.transform.position + Vector3.forward * 5f;
            var ai = boss.AddComponent<MonsterLookAI>();
            ai.playerCamera = camera;
            ai.lookDistance = 20f;
            ai.lookAngle = 45f;

            var hitbox = new GameObject("BossHitbox");
            hitbox.transform.SetParent(boss.transform, false);
            hitbox.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            typeof(MonsterLookAI)
                .GetMethod("CheckLooking", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(ai, null);

            bool playerLooking = (bool)typeof(MonsterLookAI)
                .GetField("playerLooking", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(ai);

            Assert.That(playerLooking, Is.True,
                "Gaze detection must recognize a collider belonging to this monster without relying on the original MonsterPart1 name.");
        }
        finally
        {
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void GameScene_BossIsNetworkedAndWiredToThePart2Entrance()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject boss = FindRoot(scene, "BossPart2");
            Assert.That(boss, Is.Not.Null,
                "GameScene must contain the final-room boss.");

            var bossAi = boss.GetComponent<MonsterLookAI>();
            var bossNetworkObject = boss.GetComponent<NetworkObject>();
            var bossAdapter = boss.GetComponent<MonsterNetAdapter>();
            Assert.That(bossAi, Is.Not.Null);
            Assert.That(bossNetworkObject, Is.Not.Null);
            Assert.That(bossAdapter, Is.Not.Null);

            Assert.That(boss.transform.position.x, Is.InRange(-22f, -13f));
            Assert.That(boss.transform.position.y, Is.InRange(5.4f, 5.8f));
            Assert.That(boss.transform.position.z, Is.InRange(-393f, -386f));
            Assert.That(boss.transform.lossyScale.x, Is.GreaterThan(1.2f),
                "The boss should read larger than the original monster.");

            Assert.That(bossAi.patrolPoints, Is.Not.Null);
            Assert.That(bossAi.patrolPoints, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(bossAi.chaseDistance, Is.GreaterThanOrEqualTo(40f));

            GameObject part2 = FindRoot(scene, "Part 2-1");
            var entrance = part2.transform.Find("door (1)").GetComponent<DoorTeleport>();
            Assert.That(entrance.objectToEnable, Is.SameAs(boss),
                "Entering Part 2 must wake the boss through the existing multiplayer-safe monster activation path.");

            GameObject original = FindGameObject(scene, "MonsterPart1");
            var originalNetworkObject = original.GetComponent<NetworkObject>();
            Assert.That(ReadGlobalObjectIdHash(bossNetworkObject),
                Is.Not.EqualTo(ReadGlobalObjectIdHash(originalNetworkObject)),
                "Each in-scene NetworkObject needs a unique identity for host and guests.");
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_FinalRoomBossAreaIsCoveredByNavMesh()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            bool found = NavMesh.SamplePosition(
                new Vector3(-17.72f, 4.9f, -389f),
                out NavMeshHit hit,
                4f,
                NavMesh.AllAreas);

            Assert.That(found, Is.True,
                "The final room needs baked navigation for the boss to patrol and chase players.");
            Assert.That(hit.position.z, Is.InRange(-393f, -385f));
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_BossNavigationUsesADedicatedSurface()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject navigationRoot = FindRoot(scene, "BossPart2NavMesh");
            Assert.That(navigationRoot, Is.Not.Null,
                "The final room must not overwrite the established navigation data for the rest of the map.");

            var surface = navigationRoot.GetComponent<NavMeshSurface>();
            Assert.That(surface, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(surface.navMeshData),
                Is.EqualTo("Assets/01.Scenes/GameScene/NavMesh-BossPart2NavMesh.asset"));
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;

        return null;
    }

    static GameObject FindGameObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name)
                    return transform.gameObject;

        return null;
    }

    static long ReadGlobalObjectIdHash(NetworkObject networkObject)
    {
        var serializedObject = new SerializedObject(networkObject);
        return serializedObject.FindProperty("GlobalObjectIdHash").longValue;
    }
}
