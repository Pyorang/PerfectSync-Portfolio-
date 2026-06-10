using UnityEngine;

/// <summary>
/// 코코넛 대포의 발사 애니메이션 - 스쿼시 앤 스트레치 방식
/// 모델 전체가 앞으로 늘어났다 줄어드는 효과를 만듭니다.
/// </summary>
public class CoconutCannonFire : MonoBehaviour
{
    [Header("애니메이션 설정")]
    [Tooltip("늘어나는 방향 (대포 주둥이 방향)")]
    public Axis stretchAxis = Axis.Z;

    [Tooltip("늘어나는 최대 배율")]
    [Range(1.1f, 2.0f)]
    public float stretchAmount = 1.3f;

    [Tooltip("늘어날 때 옆으로 찌그러지는 배율")]
    [Range(0.5f, 1.0f)]
    public float squashAmount = 0.85f;

    [Tooltip("늘어나는 시간 (초)")]
    [Range(0.01f, 0.5f)]
    public float expandTime = 0.1f;

    [Tooltip("원래대로 돌아오는 시간 (초)")]
    [Range(0.05f, 1.0f)]
    public float returnTime = 0.35f;

    [Header("테스트")]
    [Tooltip("스페이스바로 발사 테스트")]
    public bool enableTestKey = true;

    public enum Axis { X, Y, Z }

    private Vector3 originalScale;
    private bool isFiring = false;
    private float timer = 0f;
    private bool expanding = true;

    void Start()
    {
        originalScale = transform.localScale;
        Debug.Log("[CoconutCannonFire] 준비 완료! 스페이스바를 누르세요.");
    }

    void Update()
    {
        if (enableTestKey && Input.GetKeyDown(KeyCode.Space))
            Fire();

        if (!isFiring) return;

        timer += Time.deltaTime;

        if (expanding)
        {
            float t = Mathf.Clamp01(timer / expandTime);
            float eased = 1f - (1f - t) * (1f - t);
            ApplyScale(eased);

            if (t >= 1f)
            {
                expanding = false;
                timer = 0f;
            }
        }
        else
        {
            float t = Mathf.Clamp01(timer / returnTime);
            float eased = t * t * (3f - 2f * t);
            ApplyScale(1f - eased);

            if (t >= 1f)
            {
                transform.localScale = originalScale;
                isFiring = false;
            }
        }
    }

    private void ApplyScale(float progress)
    {
        float stretch = Mathf.Lerp(1f, stretchAmount, progress);
        float squash = Mathf.Lerp(1f, squashAmount, progress);

        Vector3 scale = originalScale;
        switch (stretchAxis)
        {
            case Axis.X:
                scale.x *= stretch;
                scale.y *= squash;
                scale.z *= squash;
                break;
            case Axis.Y:
                scale.y *= stretch;
                scale.x *= squash;
                scale.z *= squash;
                break;
            case Axis.Z:
                scale.z *= stretch;
                scale.x *= squash;
                scale.y *= squash;
                break;
        }
        transform.localScale = scale;
    }

    /// <summary>
    /// 발사! 외부에서 호출하세요.
    /// </summary>
    public void Fire()
    {
        if (isFiring) return;
        isFiring = true;
        expanding = true;
        timer = 0f;
    }
}
