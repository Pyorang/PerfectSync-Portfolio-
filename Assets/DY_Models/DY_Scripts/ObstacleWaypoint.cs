using UnityEngine;

/// <summary>
/// 장애물 Waypoint 시각화 헬퍼.
/// 빈 오브젝트에 부착하면 씬 뷰에서 Waypoint 위치를 Gizmo로 표시한다.
/// </summary>
public class ObstacleWaypoint : MonoBehaviour
{
    public enum WaypointAxis { Horizontal, Vertical }

    [Tooltip("이 Waypoint가 어느 축에 해당하는지 (색상 자동 변경)")]
    [SerializeField] private WaypointAxis axis = WaypointAxis.Horizontal;

    [SerializeField][Range(0.1f, 2f)] private float gizmoRadius = 0.4f;
    [SerializeField] private bool showLabel = true;

    private static readonly Color ColorHorizontal     = new Color(1f, 0.3f, 0.3f, 0.8f);
    private static readonly Color ColorVertical       = new Color(0.3f, 0.5f, 1f, 0.8f);
    private static readonly Color ColorSelected       = new Color(1f, 1f, 0f, 0.5f);
    private const float FILL_ALPHA = 0.2f;

    private void OnDrawGizmos()
    {
        Color color = axis == WaypointAxis.Horizontal ? ColorHorizontal : ColorVertical;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);

        Color fill = color;
        fill.a = FILL_ALPHA;
        Gizmos.color = fill;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = ColorSelected;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 1.5f);

#if UNITY_EDITOR
        if (showLabel)
        {
            var style = new GUIStyle
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (gizmoRadius + 0.3f),
                gameObject.name,
                style
            );
        }
#endif
    }
}