using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Net
{
    /// 다른 플레이어의 손전등이 공기 중에 만드는 희미한 빛줄기.
    /// 네트워크 상태를 따로 복제하지 않고 이미 동기화된 Light의 활성/밝기/방향을 표현한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class RemoteFlashlightBeam : MonoBehaviour
    {
        const string BeamShaderName = "UI/Default";
        const int ConeSegments = 24;
        const float TraceInterval = 0.05f;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] Light sourceLight;
        [SerializeField, Min(0.5f)] float maxVisualRange = 10f;
        [SerializeField, Range(0.005f, 0.3f)] float beamAlpha = 0.022f;
        [SerializeField, Min(0f)] float wallClearance = 0.05f;

        readonly RaycastHit[] _hits = new RaycastHit[16];

        Transform _ownerRoot;
        GameObject _visualRoot;
        Transform _beamTransform;
        Material _beamMaterial;
        Mesh _beamMesh;
        float _targetLength;
        float _displayedLength;
        float _nextTraceTime;

        public void Configure(Light lightSource)
        {
            sourceLight = lightSource;
        }

        void Awake()
        {
            if (sourceLight == null) sourceLight = GetComponent<Light>();
            var player = GetComponentInParent<NetPlayer>();
            _ownerRoot = player != null ? player.transform : transform.root;
            EnsureVisuals();
        }

        void OnEnable()
        {
            EnsureVisuals();
        }

        void LateUpdate()
        {
            if (sourceLight == null) sourceLight = GetComponent<Light>();
            EnsureVisuals();
            if (_visualRoot == null || sourceLight == null) return;

            bool visible = sourceLight.isActiveAndEnabled && sourceLight.intensity > 0.001f;
            if (_visualRoot.activeSelf != visible) _visualRoot.SetActive(visible);
            if (!visible) return;

            float cappedRange = Mathf.Min(Mathf.Max(0f, sourceLight.range), maxVisualRange);
            if (Time.unscaledTime >= _nextTraceTime)
            {
                _targetLength = TraceVisibleLength(cappedRange);
                _nextTraceTime = Time.unscaledTime + TraceInterval;
            }

            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            _displayedLength = Mathf.Lerp(_displayedLength, _targetLength, blend);
            if (_displayedLength <= 0.001f)
            {
                _beamTransform.gameObject.SetActive(false);
                return;
            }

            if (!_beamTransform.gameObject.activeSelf) _beamTransform.gameObject.SetActive(true);
            float radius = CalculateBeamRadius(_displayedLength, sourceLight.spotAngle);
            _beamTransform.localScale = new Vector3(radius, radius, _displayedLength);

            float intensityFactor = Mathf.Clamp01(sourceLight.intensity / 0.8f);
            Color lightColor = sourceLight.color.linear;
            _beamMaterial.SetColor(ColorId,
                new Color(lightColor.r, lightColor.g, lightColor.b, beamAlpha * intensityFactor));
        }

        float TraceVisibleLength(float range)
        {
            if (range <= 0f) return 0f;

            int count = Physics.RaycastNonAlloc(
                transform.position,
                transform.forward,
                _hits,
                range,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float nearest = -1f;
            for (int i = 0; i < count; i++)
            {
                var collider = _hits[i].collider;
                if (collider == null || IsOwnedCollider(collider.transform)) continue;
                if (nearest < 0f || _hits[i].distance < nearest) nearest = _hits[i].distance;
            }

            return CalculateTargetLength(range, nearest, wallClearance);
        }

        bool IsOwnedCollider(Transform candidate)
        {
            return candidate != null && _ownerRoot != null &&
                   (candidate == _ownerRoot || candidate.IsChildOf(_ownerRoot));
        }

        public static float CalculateTargetLength(float maxRange, float nearestHitDistance, float clearance)
        {
            float safeRange = Mathf.Max(0f, maxRange);
            if (nearestHitDistance < 0f) return safeRange;
            return Mathf.Min(safeRange, Mathf.Max(0f, nearestHitDistance - Mathf.Max(0f, clearance)));
        }

        public static float CalculateBeamRadius(float length, float gameplaySpotAngle)
        {
            float visualFullAngle = Mathf.Clamp(gameplaySpotAngle * 0.3f, 3f, 18f);
            return Mathf.Max(0f, length) * Mathf.Tan(visualFullAngle * 0.5f * Mathf.Deg2Rad);
        }

        public static Shader FindRuntimeShader()
        {
            return Shader.Find(BeamShaderName);
        }

        void EnsureVisuals()
        {
            if (_visualRoot != null) return;

            Shader shader = FindRuntimeShader();
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[RemoteFlashlightBeam] 빌드에 포함된 호환 셰이더를 찾지 못함", this);
                enabled = false;
                return;
            }

            _visualRoot = CreateRuntimeGameObject("VisibleFlashlightBeam");
            _visualRoot.transform.SetParent(transform, false);

            _beamMaterial = CreateRuntimeMaterial(shader, "Remote Flashlight Beam Material");

            var beamObject = CreateRuntimeGameObject("BeamVolume");
            beamObject.transform.SetParent(_visualRoot.transform, false);
            _beamTransform = beamObject.transform;
            _beamMesh = CreateConeMesh(ConeSegments);
            ConfigureRenderer(beamObject, _beamMesh, _beamMaterial);

            _targetLength = sourceLight != null
                ? Mathf.Min(sourceLight.range, maxVisualRange)
                : maxVisualRange;
            _displayedLength = _targetLength;
            _visualRoot.SetActive(false);
        }

        static GameObject CreateRuntimeGameObject(string objectName)
        {
            return new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
        }

        static Material CreateRuntimeMaterial(Shader shader, string materialName)
        {
            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };
        }

        static void ConfigureRenderer(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        static Mesh CreateConeMesh(int segments)
        {
            const int shellCount = 8;
            const int verticesPerSegment = 3;
            const int triangleIndicesPerSegment = 9;
            const float apexDistance = 0.02f;
            const float fadeInDistance = 0.08f;

            var vertices = new Vector3[segments * verticesPerSegment * shellCount];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * triangleIndicesPerSegment * shellCount];

            for (int shell = 0; shell < shellCount; shell++)
            {
                float normalizedRadius = (shell + 1f) / shellCount;
                float nearAlpha = Mathf.Lerp(0.3f, 0.018f, Mathf.Pow(normalizedRadius, 1.4f));
                float farAlpha = nearAlpha * 0.05f;
                int vertexBase = shell * segments * verticesPerSegment;
                int triangleBase = shell * segments * triangleIndicesPerSegment;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    int apexIndex = vertexBase + i;
                    int fadeIndex = vertexBase + segments + i;
                    int farIndex = vertexBase + segments * 2 + i;
                    vertices[apexIndex] = new Vector3(0f, 0f, apexDistance);
                    vertices[fadeIndex] = new Vector3(
                        x * fadeInDistance * normalizedRadius,
                        y * fadeInDistance * normalizedRadius,
                        fadeInDistance);
                    vertices[farIndex] = new Vector3(x * normalizedRadius, y * normalizedRadius, 1f);
                    colors[apexIndex] = new Color(1f, 1f, 1f, 0f);
                    colors[fadeIndex] = new Color(1f, 1f, 1f, nearAlpha);
                    colors[farIndex] = new Color(1f, 1f, 1f, farAlpha);

                    int next = (i + 1) % segments;
                    int nextFade = vertexBase + segments + next;
                    int nextFar = vertexBase + segments * 2 + next;
                    int offset = triangleBase + i * triangleIndicesPerSegment;
                    triangles[offset] = apexIndex;
                    triangles[offset + 1] = nextFade;
                    triangles[offset + 2] = fadeIndex;
                    triangles[offset + 3] = fadeIndex;
                    triangles[offset + 4] = nextFade;
                    triangles[offset + 5] = nextFar;
                    triangles[offset + 6] = fadeIndex;
                    triangles[offset + 7] = nextFar;
                    triangles[offset + 8] = farIndex;
                }
            }

            var mesh = new Mesh
            {
                name = "Runtime Remote Flashlight Beam",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        void OnDestroy()
        {
            DestroyRuntimeObject(_beamMaterial);
            DestroyRuntimeObject(_beamMesh);
            if (_visualRoot != null) DestroyRuntimeObject(_visualRoot);
        }

        static void DestroyRuntimeObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
