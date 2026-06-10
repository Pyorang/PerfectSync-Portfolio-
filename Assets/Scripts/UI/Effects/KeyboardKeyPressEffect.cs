using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class KeyboardKeyPressEffect : MonoBehaviour
{
    [Header("Press Settings")]
    [SerializeField] private float pressAmount = 10f;
    [SerializeField] private float pressDuration = 0.1f;
    [SerializeField] private float releaseDuration = 0.1f;
    [SerializeField] private float intervalBetweenPresses = 0.4f;

    [Header("Easing")]
    [SerializeField] private AnimationCurve pressCurve   = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private RectTransform rt;
    private Vector2 originalOffsetMax;
    private Coroutine loopCo;

    private void OnEnable()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        originalOffsetMax = rt.offsetMax;
        rt.offsetMax = originalOffsetMax;

        loopCo = StartCoroutine(PressLoop());
    }

    private void OnDisable()
    {
        if (loopCo != null)
        {
            StopCoroutine(loopCo);
            loopCo = null;
        }

        if (rt != null)
            rt.offsetMax = originalOffsetMax;
    }

    private IEnumerator PressLoop()
    {
        while (true)
        {
            yield return AnimateTop(0f, pressAmount, pressDuration, pressCurve);
            yield return AnimateTop(pressAmount, 0f, releaseDuration, releaseCurve);

            if (intervalBetweenPresses > 0f)
                yield return new WaitForSeconds(intervalBetweenPresses);
        }
    }

    private IEnumerator AnimateTop(float from, float to, float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            ApplyTop(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = curve.Evaluate(t);
            ApplyTop(Mathf.Lerp(from, to, eased));
            yield return null;
        }

        ApplyTop(to);
    }

    private void ApplyTop(float top)
    {
        rt.offsetMax = new Vector2(
            originalOffsetMax.x,
            originalOffsetMax.y - top
        );
    }
}
