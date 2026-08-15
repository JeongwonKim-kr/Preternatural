using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Player;

namespace Game.Net
{
    /// 스폰된 플레이어 루트. 오너: 기존 싱글 컴포넌트 활성 + 상태 발행.
    /// 원격: 컨트롤 비활성 + 모델 표시 + 손전등 라이트 미러.
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();
        public static event Action<NetPlayer> LocalPlayerDied;

        /// Task 7 리뷰 반영: 로컬(오너) NetPlayer — PickupItem/WoodBoard 등이 씬에 정적으로
        /// 배선된 toolHolder 대신 런타임에 실제 로컬 플레이어의 ToolHolder를 찾는 데 쓴다.
        public static NetPlayer Local { get; private set; }

        [Header("빌드 툴이 배선")]
        [SerializeField] Behaviour[] ownerOnly;        // PlayerMovement, FirstPersonCamera, FlashlightController, ToolHolder, CrosshairInteract, AudioListener
        [SerializeField] GameObject ownerCameraObject; // 카메라 GO (원격은 Camera 비활성, transform은 회전 동기 대상)
        [SerializeField] GameObject remoteModelRoot;   // Sky Protective Suit (오너에겐 숨김)
        [SerializeField] Light remoteFlashlight;       // 원격 표시용 손전등 라이트
        [SerializeField] Light ownerFlashlight;        // 기존 FlashlightController의 Light
        [SerializeField] PlayerMovement movement;
        const float KillZ = -30f;
        static GameObject s_scenePlayer;

        public NetworkVariable<bool> FlashlightOn = new(writePerm: NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsHiding = new(writePerm: NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsAlive = new(true);
        public NetworkVariable<FixedString64Bytes> Nickname = new(writePerm: NetworkVariableWritePermission.Owner);

        public Camera HeadCamera { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); LocalPlayerDied = null; s_scenePlayer = null; Local = null; }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            HeadCamera = ownerCameraObject.GetComponent<Camera>();
            bool owner = IsOwner;

            foreach (var b in ownerOnly) if (b) b.enabled = owner;
            HeadCamera.enabled = owner;
            var listener = ownerCameraObject.GetComponent<AudioListener>();
            if (listener) listener.enabled = owner;
            remoteModelRoot.SetActive(!owner);
            remoteFlashlight.enabled = !owner && FlashlightOn.Value;

            if (owner)
            {
                Local = this;
                Nickname.Value = NicknameUtil.ToFixed(SessionManager.LocalNickname);
                ownerCameraObject.AddComponent<Game.Voice.VoicePositionUpdater>();
                gameObject.AddComponent<Game.UI.MicStatusHud>(); // 좌하단 마이크 상태 HUD 코드 생성
                // 씬 활성화가 스폰보다 늦을 수 있어(대형 씬) Find를 재시도 코루틴으로 바인딩.
                StartCoroutine(BindScenePlayer());
            }

            FlashlightOn.OnValueChanged += (_, on) => { if (!IsOwner) remoteFlashlight.enabled = on; };
            IsAlive.OnValueChanged += HandleAliveChanged;
            if (!IsAlive.Value) HandleAliveChanged(true, false);
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
            All.Remove(this);
        }

        void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            if (Input.GetKeyDown(KeyCode.M) && IsAlive.Value && Game.Voice.VoiceManager.Instance != null)
                Game.Voice.VoiceManager.Instance.ToggleMute();
            bool flashOn = ownerFlashlight && ownerFlashlight.enabled;
            if (FlashlightOn.Value != flashOn) FlashlightOn.Value = flashOn;
            bool hiding = movement && movement.isHiding;
            if (IsHiding.Value != hiding) IsHiding.Value = hiding;
            if (IsAlive.Value && transform.position.y < KillZ)
            {
                var cc = GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                transform.position = s_scenePlayer != null
                    ? s_scenePlayer.transform.position + Vector3.up * 0.5f
                    : new Vector3(0f, 2f, 0f);
                if (cc) cc.enabled = true;
            }
        }

        /// 씬 Player를 찾을 때까지 재시도 후 스폰 텔레포트 + 씬 Player 비활성.
        /// NGO 스폰이 씬 활성화 완료 전에 일어나면 GameObject.Find가 실패하므로 1프레임씩 최대 15초 재시도.
        /// 최종 리뷰 Important 5: 이름 탐색이 실패하면 PlayerMovement 컴포넌트 기반 폴백도 시도한다 —
        /// 그래도 실패하면 씬 Player(카메라·AudioListener·입력)가 영구 잔존해 이중 오디오 리스너 등의
        /// 원인이 되므로 경고를 에러로 승격한다.
        IEnumerator BindScenePlayer()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (s_scenePlayer == null && Time.realtimeSinceStartup < deadline)
            {
                s_scenePlayer = GameObject.Find("Player");
                if (s_scenePlayer == null)
                {
                    // 이름 탐색 폴백: NetworkObject가 없는(=NetPlayer 프리팹 인스턴스가 아닌) PlayerMovement를
                    // 찾는다 — 씬 오브젝트 이름이 "Player"가 아니어도 이 방식으로는 찾을 수 있다.
                    foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (pm.GetComponent<NetworkObject>() == null)
                        {
                            s_scenePlayer = pm.gameObject;
                            break;
                        }
                    }
                }
                if (s_scenePlayer == null) yield return null;
            }
            var scenePlayer = s_scenePlayer;
            if (scenePlayer == null)
            {
                Debug.LogError("[NetPlayer] 씬 Player를 찾지 못함 — 스폰 텔레포트 생략(씬 Player 카메라/AudioListener 잔존 위험)");
                yield break;
            }
            if (scenePlayer == gameObject) yield break;
            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.SetPositionAndRotation(
                scenePlayer.transform.position + scenePlayer.transform.right * ((OwnerClientId % 4) * 1.4f),
                scenePlayer.transform.rotation);
            if (cc) cc.enabled = true;
            scenePlayer.SetActive(false);
        }

        public void ServerKill()
        {
            if (!IsServer || !IsAlive.Value) return;
            IsAlive.Value = false;
        }

        void HandleAliveChanged(bool _, bool alive)
        {
            if (alive) return;
            if (!IsOwner) { remoteModelRoot.SetActive(false); remoteFlashlight.enabled = false; return; }
            foreach (var b in ownerOnly) if (b) b.enabled = false;
            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            Game.Voice.VoiceManager.Instance?.SetMuted(true);
            StartCoroutine(ActivateSpectatorFallback());
            LocalPlayerDied?.Invoke(this);
        }

        /// 관전 카메라 활성의 주 경로는 MonsterNetAdapter.JumpscareRpc(점프스케어 연출 후 5초 뒤 전환)다.
        /// 이건 그 RPC가 유실되거나 몬스터 공격 이외의 사망 경로가 생길 때를 대비한 안전망 — 같은
        /// 지연을 둬 점프스케어 연출과 겹치지 않게 하고, 중복 활성(SpectatorCamera.enabled=true 재대입)은
        /// 무해하므로 두 경로가 함께 실행돼도 문제없다.
        IEnumerator ActivateSpectatorFallback()
        {
            yield return new WaitForSeconds(5f);
            if (ownerCameraObject == null) yield break;
            var spectator = ownerCameraObject.GetComponent<Game.Gameplay.SpectatorCamera>()
                         ?? ownerCameraObject.AddComponent<Game.Gameplay.SpectatorCamera>();
            spectator.enabled = true;
        }
    }
}
