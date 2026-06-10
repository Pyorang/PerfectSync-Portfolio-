using System.Collections.Generic;
using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 장애물용 홀로그램 이펙트.
    /// AllIn13DShader 의 Hologram 기능( _HOLOGRAM_ON )을 런타임에 활성화하고,
    /// 인스펙터에서 노출된 값들을 머티리얼 인스턴스에 적용한다.
    ///
    /// - ObstacleHueCycle 과 동일한 방식으로 Renderer.sharedMaterials 를 복제하여 인스턴스화
    /// - AllIn13DShader 를 쓰는 머티리얼만 처리 (그 외 머티리얼은 건드리지 않음)
    /// - 효과는 정적(셰이더 타임 기반 스크롤은 셰이더가 자체 처리) → Update 루프 불필요
    /// - 에디터에서 값 변경 시 OnValidate 로 즉시 반영
    /// </summary>
    public class HologramEffect : MonoBehaviour
    {
        // ====================================
        //  Hologram Core
        // ====================================
        [Header("Hologram Core")]

        [Tooltip("Hologram main color (HDR). Bloom 과 조합되는 발광 컬러")]
        [ColorUsage(true, true)]
        public Color hologramColor = new Color(1.25f, 2.8f, 6.8f, 1f);

        [Tooltip("Hologram 선 방향 (Object Space). 기본 (0,1,0) = 수직 스캔라인")]
        public Vector3 lineDirection = new Vector3(0f, 1f, 0f);

        [Tooltip("홀로그램 베이스 알파. 오브젝트 본체가 얼마나 비쳐 보이는지 (0: 거의 투명, 1: 불투명)")]
        [Range(0f, 1f)]
        public float baseAlpha = 0.1f;

        [Tooltip("홀로그램 전체 알파 (메인 라인)")]
        [Range(0f, 1f)]
        public float hologramAlpha = 1f;

        [Tooltip("런타임에 이 오브젝트의 머티리얼 인스턴스만 Transparent 블렌드로 전환. " +
                 "원본 공유 머티리얼은 건드리지 않으므로 다른 오브젝트에는 영향 없음. " +
                 "기반 머티리얼이 Opaque 여도 이 오브젝트만 반투명으로 나오게 하려면 켬.")]
        public bool forceTransparentBlend = true;

        // ====================================
        //  Main Lines
        // ====================================
        [Header("Main Lines")]

        [Tooltip("메인 홀로그램 라인 스크롤 속도")]
        public float scrollSpeed = 2f;

        [Tooltip("메인 홀로그램 라인 빈도 (선 개수)")]
        public float frequency = 20f;

        [Tooltip("라인 중심 위치 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float lineCenter = 0.5f;

        [Tooltip("라인 간격")]
        [Range(0.001f, 5f)]
        public float lineSpacing = 2f;

        [Tooltip("라인 경계 부드러움")]
        [Range(0.01f, 5f)]
        public float lineSmoothness = 2f;

        // ====================================
        //  Accent Lines
        // ====================================
        [Header("Accent Lines")]

        [Tooltip("악센트(보조) 라인 속도")]
        public float accentSpeed = 1f;

        [Tooltip("악센트 라인 빈도")]
        public float accentFrequency = 2f;

        [Tooltip("악센트 라인 알파")]
        [Range(0f, 1f)]
        public float accentAlpha = 0.5f;

        // ====================================
        //  Glow (Rim / Emission)
        // ====================================
        [Header("Rim Glow (실루엣 발광)")]

        [Tooltip("Rim Lighting (Fresnel) 활성화 — 오브젝트 외곽이 빛나는 효과")]
        public bool enableRim = true;

        [Tooltip("Rim 컬러 (HDR). Intensity 를 올리면 Bloom 과 만나 강하게 발광")]
        [ColorUsage(true, true)]
        public Color rimColor = new Color(0.5f, 1.5f, 3f, 1f);

        [Tooltip("Rim 감쇠. 1 에 가까울수록 엣지만, 0 에 가까울수록 넓게 퍼짐")]
        [Range(0f, 1f)]
        public float rimAttenuation = 0.9f;

        [Tooltip("Rim 이 나타나기 시작하는 최소 각도 (0: 정면까지 포함)")]
        [Range(0f, 1f)]
        public float rimMin = 0f;

        [Tooltip("Rim 이 최대가 되는 각도 (1: 완전 외곽)")]
        [Range(0f, 1f)]
        public float rimMax = 1f;

        [Header("Emission (전체 발광)")]

        [Tooltip("Emission (Self Glow) 활성화 — 오브젝트 전체를 발광시켜 전반적으로 밝게")]
        public bool enableEmission = true;

        [Tooltip("Emission 컬러 (HDR)")]
        [ColorUsage(true, true)]
        public Color emissionColor = new Color(0.4f, 1f, 2f, 1f);

        [Tooltip("Self Glow 강도 (0 ~ 20)")]
        [Range(0f, 20f)]
        public float emissionSelfGlow = 2f;

        // ====================================
        //  Extra
        // ====================================
        [Header("Extra")]

        [Tooltip("디버그 로그 출력")]
        public bool debugLog = false;

        // ====================================
        //  Internal
        // ====================================

        // Hologram property IDs (cached)
        private static readonly int ID_Hologram              = Shader.PropertyToID("_Hologram");
        private static readonly int ID_HologramColor         = Shader.PropertyToID("_HologramColor");
        private static readonly int ID_HologramLineDirection = Shader.PropertyToID("_HologramLineDirection");
        private static readonly int ID_HologramBaseAlpha     = Shader.PropertyToID("_HologramBaseAlpha");
        private static readonly int ID_HologramScrollSpeed   = Shader.PropertyToID("_HologramScrollSpeed");
        private static readonly int ID_HologramFrequency     = Shader.PropertyToID("_HologramFrequency");
        private static readonly int ID_HologramAlpha         = Shader.PropertyToID("_HologramAlpha");
        private static readonly int ID_HologramAccentSpeed   = Shader.PropertyToID("_HologramAccentSpeed");
        private static readonly int ID_HologramAccentFreq    = Shader.PropertyToID("_HologramAccentFrequency");
        private static readonly int ID_HologramAccentAlpha   = Shader.PropertyToID("_HologramAccentAlpha");
        private static readonly int ID_HologramLineCenter    = Shader.PropertyToID("_HologramLineCenter");
        private static readonly int ID_HologramLineSpacing   = Shader.PropertyToID("_HologramLineSpacing");
        private static readonly int ID_HologramLineSmooth    = Shader.PropertyToID("_HologramLineSmoothness");

        // Rim / Emission property IDs
        private static readonly int ID_RimLighting           = Shader.PropertyToID("_RimLighting");
        private static readonly int ID_RimColor              = Shader.PropertyToID("_RimColor");
        private static readonly int ID_RimAttenuation        = Shader.PropertyToID("_RimAttenuation");
        private static readonly int ID_MinRim                = Shader.PropertyToID("_MinRim");
        private static readonly int ID_MaxRim                = Shader.PropertyToID("_MaxRim");
        private static readonly int ID_EmissionEnabled       = Shader.PropertyToID("_EmissionEnabled");
        private static readonly int ID_EmissionColor         = Shader.PropertyToID("_EmissionColor");
        private static readonly int ID_EmissionSelfGlow      = Shader.PropertyToID("_EmissionSelfGlow");
        private static readonly int ID_EmissionMap           = Shader.PropertyToID("_EmissionMap");

        // Blend / ZWrite property IDs (AllIn13DShader 공용)
        private static readonly int ID_BlendSrc              = Shader.PropertyToID("_BlendSrc");
        private static readonly int ID_BlendDst              = Shader.PropertyToID("_BlendDst");
        private static readonly int ID_ZWrite                = Shader.PropertyToID("_ZWrite");

        // Keywords
        private const string KW_HOLOGRAM                    = "_HOLOGRAM_ON";
        private const string KW_RIM_LIGHTING                = "_RIM_LIGHTING_ON";
        private const string KW_EMISSION                    = "_EMISSION_ON";

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
            // 에디터/런타임 중 값 변경 시, 이미 인스턴스화된 머티리얼이 있다면 즉시 반영.
            // (Awake 이전이면 스킵 — 인스턴스가 아직 없음)
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
                    instance.name = src.name + " (HologramEffect)";
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
                Debug.Log($"[HologramEffect] Instanced {_instances.Count} material(s) across {rendererCount} renderer(s) on '{name}'.");
        }

        // ====================================
        //  Apply
        // ====================================

        private void ApplyToMaterials()
        {
            foreach (var m in _instances)
            {
                if (m == null) continue;

                // --- Transparent blend (선택) ---
                // 이 머티리얼은 이미 인스턴스(복제본) 이므로, 여기서 블렌드를 바꿔도
                // 원본 공유 머티리얼을 쓰는 다른 오브젝트에는 영향이 없다.
                if (forceTransparentBlend)
                {
                    m.SetFloat(ID_BlendSrc, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat(ID_BlendDst, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat(ID_ZWrite, 0f);
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000
                }

                // --- Hologram ON ---
                m.EnableKeyword(KW_HOLOGRAM);
                m.SetFloat(ID_Hologram, 1f);

                m.SetColor(ID_HologramColor, hologramColor);
                m.SetVector(ID_HologramLineDirection, new Vector4(lineDirection.x, lineDirection.y, lineDirection.z, 0f));
                m.SetFloat(ID_HologramBaseAlpha, baseAlpha);
                m.SetFloat(ID_HologramAlpha, hologramAlpha);

                m.SetFloat(ID_HologramScrollSpeed, scrollSpeed);
                m.SetFloat(ID_HologramFrequency, frequency);
                m.SetFloat(ID_HologramLineCenter, lineCenter);
                m.SetFloat(ID_HologramLineSpacing, lineSpacing);
                m.SetFloat(ID_HologramLineSmooth, lineSmoothness);

                m.SetFloat(ID_HologramAccentSpeed, accentSpeed);
                m.SetFloat(ID_HologramAccentFreq, accentFrequency);
                m.SetFloat(ID_HologramAccentAlpha, accentAlpha);

                // --- Rim Lighting (Fresnel 실루엣 발광) ---
                if (enableRim)
                {
                    m.EnableKeyword(KW_RIM_LIGHTING);
                    m.SetFloat(ID_RimLighting, 1f);
                    m.SetColor(ID_RimColor, rimColor);
                    m.SetFloat(ID_RimAttenuation, rimAttenuation);
                    m.SetFloat(ID_MinRim, rimMin);
                    m.SetFloat(ID_MaxRim, rimMax);
                }
                else
                {
                    m.DisableKeyword(KW_RIM_LIGHTING);
                    m.SetFloat(ID_RimLighting, 0f);
                }

                // --- Emission (전체 Self Glow) ---
                if (enableEmission)
                {
                    m.EnableKeyword(KW_EMISSION);
                    m.SetFloat(ID_EmissionEnabled, 1f);
                    // _EmissionMap 이 albedo 텍스처로 꽂혀 있으면 어두운 픽셀이 Emission 을 깎아
                    // 머티리얼마다 발광 세기가 달라 보이는 문제가 생김 (예: Color 머티리얼).
                    // 인스턴스에서만 맵을 비워 샘플러 기본값(흰색)으로 만들면
                    // _EmissionColor × _EmissionSelfGlow 가 텍스처에 영향 없이 균일하게 반영된다.
                    // 원본 공유 머티리얼은 건드리지 않음.
                    m.SetTexture(ID_EmissionMap, null);
                    m.SetColor(ID_EmissionColor, emissionColor);
                    m.SetFloat(ID_EmissionSelfGlow, emissionSelfGlow);
                }
                else
                {
                    m.DisableKeyword(KW_EMISSION);
                    m.SetFloat(ID_EmissionEnabled, 0f);
                }
            }
        }
    }
}
