using Game.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class MonsterNetAdapterTests
{
    [Test]
    public void NonHostNetworkSpawn_KeepsMonsterInitializationComponentsEnabled()
    {
        var monster = new GameObject("MonsterNetAdapterTests_Monster");
        monster.SetActive(false);
        try
        {
            var agent = monster.AddComponent<NavMeshAgent>();
            var ai = monster.AddComponent<MonsterLookAI>();
            var adapter = monster.AddComponent<MonsterNetAdapter>();

            adapter.OnNetworkSpawn();

            Assert.That(ai.enabled, Is.True,
                "MonsterLookAI.Start must run on clients so the death overlay is hidden during scene initialization.");
            Assert.That(agent.enabled, Is.True,
                "The dormant awake flag, not component disablement, should stop client-side monster simulation.");
        }
        finally
        {
            Object.DestroyImmediate(monster);
        }
    }
}
