using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Net;
using Game.Player;
using Game.UI;
using Game.Voice;

/// NetPlayer 프리팹과 씬 네트워크 배선을 코드로 조립하는 에디터 빌드 툴.
/// 손 편집 대신 이 스크립트를 고쳐 재실행할 것 — 메뉴 1→2 순서로 몇 번을 다시 실행해도
/// 동일한 결과가 나오도록(idempotent) 짜여 있다. 이후 태스크(Task 6/7/8)가 새 어댑터
/// 컴포넌트를 추가하면, 이 스크립트에 부착 로직을 보태고 메뉴 2를 재실행해 갱신한다.
public static class NetSetupTool
{
    const string PlayerPrefabPath = "Assets/Prefabs/NetPlayer.prefab";
    const string ModelPrefabPath = "Assets/Models/Player/PlayerModel.prefab";
    const string FlashlightModelPrefabPath = "Assets/VanillaLoopStudio/FreeSampleAnimationSet/Art/Prefabs/SK_Flashlight.prefab";
    const string InvisibleFlashlightLensMaterialPath = "Assets/Resources/Materials/InvisibleFlashlightLens.mat";
    const string AnimatorControllerPath = "Assets/Models/Player/PlayerLocomotion.controller";
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";
    const string HomeScenePath = "Assets/01.Scenes/Homescreen.unity";

    [MenuItem("Game/Net/1. 플레이어 프리팹 생성")]
    public static void BuildPlayerPrefab()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath);
        var scenePlayer = GameObject.Find("Player");
        if (!scenePlayer) { Debug.LogError("[NetSetup] GameScene에 Player 없음"); return; }

        var root = Object.Instantiate(scenePlayer);
        root.name = "NetPlayer";
        // 프리팹은 원점 기준으로 저장한다 — 실제 스폰 위치는 NetPlayer.OnNetworkSpawn이
        // 씬의 Player 지점을 읽어 런타임에 배치한다(클라이언트별 오프셋 포함).
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 원본 캡슐 비주얼 제거 — 원격은 수트 모델, 오너는 1인칭이라 캡슐 렌더러는 이중 표시만 유발.
        // 콜라이더/CharacterController는 트리거 상호작용에 필요하므로 유지.
        var rootRenderer = root.GetComponent<MeshRenderer>();
        if (rootRenderer) Object.DestroyImmediate(rootRenderer);
        var rootFilter = root.GetComponent<MeshFilter>();
        if (rootFilter) Object.DestroyImmediate(rootFilter);

        // 1) NetworkObject + Owner NetworkTransform
        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

        // 2) 카메라 GO — GameScene 조사 결과 Player/Camera로 이미 자식이다(별도 GO 아님).
        //    Instantiate가 하위 트리를 통째로 복제하므로 별도 탐색/재배선이 필요 없다.
        //    구조가 달라져 못 찾으면 추측 배치 대신 즉시 실패시킨다.
        var cam = root.GetComponentInChildren<Camera>(true);
        if (!cam)
        {
            Debug.LogError("[NetSetup] Player 하위에서 Camera를 찾지 못함 — 씬 구조가 예상과 다름(중단)");
            Object.DestroyImmediate(root);
            return;
        }
        // 카메라 GO에도 Owner NetworkTransform 추가 — 회전 동기(몬스터 시선/보이스 위치용).
        // NetworkObject는 루트의 것 하나면 충분 — NGO는 자식 NetworkBehaviour를 자동 탐색한다.
        if (!cam.GetComponent<NetworkTransform>())
        {
            var camNt = cam.gameObject.AddComponent<NetworkTransform>();
            camNt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            camNt.SyncScaleX = camNt.SyncScaleY = camNt.SyncScaleZ = false;
        }

        // 기존 FlashlightController가 참조하던 Light를 원격 표시의 기준값으로도 쓴다.
        var flashCtrl = root.GetComponentInChildren<FlashlightController>(true);
        Light ownerFlashlight = null;
        if (flashCtrl)
        {
            var soFlash = new SerializedObject(flashCtrl);
            ownerFlashlight = soFlash.FindProperty("flashlightLight").objectReferenceValue as Light;
        }
        if (!ownerFlashlight)
            Debug.LogWarning("[NetSetup] FlashlightController.flashlightLight를 찾지 못함 — ownerFlashlight 미배선");

        // 3) 원격 모델: PlayerModel.prefab 인스턴스 → root 자식.
        var modelRoot = BuildRemoteModel(root);

        // 4) 원격 손전등: 오른손에 Asset Store 모델과 SpotLight를 붙인다. 기본 조명은 비활성.
        //    NetPlayer.OnNetworkSpawn/FlashlightOn.OnValueChanged가 원격에서 켜고 끈다.
        var remoteFlashlight = BuildRemoteFlashlight(modelRoot, cam.transform, ownerFlashlight);

        // 5) NetPlayer 부착 + SerializedObject로 필드 배선
        var np = root.AddComponent<NetPlayer>();
        var so = new SerializedObject(np);

        var ownerOnly = new List<Behaviour>();
        void CollectOwnerOnly<T>() where T : Behaviour
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c) ownerOnly.Add(c);
            else Debug.LogWarning($"[NetSetup] ownerOnly 대상 {typeof(T).Name}을(를) Player 하위에서 찾지 못함");
        }
        CollectOwnerOnly<PlayerMovement>();
        CollectOwnerOnly<FirstPersonCamera>();
        CollectOwnerOnly<FlashlightController>();
        CollectOwnerOnly<ToolHolder>();
        CollectOwnerOnly<CrosshairInteract>();

        var ownerOnlyProp = so.FindProperty("ownerOnly");
        ownerOnlyProp.arraySize = ownerOnly.Count;
        for (int i = 0; i < ownerOnly.Count; i++)
            ownerOnlyProp.GetArrayElementAtIndex(i).objectReferenceValue = ownerOnly[i];

        so.FindProperty("ownerCameraObject").objectReferenceValue = cam.gameObject;
        so.FindProperty("remoteModelRoot").objectReferenceValue = modelRoot;
        so.FindProperty("remoteFlashlight").objectReferenceValue = remoteFlashlight;

        so.FindProperty("ownerFlashlight").objectReferenceValue = ownerFlashlight;

        so.FindProperty("movement").objectReferenceValue = root.GetComponentInChildren<PlayerMovement>(true);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 6) 저장 후 씬의 임시 인스턴스 삭제
        System.IO.Directory.CreateDirectory("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NetSetup] NetPlayer 프리팹 생성 완료: {PlayerPrefabPath}");
    }

    // ---------- 원격 모델 ----------
    /// PlayerModel.prefab을 root 자식으로 인스턴스화하고, CharacterController 규격(높이·중심)에
    /// 맞춰 스케일과 발 위치를 자동 정렬한다. 원점이 발이든 허리든 상관없이 렌더러 바운즈 기준으로 동작.
    static GameObject BuildRemoteModel(GameObject root)
    {
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (!modelPrefab)
        {
            Debug.LogError($"[NetSetup] 모델 프리팹 없음: {ModelPrefabPath}");
            return null;
        }

        var model = (GameObject)Object.Instantiate(modelPrefab);
        model.name = "RemoteModel";
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // 충돌은 CharacterController가 전담 — 모델에 콜라이더가 딸려오면 제거(현재 모델엔 없음, 방어적).
        foreach (var col in model.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        var cc = root.GetComponent<CharacterController>();
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0 && cc)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 0.01f)
            {
                float footY = cc.center.y - cc.height / 2f; // CC 하단 — 캡슐 발바닥
                float scale = cc.height / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(0, footY - bounds.min.y * scale, 0);
            }
        }
        else
        {
            Debug.LogWarning("[NetSetup] 원격 모델 크기 자동 정렬 생략 — Renderer 또는 CharacterController 없음");
        }

        // 애니메이터: PlayerModel.prefab에는 Avatar만 연결돼 있고 컨트롤러는 비어있다 — 여기서 연결.
        var animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = model.AddComponent<Animator>();
        if (animator.runtimeAnimatorController == null)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (controller) animator.runtimeAnimatorController = controller;
            else Debug.LogError($"[NetSetup] 애니메이터 컨트롤러 없음: {AnimatorControllerPath}");
        }
        animator.applyRootMotion = false; // 이동은 CharacterController(오너)/NetworkTransform(원격)이 전담
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (animator.avatar == null)
            Debug.LogWarning("[NetSetup] 모델 Animator에 Avatar가 없음 — Humanoid 애니메이션이 재생되지 않을 수 있음");

        // 루트 이동량 → Speed 파라미터. 소유자(CC 이동)·원격(NetworkTransform 보간) 모두 root.position이
        // 갱신되므로 이 하나로 양쪽 다 동작한다(PlayerAnimationDriver 헤더 주석 참고).
        if (!root.GetComponent<PlayerAnimationDriver>())
        {
            var driver = root.AddComponent<PlayerAnimationDriver>();
            var soDriver = new SerializedObject(driver);
            soDriver.FindProperty("animator").objectReferenceValue = animator;
            soDriver.ApplyModifiedPropertiesWithoutUndo();
        }

        return model;
    }

    // ---------- 원격 손전등 ----------
    static Light BuildRemoteFlashlight(GameObject modelRoot, Transform aimSource, Light ownerLight)
    {
        if (!modelRoot) return null;

        var rightHand = FindDescendant(modelRoot.transform, "hand_r");
        var flashlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightModelPrefabPath);
        Transform flashlightMount = rightHand != null ? rightHand : modelRoot.transform;

        if (flashlightPrefab)
        {
            var flashlightModel = (GameObject)PrefabUtility.InstantiatePrefab(flashlightPrefab);
            flashlightModel.name = "RemoteFlashlightModel";
            flashlightModel.transform.SetParent(flashlightMount, false);
            // hand_r의 +Y가 손가락 방향이다. 모델의 검은 손잡이가 손바닥을 통과하고
            // 흰 램프 헤드가 손끝 앞으로 나오도록 배치한다.
            flashlightModel.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            flashlightModel.transform.localRotation = Quaternion.identity;
            flashlightModel.transform.localScale = Vector3.one;
            HideFlatFlashlightLensPlate(flashlightModel);
            flashlightMount = flashlightModel.transform;
        }
        else
        {
            Debug.LogError($"[NetSetup] 원격 손전등 모델 없음: {FlashlightModelPrefabPath}");
        }

        if (!rightHand)
            Debug.LogWarning("[NetSetup] 모델에서 hand_r 본을 찾지 못함 — 원격 손전등을 모델 루트에 부착");

        var lightGo = new GameObject("RemoteFlashlight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = ownerLight ? ownerLight.range : 45f;
        light.spotAngle = ownerLight ? ownerLight.spotAngle : 60f;
        light.intensity = ownerLight ? ownerLight.intensity : 0.8f;
        light.color = ownerLight ? ownerLight.color : new Color(1f, 0.95f, 0.8f);
        light.enabled = false; // 기본 비활성 — FlashlightOn 동기화가 원격에서 켠다

        var aim = lightGo.AddComponent<RemoteFlashlightAim>();
        aim.Configure(aimSource);

        lightGo.transform.SetParent(flashlightMount, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        // Unity Spot Light는 로컬 +Z를 향하므로 손전등 모델의 +Y 방향으로 맞춘다.
        lightGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        return light;
    }

    static void HideFlatFlashlightLensPlate(GameObject flashlightModel)
    {
        var renderer = flashlightModel.GetComponentInChildren<MeshRenderer>(true);
        var invisibleLensMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            InvisibleFlashlightLensMaterialPath);

        if (!renderer)
        {
            Debug.LogError("[NetSetup] Remote flashlight model has no MeshRenderer.");
            return;
        }

        var materials = renderer.sharedMaterials;
        if (materials.Length <= 2)
        {
            Debug.LogError("[NetSetup] Remote flashlight model has no front-lens material slot.");
            return;
        }

        if (!invisibleLensMaterial)
        {
            Debug.LogError($"[NetSetup] Invisible flashlight lens material missing: {InvisibleFlashlightLensMaterialPath}");
            return;
        }

        // The imported FBX stores its flat, emissive front disk in slot 3. Replacing
        // only that slot removes the white plate without touching the model housing
        // or the real Unity Spot Light that illuminates the environment.
        materials[2] = invisibleLensMaterial;
        renderer.sharedMaterials = materials;
    }

    static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDescendant(child, name);
            if (found) return found;
        }
        return null;
    }

    [MenuItem("Game/Net/2. 씬 네트워크 배선")]
    public static void WireScenes()
    {
        WireHomescreen();
        WireGameScene();
    }

    static void WireHomescreen()
    {
        var scene = EditorSceneManager.OpenScene(HomeScenePath);

        // 기존 GO는 무수정 — NetBootstrap 하나만 없으면 만들고, 있으면 컴포넌트만 보강한다.
        var bootstrap = GameObject.Find("NetBootstrap");
        if (!bootstrap) bootstrap = new GameObject("NetBootstrap");

        var nm = EnsureComponent<NetworkManager>(bootstrap);
        var utp = EnsureComponent<UnityTransport>(bootstrap);
        if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
        nm.NetworkConfig.NetworkTransport = utp;
        nm.NetworkConfig.EnableSceneManagement = true;
        // 로비 자동 스폰 회귀 수정: ConnectionApproval을 켜야 PlayerSpawner의 콜백이 개입해
        // CreatePlayerObject=false를 응답할 수 있다(꺼져 있으면 NGO가 PlayerPrefab!=null만 보고
        // 연결 즉시 자동 스폰 — 로비에서 NetPlayer가 스폰되는 원인이었다). 실제 승인 로직은
        // PlayerSpawner.OnConnectionApproval(런타임 콜백)이 담당.
        nm.NetworkConfig.ConnectionApproval = true;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (!playerPrefab)
            Debug.LogError($"[NetSetup] {PlayerPrefabPath} 없음 — 먼저 'Game/Net/1. 플레이어 프리팹 생성'을 실행하세요.");
        else
            nm.NetworkConfig.PlayerPrefab = playerPrefab;

        EnsureComponent<GameBootstrap>(bootstrap);
        EnsureComponent<SessionManager>(bootstrap);
        EnsureComponent<VoiceManager>(bootstrap);
        EnsureComponent<NetworkManagerGuard>(bootstrap);
        EnsureComponent<MultiplayerMenu>(bootstrap); // Task 6 — Homescreen 전용, Awake에서 UGUI 코드 생성
        EnsureComponent<PlayerSpawner>(bootstrap);   // 로비 자동 스폰 방지 + GameScene 진입 시 수동 스폰

        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[NetSetup] Homescreen 배선 완료");
    }

    static void WireGameScene()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath);

        AttachAdapter<DrawerShelf, NetToggleSync>("DrawerShelf");
        AttachAdapter<LeverInteractable, NetToggleSync>("LeverInteractable");
        AttachAdapter<DoorInteractable, NetToggleSync>("DoorInteractable");
        AttachAdapter<BoardedDoor, NetToggleSync>("BoardedDoor");   // 문 열림 상태
        AttachAdapter<WoodBoard, NetToggleSync>("WoodBoard");       // 판자 제거 상태
        // v.2 병합: MonitorDoorController/MonsterChaseTrigger/MonsterJumpscareTrigger 기능 제거됨
        AttachAdapter<JumpscareTriggerPart2, NetOneShotSync>("JumpscareTriggerPart2");

        AttachAdapter<PickupItem, NetPickupSync>("PickupItem");
        FixNestedPickupParentSync();

        // Task 8 + 최종 리뷰 Critical 1: NetworkObject + NetworkTransform(서버 권위 기본) +
        // MonsterNetAdapter 부착. NGO의 PopulateScenePlacedObjects는 활성 상태인 in-scene
        // NetworkObject만 스폰 대상으로 등록한다 — 몬스터 GO가 비활성이면 영구 미스폰이 되어
        // 사망/게임오버 체인 전체가 죽는다(리뷰에서 발견, 원인은 Door Teleport.Start()가 objectToEnable을
        // SetActive(false)로 끄는 것 — 별도로 수정함). 여기서는 GO가 활성 상태로 저장되도록 방어적으로
        // 보정만 한다. MonsterLookAI/NavMeshAgent 컴포넌트 자체는 건드리지 않는다 — 끄면
        // MonsterLookAI.Start()가 지연되어 objectDisabledAtStart(DeathScreenController) 초기화 타이밍이
        // 어긋나는 회귀가 생긴다(오프라인 회귀 테스트로 확인). "아직 깨어나지 않음"은 대신
        // MonsterNetAdapter.Awake()가 MonsterLookAI.SetAwake(false)로 표현한다(렌더러/콜라이더만 감춤).
        int monsterCount = 0;
        foreach (var m in Object.FindObjectsByType<MonsterLookAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = m.gameObject;
            if (!go.activeSelf) { go.SetActive(true); EditorUtility.SetDirty(go); }

            // 이전 리비전에서 씬에 저장됐을 수 있는 ai/agent 비활성 상태를 되돌린다(재실행 수렴성) —
            // 이 컴포넌트들은 항상 켜진 채로 저장돼야 한다(MonsterNetAdapter.Awake가 런타임에 재운다).
            if (!m.enabled) { m.enabled = true; EditorUtility.SetDirty(m); }
            var agent = m.agent != null ? m.agent : go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && !agent.enabled) { agent.enabled = true; EditorUtility.SetDirty(agent); }

            if (!go.GetComponent<NetworkObject>()) go.AddComponent<NetworkObject>();
            if (!go.GetComponent<NetworkTransform>()) go.AddComponent<NetworkTransform>(); // AuthorityMode 기본값 Server 유지
            if (!go.GetComponent<MonsterNetAdapter>()) go.AddComponent<MonsterNetAdapter>(); // ai/agent는 OnNetworkSpawn이 GetComponent로 자동 배선
            monsterCount++;
        }
        Debug.Log($"[NetSetup] MonsterLookAI+MonsterNetAdapter: {monsterCount}개 배선 (GO·ai·agent 모두 활성 상태로 씬 저장 — 잠듦은 런타임에 MonsterNetAdapter.Awake가 표현)");

        FixSceneNetworkObjectHashes(scene, GameScenePath);
        Debug.Log("[NetSetup] GameScene 배선 완료");
    }

    /// GO에 NetworkObject(없으면)와 어댑터 컴포넌트(없으면)를 부착한다. 재실행해도 중복 부착되지 않는다.
    static void AttachAdapter<TFind, TAdapter>(string label)
        where TFind : Component
        where TAdapter : Component
    {
        var found = Object.FindObjectsByType<TFind>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var comp in found)
        {
            var go = comp.gameObject;
            if (!go.GetComponent<NetworkObject>()) go.AddComponent<NetworkObject>();
            if (!go.GetComponent<TAdapter>()) go.AddComponent<TAdapter>();
            count++;
        }
        Debug.Log($"[NetSetup] {label}: {count}개 배선");
    }

    /// jungwon 함정 ⑧: Hammer/Axe/CrowBar(PickupItem)는 BoardedDoor(자체 NetworkObject)의 자식으로
    /// 배치돼 있어, NetworkObject 기본값 AutoObjectParentSync=true가 PickupItem.Start()의
    /// transform.SetParent(스폰 포인트로 이동)를 되돌려버린다 — "[Netcode] networkManager is not
    /// listening, start a server or host before re-parenting" 경고와 함께 조용히 원래 부모(BoardedDoor)로
    /// 되돌아가는 회귀가 오프라인(싱글)에서도 발생함을 회귀 게이트에서 확인했다(위치 값 자체는
    /// 스폰 포인트로 정상 이동하지만 부모 계층만 되돌아감).
    /// 픽업 동기화(NetPickupSync)는 NetworkVariable로만 이뤄지고 NGO의 부모-계층 자동 동기화에
    /// 의존하지 않으므로, 이 GO들만 부모 NetworkObject를 유지한 채(중첩 구조 자체는 건드리지 않고)
    /// AutoObjectParentSync를 꺼서 NGO가 이 트랜스폼의 부모 변경에 더 이상 개입하지 않게 한다.
    static void FixNestedPickupParentSync()
    {
        int count = 0;
        foreach (var p in Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var no = p.GetComponent<NetworkObject>();
            if (no == null) continue;
            no.AutoObjectParentSync = false;
            EditorUtility.SetDirty(no);
            count++;
        }
        Debug.Log($"[NetSetup] PickupItem AutoObjectParentSync=false: {count}개 적용(중첩 NetworkObject 재부모 회귀 방지)");
    }

    static void AttachNetworkObjectOnly<TFind>(string label) where TFind : Component
    {
        var found = Object.FindObjectsByType<TFind>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var comp in found)
        {
            var go = comp.gameObject;
            if (!go.GetComponent<NetworkObject>()) go.AddComponent<NetworkObject>();
            count++;
        }
        Debug.Log($"[NetSetup] {label}: {count}개 배선(NetworkObject만)");
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    /// jungwon 함정 ⑦: in-scene NetworkObject GlobalObjectIdHash는
    /// 씬 저장 → OnValidate 리플렉션 재호출 → 재저장해야 0이 아니다.
    public static void FixSceneNetworkObjectHashes(Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
        var method = typeof(NetworkObject).GetMethod("OnValidate",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new System.MissingMethodException("NetworkObject.OnValidate 리플렉션 실패 — NGO 버전 확인");
        foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { method.Invoke(no, null); EditorUtility.SetDirty(no); }
        EditorSceneManager.SaveScene(scene, path);
        foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(no);
            if (so.FindProperty("GlobalObjectIdHash").uintValue == 0)
                Debug.LogError($"[NetSetup] 해시 0: {no.name}");
        }
    }
}
