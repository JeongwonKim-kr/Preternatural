using System;
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
        static void ResetStatics() { All.Clear(); LocalPlayerDied = null; s_scenePlayer = null; }

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
                Nickname.Value = NicknameUtil.ToFixed(SessionManager.LocalNickname);
                if (s_scenePlayer == null) s_scenePlayer = GameObject.Find("Player");
                var scenePlayer = s_scenePlayer;
                if (scenePlayer && scenePlayer != gameObject)
                {
                    // 스폰 위치 = 씬 Player 자리 + 클라이언트별 오프셋 (겹침 방지)
                    var cc = GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    transform.SetPositionAndRotation(
                        scenePlayer.transform.position + scenePlayer.transform.right * ((OwnerClientId % 4) * 1.4f),
                        scenePlayer.transform.rotation);
                    if (cc) cc.enabled = true;
                    scenePlayer.SetActive(false);
                }
            }

            FlashlightOn.OnValueChanged += (_, on) => { if (!IsOwner) remoteFlashlight.enabled = on; };
            IsAlive.OnValueChanged += HandleAliveChanged;
            if (!IsAlive.Value) HandleAliveChanged(true, false);
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            bool flashOn = ownerFlashlight && ownerFlashlight.enabled;
            if (FlashlightOn.Value != flashOn) FlashlightOn.Value = flashOn;
            bool hiding = movement && movement.isHiding;
            if (IsHiding.Value != hiding) IsHiding.Value = hiding;
            if (IsAlive.Value && transform.position.y < KillZ)
            {
                var cc = GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                transform.position = new Vector3(0f, 2f, 0f);
                if (cc) cc.enabled = true;
            }
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
            LocalPlayerDied?.Invoke(this);
        }
    }
}
