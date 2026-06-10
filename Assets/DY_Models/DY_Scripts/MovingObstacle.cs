using UnityEngine;

/// <summary>
/// Waypoint 기반 장애물 이동. 좌우/앞뒤 복합 이동을 지원하며,
/// 오프셋 기반으로 오브젝트 회전과 무관하게 올바르게 동작한다.
/// </summary>
public class MovingObstacle : MonoBehaviour
{
    #region Enums

    public enum MovementMode { Simultaneous, Alternating }
    public enum EaseType { Linear, EaseInOut }
    public enum LoopMode { PingPong, Loop }

    private enum Phase { Horizontal, Vertical }

    #endregion

    #region Inspector

    [Header("이동 모드")]
    [Tooltip("Simultaneous: 좌우+앞뒤 동시 이동 / Alternating: 좌우↔앞뒤 교대 이동")]
    [SerializeField] private MovementMode movementMode = MovementMode.Simultaneous;

    [Header("X축 (좌우) 이동")]
    [SerializeField] private Transform waypointLeft;
    [SerializeField] private Transform waypointRight;
    [SerializeField][Min(0.01f)] private float horizontalSpeed = 3f;
    [SerializeField] private EaseType horizontalEase = EaseType.Linear;
    [SerializeField][Min(0f)] private float horizontalPauseTime = 0f;
    [SerializeField] private LoopMode horizontalLoopMode = LoopMode.PingPong;

    [Header("Z축 (앞뒤) 이동")]
    [SerializeField] private Transform waypointForward;
    [SerializeField] private Transform waypointBack;
    [SerializeField][Min(0.01f)] private float verticalSpeed = 2f;
    [SerializeField] private EaseType verticalEase = EaseType.EaseInOut;
    [SerializeField][Min(0f)] private float verticalPauseTime = 0f;
    [SerializeField] private LoopMode verticalLoopMode = LoopMode.PingPong;

    [Header("시작 설정")]
    [Tooltip("게임 시작 후 이동 시작까지 대기 시간 (초)")]
    [SerializeField][Min(0f)] private float startDelay = 0f;
    [Tooltip("사이클 시작 위치 (0=시작점, 1=끝점)")]
    [SerializeField][Range(0f, 1f)] private float startOffset = 0f;

    [Header("디버그")]
    [SerializeField] private bool drawDebugLines = true;

    #endregion

    #region Internal State

    private Rigidbody _rigidbody;
    private bool isActive;

    private Vector3 initialPosition;
    private bool horizontalEnabled;
    private bool verticalEnabled;

    // 축별 이동 상태
    private AxisState hAxis;
    private AxisState vAxis;

    // Alternating 모드 전용
    private Phase currentPhase = Phase.Horizontal;
    private float altElapsed;
    private Vector3 cachedHOffset;
    private Vector3 cachedVOffset;

    private const float MIN_DISTANCE = 0.001f;

    /// <summary>
    /// 한 축의 이동에 필요한 모든 런타임 상태를 묶은 구조체.
    /// </summary>
    private struct AxisState
    {
        public Vector3 fromOffset;   // 시작점 오프셋 (좌 또는 후방)
        public Vector3 toOffset;     // 끝점 오프셋 (우 또는 전방)
        public float cycleDuration;  // 한 방향 이동에 걸리는 시간
        public float elapsed;        // 현재 경과 시간
        public bool forward;         // 현재 이동 방향 (true: from→to)
        public bool isPausing;       // 끝점 도달 후 대기 중 여부
        public float pauseTimer;     // 대기 경과 시간
    }

    #endregion

    #region Lifecycle

    private void Start()
    {
        TryGetComponent(out _rigidbody);
        if (_rigidbody) _rigidbody.isKinematic = true;

        initialPosition = transform.position;
        InitializeAxes();

        if (startDelay > 0f)
            Invoke(nameof(Activate), startDelay);
        else
            isActive = true;
    }

    private void FixedUpdate()
    {
        if (!isActive) return;

        if (movementMode == MovementMode.Simultaneous)
            UpdateSimultaneous();
        else
            UpdateAlternating();
    }

    private void Activate() => isActive = true;

    #endregion

    #region Initialization

    private void InitializeAxes()
    {
        // Waypoint 월드 좌표 → 초기 위치 기준 오프셋
        hAxis.fromOffset = (waypointLeft    ? waypointLeft.position    : initialPosition) - initialPosition;
        hAxis.toOffset   = (waypointRight   ? waypointRight.position   : initialPosition) - initialPosition;
        vAxis.fromOffset = (waypointBack    ? waypointBack.position    : initialPosition) - initialPosition;
        vAxis.toOffset   = (waypointForward ? waypointForward.position : initialPosition) - initialPosition;

        float hDist = Vector3.Distance(hAxis.fromOffset, hAxis.toOffset);
        float vDist = Vector3.Distance(vAxis.fromOffset, vAxis.toOffset);

        horizontalEnabled = hDist > MIN_DISTANCE;
        verticalEnabled   = vDist > MIN_DISTANCE;

        hAxis.cycleDuration = horizontalEnabled ? hDist / horizontalSpeed : 1f;
        vAxis.cycleDuration = verticalEnabled   ? vDist / verticalSpeed   : 1f;

        hAxis.forward = true;
        vAxis.forward = true;
        hAxis.elapsed = startOffset * hAxis.cycleDuration;
        vAxis.elapsed = startOffset * vAxis.cycleDuration;
        altElapsed    = startOffset * hAxis.cycleDuration;

        if (!horizontalEnabled && !verticalEnabled)
            Debug.LogWarning($"[MovingObstacle] '{name}': Waypoint 미할당 또는 거리 0. 이동 불가.", this);
    }

    #endregion

    #region Simultaneous Mode

    private void UpdateSimultaneous()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 hOffset = Vector3.zero;
        Vector3 vOffset = Vector3.zero;

        if (horizontalEnabled)
        {
            float t = ProcessAxis(ref hAxis, horizontalPauseTime, horizontalLoopMode, dt);
            hOffset = Vector3.Lerp(hAxis.fromOffset, hAxis.toOffset, ApplyEase(t, horizontalEase));
        }

        if (verticalEnabled)
        {
            float t = ProcessAxis(ref vAxis, verticalPauseTime, verticalLoopMode, dt);
            vOffset = Vector3.Lerp(vAxis.fromOffset, vAxis.toOffset, ApplyEase(t, verticalEase));
        }

        ApplyPosition(hOffset + vOffset);
    }

    /// <summary>
    /// 축 이동/일시정지/방향전환을 처리하고 현재 보간값 t(0~1)를 반환한다.
    /// </summary>
    private float ProcessAxis(ref AxisState axis, float pauseTime, LoopMode loopMode, float dt)
    {
        // --- 일시정지 처리 ---
        if (axis.isPausing)
        {
            axis.pauseTimer += dt;
            if (axis.pauseTimer >= pauseTime)
            {
                // 정지 종료 → 방향 전환 후 아래 이동 코드로 진행
                axis.isPausing  = false;
                axis.pauseTimer = 0f;
                if (loopMode == LoopMode.PingPong) axis.forward = !axis.forward;
                axis.elapsed = 0f;
            }
            else
            {
                // 아직 정지 중 → 끝점에 머무름
                return axis.forward ? 1f : 0f;
            }
        }

        // --- 이동 처리 ---
        axis.elapsed += dt;

        if (axis.elapsed >= axis.cycleDuration)
        {
            axis.elapsed = axis.cycleDuration;

            if (pauseTime > 0f)
            {
                // 끝점 도달 → 정지 시작
                axis.isPausing  = true;
                axis.pauseTimer = 0f;
                return axis.forward ? 1f : 0f;
            }

            // 정지 없이 즉시 방향 전환
            if (loopMode == LoopMode.PingPong) axis.forward = !axis.forward;
            axis.elapsed = 0f;
        }

        float t = Mathf.Clamp01(axis.elapsed / axis.cycleDuration);
        return axis.forward ? t : 1f - t;
    }

    #endregion

    #region Alternating Mode

    private void UpdateAlternating()
    {
        float dt = Time.fixedDeltaTime;
        bool isH = (currentPhase == Phase.Horizontal);

        // 비활성 축이면 반대쪽으로 전환
        if (isH && !horizontalEnabled)
        {
            if (verticalEnabled) { currentPhase = Phase.Vertical; isH = false; }
            else return;
        }
        else if (!isH && !verticalEnabled)
        {
            if (horizontalEnabled) { currentPhase = Phase.Horizontal; isH = true; }
            else return;
        }

        // 현재 활성 축의 설정 참조
        AxisState axis = isH ? hAxis : vAxis;
        EaseType ease  = isH ? horizontalEase : verticalEase;

        altElapsed += dt;
        bool reachedEnd = altElapsed >= axis.cycleDuration;
        if (reachedEnd) altElapsed = axis.cycleDuration;

        // 오프셋 계산
        float t    = Mathf.Clamp01(altElapsed / axis.cycleDuration);
        float rawT = axis.forward ? t : 1f - t;
        Vector3 offset = Vector3.Lerp(axis.fromOffset, axis.toOffset, ApplyEase(rawT, ease));

        if (isH) cachedHOffset = offset;
        else     cachedVOffset = offset;

        ApplyPosition(cachedHOffset + cachedVOffset);

        // 끝점 도달 → 이 축의 방향 반전, 다른 축으로 전환
        if (reachedEnd)
        {
            altElapsed = 0f;

            // 각 축이 독립적으로 PingPong
            if (isH) hAxis.forward = !hAxis.forward;
            else     vAxis.forward = !vAxis.forward;

            bool otherEnabled = isH ? verticalEnabled : horizontalEnabled;
            if (otherEnabled)
                currentPhase = isH ? Phase.Vertical : Phase.Horizontal;
        }
    }

    #endregion

    #region Utility

    public void SetPaused(bool paused) => isActive = !paused;

    private void ApplyPosition(Vector3 offset)
    {
        Vector3 targetPos = initialPosition + offset;

        if (_rigidbody)
            _rigidbody.MovePosition(targetPos);
        else
            transform.position = targetPos;
    }

    private float ApplyEase(float t, EaseType ease)
    {
        return ease == EaseType.EaseInOut ? t * t * (3f - 2f * t) : t;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!drawDebugLines) return;

        if (waypointLeft && waypointRight)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(waypointLeft.position, waypointRight.position);
        }
        if (waypointForward && waypointBack)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(waypointBack.position, waypointForward.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        if (waypointLeft)    Gizmos.DrawLine(transform.position, waypointLeft.position);
        if (waypointRight)   Gizmos.DrawLine(transform.position, waypointRight.position);
        if (waypointForward) Gizmos.DrawLine(transform.position, waypointForward.position);
        if (waypointBack)    Gizmos.DrawLine(transform.position, waypointBack.position);
    }

    #endregion
}