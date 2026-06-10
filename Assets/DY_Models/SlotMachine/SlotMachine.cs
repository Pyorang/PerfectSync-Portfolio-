using UnityEngine;
using System;
using System.Collections;
using InGame.Audio;

/// <summary>
/// 슬롯머신 컨트롤러
///
/// 사용법:
/// 1. Jackpot 오브젝트(최상위)에 이 스크립트를 부착
/// 2. Inspector에서 lever, reel1, reel2, reel3에 각각 오브젝트를 할당
///    - lever: Cylinder.007 (손잡이)
///    - reel1: Cylinder.005 (왼쪽 릴)
///    - reel2: Cylinder.008 (가운데 릴)
///    - reel3: Cylinder.004 (오른쪽 릴)
/// 3. 외부에서 Spin(results) 또는 StartSpin() + StopOnSymbols(results) 호출
///
/// 2단계 스핀:
///   slotMachine.StartSpin();              // 릴 회전 시작 (결과 미정)
///   slotMachine.StopOnSymbols(results);   // 결과 확정 후 감속 정지
///
/// 1단계 스핀 (하위 호환):
///   slotMachine.Spin(new int[] { 2, 0, 4 });
///
/// 심볼 인덱스:
///   0 = 체리, 1 = 별, 2 = 하트, 3 = 다이아몬드, 4 = 벨, 5 = 세븐
/// </summary>
public class SlotMachine : MonoBehaviour
{
    [Header("=== 오브젝트 연결 ===")]
    [Tooltip("손잡이 오브젝트 (Cylinder.007)")]
    public Transform lever;

    [Tooltip("왼쪽 릴 (Cylinder.005)")]
    public Transform reel1;

    [Tooltip("가운데 릴 (Cylinder.008)")]
    public Transform reel2;

    [Tooltip("오른쪽 릴 (Cylinder.004)")]
    public Transform reel3;

    [Header("=== 손잡이 설정 ===")]
    [Tooltip("손잡이 회전 각도 (내려가는 정도)")]
    public float leverPullAngle = 45f;

    [Tooltip("손잡이 내려가는 시간 (초)")]
    public float leverPullDuration = 0.3f;

    [Tooltip("손잡이 올라오는 시간 (초)")]
    public float leverReturnDuration = 0.5f;

    [Tooltip("손잡이 회전 축 (로컬 좌표)")]
    public Vector3 leverAxis = Vector3.right;

    [Header("=== 릴 회전 설정 ===")]
    [Tooltip("릴 회전 축 (로컬 좌표)")]
    public Vector3 reelSpinAxis = Vector3.right;

    [Tooltip("릴 최고 회전 속도 (도/초)")]
    public float maxSpinSpeed = 1080f;

    [Tooltip("가속 시간 (초)")]
    public float spinUpDuration = 0.5f;

    [Tooltip("최고 속도 유지 시간 (초)")]
    public float spinHoldDuration = 1.0f;

    [Tooltip("감속 시간 (초)")]
    public float spinDownDuration = 1.5f;

    [Tooltip("릴 사이 정지 딜레이 (초)")]
    public float reelStopDelay = 0.4f;

    [Header("=== 심볼 설정 ===")]
    [Tooltip("총 심볼 개수")]
    public int symbolCount = 6;

    [Header("=== 결과 VFX (프리팹 하위 ParticleSystem) ===")]
    [Tooltip("잭팟 시 재생 (playOnAwake=off, looping=off)")]
    [SerializeField] private ParticleSystem _jackpotVfx;

    [Tooltip("꽝 시 재생 (playOnAwake=off, looping=off)")]
    [SerializeField] private ParticleSystem _missVfx;

    [Header("=== SFX (3D Spatial) ===")]
    [SerializeField] private SpatialSfxProfile _spinProfile;
    [SerializeField] private SpatialSfxProfile _matchProfile;
    [SerializeField] private SpatialSfxProfile _missProfile;
    [SerializeField] private SpatialSfxProfile _leverPullProfile;
    [SerializeField] private SpatialSfxProfile _reelStopProfile;

    // 상태
    private enum ESpinPhase { Idle, Accelerating, Holding, Decelerating }
    private ESpinPhase _spinPhase = ESpinPhase.Idle;

    private Quaternion leverStartRotation;
    private Quaternion[] reelStartRotations;

    // 릴별 현재 각도 (Holding 중 공유)
    private float[] _reelAngles;
    private Coroutine _spinCoroutine;
    private int[] _pendingResults;

    // 스핀 루프 SFX handle (InGameSfxManager.EmitSpatialOn 반환값, 마지막 릴 정지 시 StopSpatial)
    private int _spinLoopHandle;

    // 이벤트: 스핀 완료 시 호출
    public event Action<int[]> OnSpinComplete;

    public bool IsSpinning => _spinPhase != ESpinPhase.Idle;

    void Start()
    {
        if (lever != null)
            leverStartRotation = lever.localRotation;

        reelStartRotations = new Quaternion[3];
        _reelAngles = new float[3];
        Transform[] reels = { reel1, reel2, reel3 };
        for (int i = 0; i < 3; i++)
        {
            if (reels[i] != null)
                reelStartRotations[i] = reels[i].localRotation;
        }
    }

    /// <summary>
    /// 1단계 스핀 (하위 호환): 결과를 미리 알고 있을 때 사용.
    /// </summary>
    public void Spin(int[] results)
    {
        if (_spinPhase != ESpinPhase.Idle)
        {
            Debug.LogWarning("SlotMachine: 이미 회전 중입니다.");
            return;
        }

        if (results == null || results.Length != 3)
        {
            Debug.LogError("SlotMachine: results는 길이 3의 배열이어야 합니다.");
            return;
        }

        _spinCoroutine = StartCoroutine(LegacySpinSequence(results));
    }

    /// <summary>
    /// 2단계 스핀 Phase 1: 레버 당기기 + 릴 가속 + 최고속도 무한 유지.
    /// StopOnSymbols()를 호출할 때까지 회전 지속.
    /// </summary>
    public void StartSpin()
    {
        if (_spinPhase != ESpinPhase.Idle)
        {
            Debug.LogWarning("SlotMachine: 이미 회전 중입니다.");
            return;
        }

        _pendingResults = null;
        _spinCoroutine = StartCoroutine(TwoPhaseSpinSequence());
    }

    /// <summary>
    /// 2단계 스핀 Phase 2: 결과 심볼을 지정하고 감속 정지.
    /// StartSpin() 이후, Holding 상태에서 호출해야 함.
    /// </summary>
    public void StopOnSymbols(int[] results)
    {
        if (results == null || results.Length != 3)
        {
            Debug.LogError("SlotMachine: results는 길이 3의 배열이어야 합니다.");
            return;
        }

        if (_spinPhase == ESpinPhase.Holding)
        {
            _pendingResults = results;
        }
        else if (_spinPhase == ESpinPhase.Accelerating)
        {
            // 아직 가속 중이면 큐잉 — Holding 진입 시 자동 처리.
            _pendingResults = results;
        }
        else
        {
            Debug.LogWarning("SlotMachine: StopOnSymbols는 스핀 중에만 호출 가능합니다.");
        }
    }

    // ── 2단계 스핀 시퀀스 ─────────────────────────────────────

    private IEnumerator TwoPhaseSpinSequence()
    {
        _spinPhase = ESpinPhase.Accelerating;
        Transform[] reels = { reel1, reel2, reel3 };

        InGameSfxManager.Instance?.EmitSpatialOn(_leverPullProfile, lever, this);
        StartSpinLoop();

        // 1. 레버 당기기
        yield return StartCoroutine(AnimateLever(0f, leverPullAngle, leverPullDuration));

        // 2. 릴 가속
        float elapsed = 0f;
        while (elapsed < spinUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spinUpDuration;
            float speed = Mathf.Lerp(0f, maxSpinSpeed, t * t);

            for (int i = 0; i < 3; i++)
            {
                if (reels[i] == null) continue;
                _reelAngles[i] += speed * Time.deltaTime;
                reels[i].localRotation = reelStartRotations[i] * Quaternion.AngleAxis(_reelAngles[i], reelSpinAxis);
            }
            yield return null;
        }

        // 3. 레버 복귀 (비동기 — 유지 루프와 병행)
        StartCoroutine(AnimateLever(leverPullAngle, 0f, leverReturnDuration));

        // 4. Holding: 최고속도 무한 유지. _pendingResults가 설정되면 탈출.
        _spinPhase = ESpinPhase.Holding;
        while (_pendingResults == null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (reels[i] == null) continue;
                _reelAngles[i] += maxSpinSpeed * Time.deltaTime;
                reels[i].localRotation = reelStartRotations[i] * Quaternion.AngleAxis(_reelAngles[i], reelSpinAxis);
            }
            yield return null;
        }

        // 5. 감속 → 목표 심볼 정지
        _spinPhase = ESpinPhase.Decelerating;
        yield return StartCoroutine(DecelerateReels(reels, _pendingResults));

        _spinPhase = ESpinPhase.Idle;
        int[] finalResults = _pendingResults;
        _pendingResults = null;
        OnSpinComplete?.Invoke(finalResults);
        Debug.Log($"SlotMachine: 스핀 완료! 결과 = [{finalResults[0]}, {finalResults[1]}, {finalResults[2]}]");
    }

    // ── 1단계 스핀 (하위 호환) ─────────────────────────────────

    private IEnumerator LegacySpinSequence(int[] results)
    {
        _spinPhase = ESpinPhase.Accelerating;

        Transform[] reels = { reel1, reel2, reel3 };

        InGameSfxManager.Instance?.EmitSpatialOn(_leverPullProfile, lever, this);
        StartSpinLoop();

        // 1. 레버 당기기
        yield return StartCoroutine(AnimateLever(0f, leverPullAngle, leverPullDuration));

        // 2. 릴 회전 시작
        Coroutine[] reelCoroutines = new Coroutine[3];

        for (int i = 0; i < 3; i++)
        {
            if (reels[i] != null)
            {
                float extraDelay = i * reelStopDelay;
                reelCoroutines[i] = StartCoroutine(
                    SpinReel(reels[i], i, results[i], extraDelay)
                );
            }
        }

        // 3. 레버 복귀
        yield return StartCoroutine(AnimateLever(leverPullAngle, 0f, leverReturnDuration));

        // 4. 모든 릴 정지 대기
        for (int i = 0; i < 3; i++)
        {
            if (reelCoroutines[i] != null)
                yield return reelCoroutines[i];
        }

        _spinPhase = ESpinPhase.Idle;
        OnSpinComplete?.Invoke(results);
        Debug.Log($"SlotMachine: 스핀 완료! 결과 = [{results[0]}, {results[1]}, {results[2]}]");
    }

    // ── 감속 시퀀스 (2단계 스핀용) ─────────────────────────────

    private IEnumerator DecelerateReels(Transform[] reels, int[] results)
    {
        Coroutine[] stopCoroutines = new Coroutine[3];

        for (int i = 0; i < 3; i++)
        {
            if (reels[i] != null)
            {
                float extraDelay = i * reelStopDelay;
                stopCoroutines[i] = StartCoroutine(
                    DecelerateReel(reels[i], i, results[i], extraDelay)
                );
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (stopCoroutines[i] != null)
                yield return stopCoroutines[i];
        }
    }

    private IEnumerator DecelerateReel(Transform reel, int reelIndex, int targetSymbol, float extraDelay)
    {
        if (extraDelay > 0f)
        {
            // 딜레이 중에도 회전 유지
            float delayElapsed = 0f;
            while (delayElapsed < extraDelay)
            {
                delayElapsed += Time.deltaTime;
                _reelAngles[reelIndex] += maxSpinSpeed * Time.deltaTime;
                reel.localRotation = reelStartRotations[reelIndex]
                    * Quaternion.AngleAxis(_reelAngles[reelIndex], reelSpinAxis);
                yield return null;
            }
        }

        float currentAngle = _reelAngles[reelIndex];
        float symbolAngle = 360f / symbolCount;
        float targetAngle = targetSymbol * symbolAngle;

        float normalizedCurrent = currentAngle % 360f;
        if (normalizedCurrent < 0) normalizedCurrent += 360f;

        float remaining = targetAngle - normalizedCurrent;
        if (remaining <= 0) remaining += 360f;
        remaining += 360f; // 최소 1바퀴 추가

        float targetTotalAngle = currentAngle + remaining;
        float decelStart = currentAngle;
        float decelDistance = targetTotalAngle - decelStart;
        float elapsed = 0f;

        while (elapsed < spinDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spinDownDuration;
            float easedT = 1f - (1f - t) * (1f - t);
            _reelAngles[reelIndex] = decelStart + decelDistance * easedT;
            reel.localRotation = reelStartRotations[reelIndex]
                * Quaternion.AngleAxis(_reelAngles[reelIndex], reelSpinAxis);
            yield return null;
        }

        reel.localRotation = reelStartRotations[reelIndex]
            * Quaternion.AngleAxis(targetTotalAngle, reelSpinAxis);
        _reelAngles[reelIndex] = targetTotalAngle;

        // 첫 번째로 정지하는 릴(index 0)에 맞춰 스핀 루프 중단.
        if (reelIndex == 0)
            StopSpinLoop();

        InGameSfxManager.Instance?.EmitSpatialOn(_reelStopProfile, reel, this);
    }

    // ── 개별 릴 회전 (레거시용) ─────────────────────────────────

    private IEnumerator SpinReel(Transform reel, int reelIndex, int targetSymbol, float extraDelay)
    {
        float currentAngle = 0f;
        float currentSpeed = 0f;

        // 가속
        float elapsed = 0f;
        while (elapsed < spinUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spinUpDuration;
            currentSpeed = Mathf.Lerp(0f, maxSpinSpeed, t * t);
            currentAngle += currentSpeed * Time.deltaTime;
            reel.localRotation = reelStartRotations[reelIndex] * Quaternion.AngleAxis(currentAngle, reelSpinAxis);
            yield return null;
        }

        // 최고 속도 유지
        currentSpeed = maxSpinSpeed;
        elapsed = 0f;
        float holdTime = spinHoldDuration + extraDelay;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;
            currentAngle += currentSpeed * Time.deltaTime;
            reel.localRotation = reelStartRotations[reelIndex] * Quaternion.AngleAxis(currentAngle, reelSpinAxis);
            yield return null;
        }

        // 감속 → 목표 심볼 정지
        float symbolAngle = 360f / symbolCount;
        float targetAngle = targetSymbol * symbolAngle;

        float normalizedCurrent = currentAngle % 360f;
        if (normalizedCurrent < 0) normalizedCurrent += 360f;

        float remaining = targetAngle - normalizedCurrent;
        if (remaining <= 0) remaining += 360f;
        remaining += 360f;

        float targetTotalAngle = currentAngle + remaining;
        float decelStart = currentAngle;
        float decelDistance = targetTotalAngle - decelStart;
        elapsed = 0f;

        while (elapsed < spinDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spinDownDuration;
            float easedT = 1f - (1f - t) * (1f - t);
            currentAngle = decelStart + decelDistance * easedT;
            reel.localRotation = reelStartRotations[reelIndex] * Quaternion.AngleAxis(currentAngle, reelSpinAxis);
            yield return null;
        }

        reel.localRotation = reelStartRotations[reelIndex] * Quaternion.AngleAxis(targetTotalAngle, reelSpinAxis);

        // 첫 번째로 정지하는 릴(index 0)에 맞춰 스핀 루프 중단.
        if (reelIndex == 0)
            StopSpinLoop();

        InGameSfxManager.Instance?.EmitSpatialOn(_reelStopProfile, reel, this);
    }

    // ── 공통 유틸 ──────────────────────────────────────────────

    private IEnumerator AnimateLever(float fromAngle, float toAngle, float duration)
    {
        if (lever == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float angle = Mathf.Lerp(fromAngle, toAngle, t);
            lever.localRotation = leverStartRotation * Quaternion.AngleAxis(angle, leverAxis);
            yield return null;
        }

        lever.localRotation = leverStartRotation * Quaternion.AngleAxis(toAngle, leverAxis);
    }

    // ── 스핀 루프 SFX handle 관리 ─────────────────────────────────

    private void StartSpinLoop()
    {
        var sfx = InGameSfxManager.Instance;
        if (sfx == null) return;

        _spinLoopHandle = sfx.EmitSpatialOn(_spinProfile, transform, this);
    }

    private void StopSpinLoop()
    {
        if (_spinLoopHandle == 0) return;

        InGameSfxManager.Instance?.StopSpatial(_spinLoopHandle);
        _spinLoopHandle = 0;
    }

    public void PlayResultEffects(bool isMatch)
    {
        ParticleSystem vfx = isMatch ? _jackpotVfx : _missVfx;
        if (vfx != null)
        {
            vfx.Clear(true);
            vfx.Play(true);
        }

        InGameSfxManager.Instance?.EmitSpatialOn(isMatch ? _matchProfile : _missProfile, transform, this);
    }

    public void ResetState()
    {
        StopAllCoroutines();
        _spinPhase = ESpinPhase.Idle;
        _pendingResults = null;

        StopSpinLoop();

        if (lever != null)
            lever.localRotation = leverStartRotation;

        Transform[] reels = { reel1, reel2, reel3 };
        for (int i = 0; i < 3; i++)
        {
            if (reels[i] != null)
                reels[i].localRotation = reelStartRotations[i];
            _reelAngles[i] = 0f;
        }
    }
}
