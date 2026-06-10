using System.Collections.Generic;
using InGame.Team;
using UnityEngine;

/// <summary>
/// 무적 모드 Hue Shift 이펙트 컨트롤러.
/// TeamCharacter 루트 오브젝트에 부착.
///
/// InvincibleModeController의 OnInvincibleEnter/Exit 이벤트에 연동하여
/// All In One 3D Shader의 Hue Shift, Emission, Rim Lighting을 제어한다.
///
/// Material 인스턴스는 Awake()에서 미리 생성하여 런타임 랙을 방지.
/// </summary>
public class StarPowerUpEffect : MonoBehaviour
{
    // ====================================
    //  Color Settings
    // ====================================
    [Header("Color Settings")]

    [Tooltip("Enable rainbow hue cycling on base texture. If OFF, original colors are kept and only glow is added")]
    public bool enableHueShift = true;

    [Tooltip("Hue cycle speed (degrees/sec). Higher = faster rainbow")]
    [Range(30f, 600f)]
    public float hueSpeed = 200f;

    [Tooltip("Saturation (0: gray, 4: vivid)")]
    [Range(0f, 4f)]
    public float hueSaturation = 1.5f;

    [Tooltip("Rim light hue offset from body color")]
    [Range(0f, 360f)]
    public float rimHueOffset = 90f;

    [Tooltip("Emission hue offset from body color")]
    [Range(0f, 360f)]
    public float emissionHueOffset = 0f;

    // ====================================
    //  Brightness Settings
    // ====================================
    [Header("Brightness Settings")]

    [Tooltip("Hue Shift brightness (0: dark, 2: very bright)")]
    [Range(0f, 2f)]
    public float hueBrightness = 1.5f;

    [Tooltip("Emission self glow intensity (0~20)")]
    [Range(0f, 20f)]
    public float emissionSelfGlow = 5f;

    [Tooltip("Emission color HDR intensity multiplier")]
    [Range(0f, 5f)]
    public float emissionIntensity = 2f;

    [Tooltip("Rim light attenuation (0: none, 1: max)")]
    [Range(0f, 1f)]
    public float rimAttenuation = 0.8f;

    [Tooltip("Rim min range")]
    [Range(0f, 1f)]
    public float rimMin = 0f;

    [Tooltip("Rim max range")]
    [Range(0f, 1f)]
    public float rimMax = 1f;

    [Tooltip("Rim light HDR intensity multiplier")]
    [Range(0f, 5f)]
    public float rimIntensity = 2f;

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

    // ====================================
    //  Fade Settings
    // ====================================
    [Header("Fade Settings")]

    [Tooltip("Fade in time (seconds)")]
    [Range(0f, 3f)]
    public float fadeInDuration = 0.5f;

    [Tooltip("Fade out time (seconds)")]
    [Range(0f, 3f)]
    public float fadeOutDuration = 1.0f;

    // ====================================
    //  Warning Blink Settings
    // ====================================
    [Header("Warning Blink")]

    [Tooltip("Warning blink start time before end (remaining seconds)")]
    [Range(0f, 5f)]
    public float warningBlinkTime = 3f;

    [Tooltip("Warning blink speed (Hz)")]
    [Range(2f, 15f)]
    public float warningBlinkSpeed = 6f;

    // ====================================
    //  Extra Settings
    // ====================================
    [Header("Extra Settings")]

    [Tooltip("Enable debug log output")]
    public bool debugLog = false;

    // ====================================
    //  Read-only State
    // ====================================
    [Header("State (Read-only)")]

    [SerializeField, Tooltip("Is effect currently active")]
    private bool _isActive = false;
    public bool IsActive => _isActive;

    // ====================================
    //  Internal Variables
    // ====================================

    // Shader property IDs (cached)
    private static readonly int ID_HueShift = Shader.PropertyToID("_HueShift");
    private static readonly int ID_HueSaturation = Shader.PropertyToID("_HueSaturation");
    private static readonly int ID_HueBrightness = Shader.PropertyToID("_HueBrightness");
    private static readonly int ID_EmissionSelfGlow = Shader.PropertyToID("_EmissionSelfGlow");
    private static readonly int ID_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");
    private static readonly int ID_RimAttenuation = Shader.PropertyToID("_RimAttenuation");
    private static readonly int ID_MinRim = Shader.PropertyToID("_MinRim");
    private static readonly int ID_MaxRim = Shader.PropertyToID("_MaxRim");

    // Shader keywords
    private const string KW_HUE_SHIFT = "_HUE_SHIFT_ON";
    private const string KW_EMISSION = "_EMISSION_ON";
    private const string KW_RIM_LIGHTING = "_RIM_LIGHTING_ON";

    // Per-renderer data: original + pre-created instance materials
    private struct RendererData
    {
        public Renderer renderer;
        public Material[] originalSharedMaterials;
        public Material[] effectMaterials;
    }

    private InvincibleModeController _controller;
    private List<RendererData> _rendererDataList = new List<RendererData>();
    private List<Material> _instancedMaterials = new List<Material>();
    private float _currentHue = 0f;
    private float _fadeProgress = 0f;
    private bool _pendingRestore = false;
    private bool _prepared = false;

    private enum EffectPhase { Inactive, FadingIn, Active, FadingOut }
    private EffectPhase _phase = EffectPhase.Inactive;

    // ====================================
    //  Unity Lifecycle
    // ====================================

    private void Awake()
    {
        PrepareInstances();
    }

    private void Start()
    {
        _controller = GetComponent<InvincibleModeController>();
        if (_controller == null)
            _controller = GetComponentInParent<InvincibleModeController>();

        if (_controller == null)
        {
            if (debugLog) Debug.LogWarning("[StarPowerUp] InvincibleModeController not found.");
            return;
        }

        _controller.OnInvincibleEnter += HandleInvincibleEnter;
        _controller.OnInvincibleExit += HandleInvincibleExit;
    }

    private void Update()
    {
        if (_phase == EffectPhase.Inactive) return;

        if (_pendingRestore)
        {
            _pendingRestore = false;
            SwapToOriginals();
            ResetState();
            return;
        }

        UpdatePhase();

        // Hue cycling
        _currentHue = (_currentHue + hueSpeed * Time.deltaTime) % 360f;

        // Brightness pulse
        float pulseMult = 1f;
        if (enableBrightnessPulse)
        {
            pulseMult = Mathf.Lerp(brightnessPulseMin, 1f,
                (Mathf.Sin(Time.time * brightnessPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
        }

        // Warning blink (controller의 RemainingTime 참조)
        float warningMult = 1f;
        if (_phase == EffectPhase.Active && _controller != null && warningBlinkTime > 0f)
        {
            float remaining = _controller.RemainingTime;
            if (remaining > 0f && remaining <= warningBlinkTime)
            {
                warningMult = (Mathf.Sin(Time.time * warningBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                warningMult = Mathf.Lerp(0.3f, 1f, warningMult);
            }
        }

        float intensity = _fadeProgress * pulseMult * warningMult;
        ApplyEffectToMaterials(intensity, _fadeProgress);
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.OnInvincibleEnter -= HandleInvincibleEnter;
            _controller.OnInvincibleExit -= HandleInvincibleExit;
        }

        if (_isActive || _phase != EffectPhase.Inactive)
            SwapToOriginals();

        foreach (Material mat in _instancedMaterials)
        {
            if (mat != null) Destroy(mat);
        }
        _instancedMaterials.Clear();
        _rendererDataList.Clear();
    }

    // ====================================
    //  Event Handlers
    // ====================================

    private void HandleInvincibleEnter()
    {
        ActivateEffect();
    }

    private void HandleInvincibleExit()
    {
        DeactivateEffect();
    }

    // ====================================
    //  Internal: Activation / Deactivation
    // ====================================

    private void ActivateEffect()
    {
        if (_isActive) return;

        if (debugLog) Debug.Log("[StarPowerUp] Activating star effect.");

        if (!_prepared) PrepareInstances();

        SwapToEffectMaterials();

        _isActive = true;
        _currentHue = 0f;
        _fadeProgress = 0f;
        _phase = EffectPhase.FadingIn;
    }

    private void DeactivateEffect()
    {
        if (!_isActive) return;

        if (debugLog) Debug.Log("[StarPowerUp] Deactivating star effect.");

        if (fadeOutDuration <= 0f)
        {
            SwapToOriginals();
            ResetState();
        }
        else
        {
            _phase = EffectPhase.FadingOut;
        }
    }

    // ====================================
    //  Internal: Material Management
    // ====================================

    private void PrepareInstances()
    {
        _rendererDataList.Clear();
        _instancedMaterials.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        Dictionary<Renderer, List<int>> rendererToIndices = new Dictionary<Renderer, List<int>>();
        foreach (Renderer rend in renderers)
        {
            Material[] sharedMats = rend.sharedMaterials;
            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material mat = sharedMats[i];
                if (mat == null) continue;
                if (!mat.shader.name.Contains("AllIn13DShader")) continue;

                if (!rendererToIndices.ContainsKey(rend))
                    rendererToIndices[rend] = new List<int>();
                rendererToIndices[rend].Add(i);
            }
        }

        foreach (var kvp in rendererToIndices)
        {
            Renderer rend = kvp.Key;
            List<int> indices = kvp.Value;

            Material[] origShared = rend.sharedMaterials;
            Material[] origCopy = new Material[origShared.Length];
            System.Array.Copy(origShared, origCopy, origShared.Length);

            Material[] effectMats = new Material[origShared.Length];
            System.Array.Copy(origShared, effectMats, origShared.Length);

            foreach (int idx in indices)
            {
                Material instanceMat = new Material(origShared[idx]);
                instanceMat.name = origShared[idx].name + " (StarEffect)";

                if (enableHueShift)
                {
                    instanceMat.EnableKeyword(KW_HUE_SHIFT);
                    instanceMat.SetFloat("_HueShiftEnabled", 1f);
                }
                instanceMat.EnableKeyword(KW_EMISSION);
                instanceMat.SetFloat("_EmissionEnabled", 1f);
                instanceMat.EnableKeyword(KW_RIM_LIGHTING);
                instanceMat.SetFloat("_RimLighting", 1f);

                effectMats[idx] = instanceMat;
                _instancedMaterials.Add(instanceMat);
            }

            _rendererDataList.Add(new RendererData
            {
                renderer = rend,
                originalSharedMaterials = origCopy,
                effectMaterials = effectMats
            });
        }

        _prepared = true;
        if (debugLog) Debug.Log($"[StarPowerUp] Pre-created {_instancedMaterials.Count} material instances across {_rendererDataList.Count} renderers.");
    }

    private void SwapToEffectMaterials()
    {
        foreach (RendererData data in _rendererDataList)
        {
            if (data.renderer == null) continue;
            data.renderer.sharedMaterials = data.effectMaterials;
        }
    }

    private void SwapToOriginals()
    {
        foreach (RendererData data in _rendererDataList)
        {
            if (data.renderer == null) continue;
            data.renderer.sharedMaterials = data.originalSharedMaterials;
        }

        if (debugLog) Debug.Log("[StarPowerUp] Restored original materials.");
    }

    // ====================================
    //  Internal: Phase & Effect
    // ====================================

    private void UpdatePhase()
    {
        switch (_phase)
        {
            case EffectPhase.FadingIn:
                if (fadeInDuration > 0f)
                    _fadeProgress = Mathf.MoveTowards(_fadeProgress, 1f, Time.deltaTime / fadeInDuration);
                else
                    _fadeProgress = 1f;

                if (_fadeProgress >= 1f)
                    _phase = EffectPhase.Active;
                break;

            case EffectPhase.Active:
                // 컨트롤러의 OnInvincibleExit 이벤트를 기다림
                break;

            case EffectPhase.FadingOut:
                if (fadeOutDuration > 0f)
                    _fadeProgress = Mathf.MoveTowards(_fadeProgress, 0f, Time.deltaTime / fadeOutDuration);
                else
                    _fadeProgress = 0f;

                if (_fadeProgress <= 0f)
                    _pendingRestore = true;
                break;
        }
    }

    private void ApplyEffectToMaterials(float intensity, float fadeProgress)
    {
        float effectiveHueBrightness = Mathf.Lerp(1f, hueBrightness, intensity);
        float effectiveSaturation = Mathf.Lerp(1f, hueSaturation, intensity);
        float effectiveEmissionGlow = emissionSelfGlow * intensity;
        float effectiveRimAtten = rimAttenuation * intensity;

        float effectiveHue = _currentHue * fadeProgress;

        float emSat = 0.8f * fadeProgress;
        Color emissionHDR = Color.HSVToRGB(
            Mathf.Repeat((_currentHue + emissionHueOffset) / 360f, 1f), emSat, 1f
        ) * emissionIntensity * intensity;
        emissionHDR.a = 1f;

        float rimSat = 0.9f * fadeProgress;
        Color rimHDR = Color.HSVToRGB(
            Mathf.Repeat((_currentHue + rimHueOffset) / 360f, 1f), rimSat, 1f
        ) * rimIntensity * intensity;
        rimHDR.a = 1f;

        foreach (Material mat in _instancedMaterials)
        {
            if (mat == null) continue;

            if (enableHueShift)
            {
                mat.SetFloat(ID_HueShift, effectiveHue);
                mat.SetFloat(ID_HueSaturation, effectiveSaturation);
                mat.SetFloat(ID_HueBrightness, effectiveHueBrightness);
            }

            mat.SetFloat(ID_EmissionSelfGlow, effectiveEmissionGlow);
            mat.SetColor(ID_EmissionColor, emissionHDR);

            mat.SetColor(ID_RimColor, rimHDR);
            mat.SetFloat(ID_RimAttenuation, effectiveRimAtten);
            mat.SetFloat(ID_MinRim, rimMin);
            mat.SetFloat(ID_MaxRim, rimMax);
        }
    }

    private void ResetState()
    {
        _isActive = false;
        _fadeProgress = 0f;
        _pendingRestore = false;
        _phase = EffectPhase.Inactive;
    }
}
