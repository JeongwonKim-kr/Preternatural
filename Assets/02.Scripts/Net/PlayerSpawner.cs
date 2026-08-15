using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Net
{
    /// 로비(Homescreen) 자동 스폰 회귀 수정: NGO는 NetworkConfig.PlayerPrefab이 설정돼 있으면
    /// ConnectionApproval을 쓰지 않는 한 클라이언트 연결 즉시(= 로비 대기 중에도) 플레이어를
    /// 자동 스폰한다(NetworkManager.HostServerInitialize / ConnectionRequestMessage.Handle 확인,
    /// NGO 2.13.1 기준: createPlayerObject = NetworkConfig.PlayerPrefab != null). 그 결과 NetPlayer가
    /// Homescreen 씬에서 스폰되어 씬 Player를 찾지 못하고(BindScenePlayer 15초 실패), FirstPersonCamera가
    /// 커서를 잠가 로비 UI 클릭이 먹통이 되는 회귀가 있었다("게임 시작"을 눌러도 반응이 없던 원인).
    ///
    /// 고침: NetworkConfig.ConnectionApproval=true(NetSetupTool이 씬에 배선)로 두고, 이 컴포넌트가
    /// ConnectionApprovalCallback에서 항상 CreatePlayerObject=false를 응답해 자동 스폰을 막는다.
    /// 실제 스폰은 서버가 명시적으로 수행한다:
    ///  - 로비→게임 전환: NetworkSceneManager.OnLoadEventCompleted(sceneName==GameScene)에서
    ///    완료된 클라이언트 전원을 스폰.
    ///  - 게임 중 늦은 접속: NetworkSceneManager.OnSynchronizeComplete에서, 그 시점 활성 씬이
    ///    GameScene이면 해당 클라이언트만 스폰(로비 중 최초 동기화는 활성 씬이 Homescreen이므로 스킵).
    /// 두 경로 모두 서버 전용이며(IsServer 가드), 이미 플레이어 오브젝트가 있으면 건너뛴다(중복 스폰 방지).
    ///
    /// Awake가 아닌 Start에서 콜백을 등록한다 — Unity는 씬의 모든 컴포넌트 Awake+OnEnable을 마친
    /// 뒤에야 첫 Start를 호출하므로, 같은 GO에서 NetworkManager보다 먼저 배치돼도(컴포넌트 순서
    /// 비보장) NetworkManager.Singleton이 이미 채워져 있음이 보장된다. 방 생성/입장 버튼은 그보다
    /// 늦게(최소 한 프레임 뒤, 사용자 입력 이후) 호출되므로 콜백 등록이 항상 앞선다.
    ///
    /// 단, NetworkManager.SceneManager/SpawnManager는 별개 타이밍이다 — 둘 다 NetworkManager.Initialize()
    /// (StartHost/StartServer/StartClient 내부에서 호출)가 실행돼야 생성되므로 Start() 시점엔 아직
    /// null이다(실측: NullReferenceException at PlayerSpawner.cs 구독 라인). 그래서 씬 이벤트 구독은
    /// OnServerStarted(호스트가 됐을 때만 발생) 안에서 한다 — 그 시점엔 이미 Initialize()가 끝나 있다.
    /// 로비 재입장 등으로 호스트를 다시 시작할 때마다 NetworkManager.Initialize()가 SceneManager를
    /// 새 인스턴스로 교체하므로, OnServerStarted가 다시 뜰 때마다 그 새 인스턴스에 재구독한다.
    public class PlayerSpawner : MonoBehaviour
    {
        const string GameSceneName = "GameScene";

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[PlayerSpawner] NetworkManager.Singleton이 없음 — 로비 자동 스폰 방지 배선 실패(NetSetupTool 재실행 필요)");
                return;
            }
            nm.ConnectionApprovalCallback = OnConnectionApproval;
            nm.OnServerStarted += HandleServerStarted;
        }

        static void HandleServerStarted()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
            {
                Debug.LogError("[PlayerSpawner] OnServerStarted에서도 SceneManager가 없음 — 스폰 배선 불가");
                return;
            }
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            nm.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;
        }

        static void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // 연결 자체는 항상 승인하되, 플레이어 오브젝트 자동 생성은 항상 거부한다.
            // 실제 스폰은 OnLoadEventCompleted/OnSynchronizeComplete가 GameScene 진입 시점에 수행한다.
            response.Approved = true;
            response.CreatePlayerObject = false;
        }

        static void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || sceneName != GameSceneName) return;
            foreach (var clientId in clientsCompleted) SpawnIfNeeded(nm, clientId);
        }

        static void OnSynchronizeComplete(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            // 로비 중 최초 접속 동기화는 활성 씬이 Homescreen이라 여기서 걸러진다 — 늦은 접속(게임 중 합류)만 통과.
            if (SceneManager.GetActiveScene().name != GameSceneName) return;
            SpawnIfNeeded(nm, clientId);
        }

        static void SpawnIfNeeded(NetworkManager nm, ulong clientId)
        {
            if (nm.SpawnManager.GetPlayerNetworkObject(clientId) != null) return; // 이미 스폰됨 — 중복 방지

            var prefab = nm.NetworkConfig.PlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[PlayerSpawner] NetworkConfig.PlayerPrefab이 비어 있어 수동 스폰 불가");
                return;
            }

            var instance = Instantiate(prefab);
            var no = instance.GetComponent<NetworkObject>();
            no.SpawnAsPlayerObject(clientId);
        }
    }
}
