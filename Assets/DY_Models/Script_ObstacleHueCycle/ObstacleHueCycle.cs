using System.Collections.Generic;
using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 장애물용 영구 Hue Cycle 이펙트.
    /// 게임 시작과 동시에 AllIn13DShader의 Hue Shift / Emission / Rim Lighting 을 활성화하고,
    /// 런타임 동안 계속 색이 변하도록 한다.
    ///
    /// - Fade In/Out, Warning Blink, Activate/Deactivate 기능 없음 (영구 적용)
    /// - 원본 머티리얼 복원 로직 없음 (OnDestroy 시 생성된 인스턴스 머티리얼만 파괴)
    /// - 여러 인스턴스가 독립적으로 사이클링 (randomizeStartHue 로 자연스러운 비동기화)
    /// - hue 범위 제한 / 인스턴스별 시작 오프셋 / 머티리얼별 phase spread 지원
    /// </summary>
    public class ObstacleHueCycle : MonoBehaviour
    {
        // ====================================
        //  Color Settings
        // ====================================
        [Header("Color Settings")]

        [Tooltip("Enable rainbow hue cycling on base texture. If OFF, original colors are kept and only glow is added")]
        public bool enableHueShift = true;

        [Tooltip("Hue cycle speed (degrees/sec). Higher = faster rainbow")]
        [Range(30f, 600f)]
        public float hueSpeed = 180f;

        [Tooltip("Saturation (0: gray, 4: vivid)")]
        [Range(0f, 4f)]
        public float hueSaturation = 1.8f;

        [Tooltip("Rim light hue offset from body color")]
        [Range(0f, 360f)]
        public float rimHueOffset = 90f;

        [Tooltip("Emission hue offset from body color")]
        [Range(0f, 360f)]
        public float emissionHueOffset = 0f;

        [Tooltip("Starting hue offset for this instance (deg). Used when randomizeStartHue is OFF.")]
        [Range(0f, 360f)]
        public float hueStartOffset = 0f;

        [Tooltip("Randomize starting hue on Awake. Makes multiple obstacles naturally out of sync.")]
        public bool randomizeStartHue = true;

        [Tooltip("Limit hue to a range (ping-pong between Min/Max). If OFF, full 0~360 cycling.")]
        public bool limitHueRange = false;

        [Tooltip("Hue range minimum (deg) when limitHueRange is ON")]
        [Range(0f, 360f)]
        public float hueRangeMin = 0f;

        [Tooltip("Hue range maximum (deg) when limitHueRange is ON")]
        [Range(0f, 360f)]
        public float hueRangeMax = 60f;

        // ====================================
        //  Brightness Settings
        // ====================================
        [Header("Brightness Settings")]

        [Tooltip("Hue Shift brightness (0: dark, 2: very bright)")]
        [Range(0f, 2f)]
        public float hueBrightness = 1.7f;

        [Tooltip("Emission self glow intensity (0~20)")]
        [Range(0f, 20f)]
        public float emissionSelfGlow = 8f;

        [Tooltip("Emission color HDR intensity multiplier")]
        [Range(0f, 5f)]
        public float emissionIntensity = 2.5f;

        [Tooltip("Rim light attenuation (0: none, 1: max)")]
        [Range(0f, 1f)]
        public float rimAttenuation = 0.9f;

        [Tooltip("Rim min range")]
        [Range(0f, 1f)]
        public float rimMin = 0f;

        [Tooltip("Rim max range")]
        [Range(0f, 1f)]
        public float rimMax = 1f;

        [Tooltip("Rim light HDR intensity multiplier")]
        [Range(0f, 5f)]
        public float rimIntensity = 3f;

        // ====================================
        //  Cycle Settings
        // ====================================
        [Header("Cycle Settings")]

        [Tooltip("Enable brightness pulse")]
        public bool enableBrightnessPulse = true;

        [Tooltip("Brightness pulse speed (Hz)")]
        [Range(0.5f, 10f)]
        public float brightnessPulseSpeed = 3f;

        [Tooltip("Brightness pulse min multiplier (1.0 = no pulse)")]
        [Range(0.3f, 1f)]
        public float brightnessPulseMin = 0.7f;

        [Tooltip("Enable per-material hue phase offset (each material sits at a different hue).")]
        public bool enablePerMaterialHuePhase = false;

        [Tooltip("Per-material phase spread (deg). Each instanced material gets a phase offset within 0~spread.")]
        [Range(0f, 360f)]
        public float perMaterialPhaseSpread = 120f;

        // ====================================
        //  Extra Settings
        // ====================================
        [Header("Extra Settings")]

        [Tooltip("Enable debug log output")]
        public bool debugLog = false;

        // ====================================
        //  Internal
        // ====================================

        // Shader property IDs (cached)
        private static readonly int ID_HueShift        = Shader.PropertyToID("_HueShift");
        private static readonly int ID_HueSaturation   = Shader.PropertyToID("_HueSaturation");
        private static readonly int ID_HueBrightness   = Shader.PropertyToID("_HueBrightness");
        private static readonly int ID_EmissionSelfGlow= Shader.PropertyToID("_EmissionSelfGlow");
        private static readonly int ID_EmissionColor   = Shader.PropertyToID("_EmissionColor");
        private static readonly int ID_RimColor        = Shader.PropertyToID("_RimColor");
        private static readonly int ID_RimAttenuation  = Shader.PropertyToID("_RimAttenuation");
        private static readonly int ID_MinRim          = Shader.PropertyToID("_MinRim");
        private static readonly int ID_MaxRim          = Shader.PropertyToID("_MaxRim");

        // Shader keywords
        private const string KW_HUE_SHIFT     = "_HUE_SHIFT_ON";
        private const string KW_EMISSION      = "_EMISSION_ON";
        private const string KW_RIM_LIGHTING  = "_RIM_LIGHTING_ON";

        // Per-material runtime data
        private class MaterialEntry
        {
            public Material material;
            public float phaseOffset; // deg
        }

        private readonly List<MaterialEntry> _entries = new List<MaterialEntry>();
        private float _currentHue = 0f;

        // ====================================
        //  Unity Lifecycle
        // ====================================

        private void Awake()
        {
            PrepareInstances();

            if (randomizeStartHue)
                _currentHue = Random.Range(0f, 360f);
            else
                _currentHue = Mathf.Repeat(hueStartOffset, 360f);
        }

        private void Update()
        {
            // 1. Advance hue
            _currentHue = (_currentHue + hueSpeed * Time.deltaTime) % 360f;

            // 2. Base body hue (respecting range limit if enabled)
            float bodyHueDeg = ComputeBodyHueDeg(_currentHue);

            // 3. Brightness pulse
            float pulseMult = 1f;
            if (enableBrightnessPulse)
            {
                pulseMult = Mathf.Lerp(brightnessPulseMin, 1f,
                    (Mathf.Sin(Time.time * brightnessPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
            }

            ApplyToMaterials(bodyHueDeg, pulseMult);
        }

        private void OnDestroy()
        {
            foreach (var e in _entries)
            {
                if (e.material != null) Destroy(e.material);
            }
            _entries.Clear();
        }

        // ====================================
        //  Setup
        // ====================================

        private void PrepareInstances()
        {
            _entries.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            // Collect AllIn13DShader material indices per renderer
            Dictionary<Renderer, List<int>> rendererToIndices = new Dictionary<Renderer, List<int>>();
            foreach (Renderer rend in renderers)
            {
                Material[] shared = rend.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material m = shared[i];
                    if (m == null || m.shader == null) continue;
                    if (!m.shader.name.Contains("AllIn13DShader")) continue;

                    if (!rendererToIndices.ContainsKey(rend))
                        rendererToIndices[rend] = new List<int>();
                    rendererToIndices[rend].Add(i);
                }
            }

            // Create instance materials and swap in-place
            int matCount = 0;
            foreach (var kvp in rendererToIndices)
            {
                Renderer rend = kvp.Key;
                List<int> indices = kvp.Value;

                Material[] shared = rend.sharedMaterials;
                Material[] newMats = new Material[shared.Length];
                System.Array.Copy(shared, newMats, shared.Length);

                foreach (int idx in indices)
                {
                    Material instance = new Material(shared[idx]);
                    instance.name = shared[idx].name + " (ObstacleHueCycle)";

                    if (enableHueShift)
                    {
                        instance.EnableKeyword(KW_HUE_SHIFT);
                        instance.SetFloat("_HueShiftEnabled", 1f);
                    }
                    instance.EnableKeyword(KW_EMISSION);
                    instance.SetFloat("_EmissionEnabled", 1f);
                    instance.EnableKeyword(KW_RIM_LIGHTING);
                    instance.SetFloat("_RimLighting", 1f);

                    newMats[idx] = instance;

                    float phase = 0f;
                    if (enablePerMaterialHuePhase && perMaterialPhaseSpread > 0f)
                    {
                        // Deterministic-ish spread: evenly distributed + small jitter
                        phase = (matCount * (perMaterialPhaseSpread /
                                 Mathf.Max(1, indices.Count))) % perMaterialPhaseSpread;
                    }

                    _entries.Add(new MaterialEntry { material = instance, phaseOffset = phase });
                    matCount++;
                }

                rend.sharedMaterials = newMats;
            }

            if (debugLog)
                Debug.Log($"[ObstacleHueCycle] Instanced {_entries.Count} material(s) across {rendererToIndices.Count} renderer(s) on '{name}'.");
        }

        // ====================================
        //  Hue math
        // ====================================

        private float ComputeBodyHueDeg(float rawHue)
        {
            if (!limitHueRange) return rawHue;

            float min = Mathf.Min(hueRangeMin, hueRangeMax);
            float max = Mathf.Max(hueRangeMin, hueRangeMax);
            float span = Mathf.Max(0.0001f, max - min);

            // Ping-pong 0..span..0 using a 2*span cycle, then offset by min.
            float t = Mathf.Repeat(rawHue, span * 2f);
            float pp = t <= span ? t : (span * 2f - t);
            return min + pp;
        }

        // ====================================
        //  Apply
        // ====================================

        private void ApplyToMaterials(float bodyHueDeg, float pulseMult)
        {
            float effectiveHueBrightness = hueBrightness * pulseMult;
            float effectiveSaturation    = hueSaturation;
            float effectiveEmissionGlow  = emissionSelfGlow * pulseMult;
            float effectiveRimAtten      = rimAttenuation;

            for (int i = 0; i < _entries.Count; i++)
            {
                MaterialEntry e = _entries[i];
                if (e.material == null) continue;

                float matHue = bodyHueDeg + e.phaseOffset;

                // Body hue shift
                if (enableHueShift)
                {
                    e.material.SetFloat(ID_HueShift, matHue);
                    e.material.SetFloat(ID_HueSaturation, effectiveSaturation);
                    e.material.SetFloat(ID_HueBrightness, effectiveHueBrightness);
                }

                // Emission
                Color emissionHDR = Color.HSVToRGB(
                    Mathf.Repeat((matHue + emissionHueOffset) / 360f, 1f), 0.8f, 1f
                ) * emissionIntensity * pulseMult;
                emissionHDR.a = 1f;

                e.material.SetFloat(ID_EmissionSelfGlow, effectiveEmissionGlow);
                e.material.SetColor(ID_EmissionColor, emissionHDR);

                // Rim
                Color rimHDR = Color.HSVToRGB(
                    Mathf.Repeat((matHue + rimHueOffset) / 360f, 1f), 0.9f, 1f
                ) * rimIntensity;
                rimHDR.a = 1f;

                e.material.SetColor(ID_RimColor, rimHDR);
                e.material.SetFloat(ID_RimAttenuation, effectiveRimAtten);
                e.material.SetFloat(ID_MinRim, rimMin);
                e.material.SetFloat(ID_MaxRim, rimMax);
            }
        }
    }
}
