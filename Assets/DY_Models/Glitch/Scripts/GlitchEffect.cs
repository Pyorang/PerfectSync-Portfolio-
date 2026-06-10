using System.Collections.Generic;
using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 장애물용 글리치 이펙트.
    /// AllIn13DShader 의 Glitch 기능( _GLITCH_ON )을 런타임에 활성화하고,
    /// 인스펙터에서 노출된 값들을 머티리얼 인스턴스에 적용한다.
    ///
    /// - HologramEffect 와 동일 패턴: Renderer.sharedMaterials 를 복제 후 인스턴스에만 적용
    /// - AllIn13DShader 를 쓰는 머티리얼만 처리
    /// - Glitch 는 Vertex(Mesh) 이펙트라 블렌드 모드 변경 불필요
    /// - 에디터에서 값 변경 시 OnValidate 로 즉시 반영
    /// </summary>
    public class GlitchEffect : MonoBehaviour
    {
        // ====================================
        //  Glitch Core
        // ====================================
        [Header("Glitch Core")]

        [Tooltip("글리치 타일링. 값이 클수록 작은 조각으로 잘게 흔들림")]
        public float glitchTiling = 5f;

        [Tooltip("글리치 강도 (0: 없음, 1: 최대 왜곡)")]
        [Range(0f, 1f)]
        public float glitchAmount = 0.5f;

        [Tooltip("글리치 오프셋 방향/크기. 각 축이 글리치 조각이 튀는 방향의 가중치")]
        public Vector3 glitchOffset = new Vector3(-0.5f, 0f, 0f);

        [Tooltip("글리치 애니메이션 속도")]
        public float glitchSpeed = 2.5f;

        [Tooltip("World Space 기준으로 글리치 계산 (켜면 오브젝트 이동/회전과 독립적으로 흔들림)")]
        public bool glitchWorldSpace = true;

        // ====================================
        //  Extra
        // ====================================
        [Header("Extra")]

        [Tooltip("디버그 로그 출력")]
        public bool debugLog = false;

        // ====================================
        //  Internal
        // ====================================

        // Glitch property IDs (cached)
        private static readonly int ID_Glitch           = Shader.PropertyToID("_Glitch");
        private static readonly int ID_GlitchTiling     = Shader.PropertyToID("_GlitchTiling");
        private static readonly int ID_GlitchAmount     = Shader.PropertyToID("_GlitchAmount");
        private static readonly int ID_GlitchOffset     = Shader.PropertyToID("_GlitchOffset");
        private static readonly int ID_GlitchSpeed      = Shader.PropertyToID("_GlitchSpeed");
        private static readonly int ID_GlitchWorldSpace = Shader.PropertyToID("_GlitchWorldSpace");

        // Keywords
        private const string KW_GLITCH = "_GLITCH_ON";

        private readonly List<Material> _instances = new List<Material>();
        private bool _initialized = false;

        // ====================================
        //  Unity Lifecycle
        // ====================================

        private void Awake()
        {
            PrepareInstances();
            ApplyToMaterials();
            _initialized = true;
        }

        private void OnValidate()
        {
            if (!_initialized) return;
            if (_instances.Count == 0) return;
            ApplyToMaterials();
        }

        private void OnDestroy()
        {
            foreach (var m in _instances)
            {
                if (m != null) Destroy(m);
            }
            _instances.Clear();
        }

        // ====================================
        //  Setup
        // ====================================

        private void PrepareInstances()
        {
            _instances.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            int rendererCount = 0;

            foreach (Renderer rend in renderers)
            {
                Material[] shared = rend.sharedMaterials;
                bool anyReplaced = false;
                Material[] newMats = new Material[shared.Length];
                System.Array.Copy(shared, newMats, shared.Length);

                for (int i = 0; i < shared.Length; i++)
                {
                    Material src = shared[i];
                    if (src == null || src.shader == null) continue;
                    if (!src.shader.name.Contains("AllIn13DShader")) continue;

                    Material instance = new Material(src);
                    instance.name = src.name + " (GlitchEffect)";
                    newMats[i] = instance;
                    _instances.Add(instance);
                    anyReplaced = true;
                }

                if (anyReplaced)
                {
                    rend.sharedMaterials = newMats;
                    rendererCount++;
                }
            }

            if (debugLog)
                Debug.Log($"[GlitchEffect] Instanced {_instances.Count} material(s) across {rendererCount} renderer(s) on '{name}'.");
        }

        // ====================================
        //  Apply
        // ====================================

        private void ApplyToMaterials()
        {
            foreach (var m in _instances)
            {
                if (m == null) continue;

                // --- Glitch ON ---
                m.EnableKeyword(KW_GLITCH);
                m.SetFloat(ID_Glitch, 1f);

                m.SetFloat(ID_GlitchTiling, glitchTiling);
                m.SetFloat(ID_GlitchAmount, glitchAmount);
                m.SetVector(ID_GlitchOffset, new Vector4(glitchOffset.x, glitchOffset.y, glitchOffset.z, 0f));
                m.SetFloat(ID_GlitchSpeed, glitchSpeed);
                m.SetFloat(ID_GlitchWorldSpace, glitchWorldSpace ? 1f : 0f);
            }
        }
    }
}
