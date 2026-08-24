using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class MapContinuityTests
{
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";

    [Test]
    public void GameScene_NorthCorridorHasContinuousWalkableSupportIntoCentralSquare()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            Physics.SyncTransforms();

            float[] corridorX = { -83.10f, -82.10f, -81.10f };
            for (float z = -94f; z >= -172f; z -= 0.5f)
            {
                foreach (float x in corridorX)
                {
                    Assert.That(TryFindWalkableSupport(x, z, out float supportY), Is.True,
                        $"The north corridor has no walkable floor at ({x:F2}, {z:F2}); " +
                        "the player cannot reach the central square without teleporting.");
                    Assert.That(supportY, Is.InRange(3.7f, 5.0f),
                        $"Floor support at ({x:F2}, {z:F2}) is outside the traversable elevation range.");
                    Assert.That(HasPlayerClearance(x, z, supportY), Is.True,
                        $"A solid obstacle blocks the player-sized corridor at ({x:F2}, {z:F2}).");
                }
            }
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_NorthCorridorNavMeshConnectsToCentralSquare()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            Assert.That(NavMesh.SamplePosition(
                new Vector3(-82.10f, 3.89f, -100f),
                out NavMeshHit corridor,
                3f,
                NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(
                new Vector3(-67.88f, 4.9f, -221.08f),
                out NavMeshHit square,
                3f,
                NavMesh.AllAreas), Is.True);

            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(
                corridor.position,
                square.position,
                NavMesh.AllAreas,
                path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                "Enemies must be able to follow players over the physical bridge into and out of the central square.");
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_CentralSquareEntranceKeepsPlayersInsideRamp()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            Physics.SyncTransforms();

            const float corridorCenterX = -82.10f;
            float[] exposedSectionZ = { -155f, -160f, -165f, -169f };
            foreach (float z in exposedSectionZ)
            {
                Assert.That(TryFindWalkableSupport(corridorCenterX, z, out float supportY), Is.True,
                    $"The central-square entrance has no walkable support at z={z:F2}.");
                Assert.That(HasSideBarrier(corridorCenterX, z, supportY, Vector3.left), Is.True,
                    $"The left side of the central-square entrance is open at z={z:F2}; players can fall off the ramp.");
                Assert.That(HasSideBarrier(corridorCenterX, z, supportY, Vector3.right), Is.True,
                    $"The right side of the central-square entrance is open at z={z:F2}; players can fall off the ramp.");
            }
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_CentralSquareEntranceSideIsClosedExceptForDoorway()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            Physics.SyncTransforms();

            float[] closedSectionsX = { -124f, -110f, -95f, -85f, -75f, -55f, -35f, -15f };
            foreach (float x in closedSectionsX)
            {
                Assert.That(HasBoundaryBarrier(new Vector3(x, 6f, -166f), Vector3.forward, 4f), Is.True,
                    $"The entrance-facing side of the central square is open at x={x:F2}; only the doorway may remain open.");
            }

            Assert.That(HasBoundaryBarrier(new Vector3(-82.1f, 6f, -166f), Vector3.forward, 4f), Is.False,
                "The corridor-width entrance into the central square must remain open.");

            Assert.That(HasBoundaryBarrier(new Vector3(-122f, 6f, -166f), Vector3.left, 3f), Is.True,
                "The left corner return beside the entrance wall is open; players can walk around the wall and fall.");
            Assert.That(HasBoundaryBarrier(new Vector3(-13f, 6f, -166f), Vector3.right, 3f), Is.True,
                "The right corner return beside the entrance wall is open; players can walk around the wall and fall.");
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void GameScene_CentralSquareEntranceRampConcealsWorldBelowSmoothTransition()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            Physics.SyncTransforms();

            Transform map = GameObject.Find("Map Part 1-2").transform;
            Collider ramp = map.Find("CentralSquareEntranceRamp").GetComponent<Collider>();

            float previousY = float.NaN;
            for (float z = -145.5f; z >= -163.5f; z -= 0.5f)
            {
                Assert.That(TryFindWalkableSupport(-82.1f, z, out float supportY), Is.True,
                    $"The entrance transition has no walkable support at z={z:F2}.");
                if (!float.IsNaN(previousY))
                {
                    Assert.That(Mathf.Abs(supportY - previousY), Is.LessThanOrEqualTo(0.04f),
                        $"The entrance transition has a visible height step near z={z:F2}.");
                }
                previousY = supportY;
            }

            foreach (float z in new[] { -150f, -155f, -160f, -163f })
            {
                Ray undersideProbe = new Ray(new Vector3(-87f, 3.8f, z), Vector3.right);
                Assert.That(ramp.Raycast(undersideProbe, out _, 10f), Is.True,
                    $"The entrance ramp is visibly open underneath at z={z:F2}; the outside world can show through.");
            }
        }
        finally
        {
            if (openedForTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static bool TryFindWalkableSupport(float x, float z, out float supportY)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(x, 7f, z),
            Vector3.down,
            5f,
            ~0,
            QueryTriggerInteraction.Ignore);

        supportY = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.7f || hit.point.y < 3.7f || hit.point.y > 5.0f)
                continue;

            supportY = Mathf.Max(supportY, hit.point.y);
        }

        return !float.IsNegativeInfinity(supportY);
    }

    static bool HasPlayerClearance(float x, float z, float supportY)
    {
        Vector3 lower = new Vector3(x, supportY + 0.55f, z);
        Vector3 upper = new Vector3(x, supportY + 1.45f, z);
        return !Physics.CheckCapsule(
            lower,
            upper,
            0.45f,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    static bool HasSideBarrier(float x, float z, float supportY, Vector3 direction)
    {
        return Physics.Raycast(
            new Vector3(x, supportY + 1f, z),
            direction,
            2.25f,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    static bool HasBoundaryBarrier(Vector3 origin, Vector3 direction, float distance)
    {
        return Physics.Raycast(
            origin,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
    }
}
