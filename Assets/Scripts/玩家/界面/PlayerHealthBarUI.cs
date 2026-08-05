using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家生命值 UI 绑定脚本。
/// 只负责读取 PlayerHealth 并刷新手动搭好的血条，不再自动创建 Canvas 或调整屏幕位置。
/// </summary>
public class PlayerHealthBarUI : MonoBehaviour
{
    public enum FillUpdateMode
    {
        ImageFillAmount,
        RectAnchorWidth
    }

    public enum DamageReduceDirection
    {
        RightToLeft,
        LeftToRight
    }

    [Header("绑定")]
    [Tooltip("要显示的玩家生命值。为空时会优先从父物体查找 PlayerHealth，再从场景中查找 Player 2。")]
    public PlayerHealth targetHealth;

    [Tooltip("真实血量填充图片。建议手动拖入 Canvas 下的 Fill Image。")]
    public Image fillImage;

    [Tooltip("缓冲血量填充图片。为空时不显示缓冲血量。")]
    public Image bufferImage;

    [Tooltip("可选的血量文字，例如 8 / 10。为空时不显示文字。")]
    public Text healthText;

    [Header("刷新方式")]
    [Tooltip("血条刷新方式。ImageFillAmount 适合 Image Type 为 Filled 的素材；RectAnchorWidth 会横向缩放 RectTransform。")]
    public FillUpdateMode fillUpdateMode = FillUpdateMode.RectAnchorWidth;

    [Tooltip("血量减少时的方向。RightToLeft 表示右侧向左减少，LeftToRight 表示左侧向右减少。")]
    public DamageReduceDirection damageReduceDirection = DamageReduceDirection.RightToLeft;

    [Tooltip("缓冲血量追向真实血量的速度。数值越大，黄条回落越快。")]
    public float bufferLerpSpeed = 6f;

    [Tooltip("没有绑定到 PlayerHealth 时是否隐藏整个血条。")]
    public bool hideWhenNoTarget = false;

    [Header("文字")]
    [Tooltip("开启后，healthText 会显示当前血量 / 最大血量。")]
    public bool showHealthText = true;

    private float bufferRatio = 1f;
    private float fillFullWidth = -1f;
    private float fillLeftEdge;
    private float fillRightEdge;
    private float bufferFullWidth = -1f;
    private float bufferLeftEdge;
    private float bufferRightEdge;

    private void Awake()
    {
        BindBestHealthTarget();
        CaptureInitialBarLayout();
        RefreshImmediate();
    }

    private void OnEnable()
    {
        BindBestHealthTarget();
        CaptureInitialBarLayout();
        RefreshImmediate();
    }

    private void Update()
    {
        if (targetHealth == null)
            BindBestHealthTarget();

        bool hasTarget = targetHealth != null;
        if (hideWhenNoTarget && gameObject.activeSelf != hasTarget)
            gameObject.SetActive(hasTarget);

        if (!hasTarget)
            return;

        float ratio = targetHealth.GetHealthRatio();
        bufferRatio = Mathf.MoveTowards(bufferRatio, ratio, Mathf.Max(0.01f, bufferLerpSpeed) * Time.unscaledDeltaTime);

        SetImageRatio(fillImage, ratio);
        SetImageRatio(bufferImage, bufferRatio);
        UpdateHealthText();
    }

    [ContextMenu("立即刷新血条")]
    public void RefreshImmediate()
    {
        if (targetHealth == null)
            BindBestHealthTarget();

        float ratio = targetHealth != null ? targetHealth.GetHealthRatio() : 1f;
        bufferRatio = ratio;

        SetImageRatio(fillImage, ratio);
        SetImageRatio(bufferImage, ratio);
        UpdateHealthText();
    }

    private void BindBestHealthTarget()
    {
        if (targetHealth != null)
            return;

        PlayerHealth parentHealth = GetComponentInParent<PlayerHealth>();
        if (parentHealth != null)
        {
            targetHealth = parentHealth;
            return;
        }

        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        PlayerHealth best = null;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth health = healths[i];
            if (health == null)
                continue;

            if (best == null || IsPreferredPlayerHealth(health, best))
                best = health;
        }

        targetHealth = best;
    }

    private bool IsPreferredPlayerHealth(PlayerHealth candidate, PlayerHealth current)
    {
        bool candidateIsPlayer2 = candidate.name.Contains("Player 2");
        bool currentIsPlayer2 = current != null && current.name.Contains("Player 2");
        if (candidateIsPlayer2 != currentIsPlayer2)
            return candidateIsPlayer2;

        return current == null;
    }

    private void SetImageRatio(Image image, float ratio)
    {
        if (image == null)
            return;

        ratio = Mathf.Clamp01(ratio);

        if (fillUpdateMode == FillUpdateMode.ImageFillAmount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = damageReduceDirection == DamageReduceDirection.RightToLeft
                ? (int)Image.OriginHorizontal.Left
                : (int)Image.OriginHorizontal.Right;
            image.fillAmount = ratio;
            return;
        }

        RectTransform rect = image.rectTransform;
        float fullWidth;
        float leftEdge;
        float rightEdge;
        GetCapturedBarLayout(image, rect, out fullWidth, out leftEdge, out rightEdge);

        Vector3 scale = rect.localScale;
        scale.x = 1f;
        rect.localScale = scale;

        Vector2 pivot = rect.pivot;
        pivot.x = damageReduceDirection == DamageReduceDirection.RightToLeft ? 0f : 1f;
        rect.pivot = pivot;

        Vector2 size = rect.sizeDelta;
        size.x = fullWidth * ratio;
        rect.sizeDelta = size;

        Vector2 position = rect.anchoredPosition;
        position.x = damageReduceDirection == DamageReduceDirection.RightToLeft ? leftEdge : rightEdge;
        rect.anchoredPosition = position;
    }

    private void CaptureInitialBarLayout()
    {
        CaptureImageLayout(fillImage, ref fillFullWidth, ref fillLeftEdge, ref fillRightEdge);
        CaptureImageLayout(bufferImage, ref bufferFullWidth, ref bufferLeftEdge, ref bufferRightEdge);
    }

    private void CaptureImageLayout(Image image, ref float fullWidth, ref float leftEdge, ref float rightEdge)
    {
        if (image == null || fullWidth > 0f)
            return;

        RectTransform rect = image.rectTransform;
        fullWidth = Mathf.Max(1f, rect.rect.width, rect.sizeDelta.x);
        leftEdge = rect.anchoredPosition.x - fullWidth * rect.pivot.x;
        rightEdge = leftEdge + fullWidth;
    }

    private void GetCapturedBarLayout(Image image, RectTransform rect, out float fullWidth, out float leftEdge, out float rightEdge)
    {
        if (image == fillImage && fillFullWidth > 0f)
        {
            fullWidth = fillFullWidth;
            leftEdge = fillLeftEdge;
            rightEdge = fillRightEdge;
            return;
        }

        if (image == bufferImage && bufferFullWidth > 0f)
        {
            fullWidth = bufferFullWidth;
            leftEdge = bufferLeftEdge;
            rightEdge = bufferRightEdge;
            return;
        }

        fullWidth = Mathf.Max(1f, rect.rect.width, rect.sizeDelta.x);
        leftEdge = rect.anchoredPosition.x - fullWidth * rect.pivot.x;
        rightEdge = leftEdge + fullWidth;
    }

    private void UpdateHealthText()
    {
        if (healthText == null || !showHealthText)
            return;

        if (targetHealth == null)
        {
            healthText.text = string.Empty;
            return;
        }

        healthText.text = $"{targetHealth.currentHealth} / {targetHealth.maxHealth}";
    }

    private void OnValidate()
    {
        bufferLerpSpeed = Mathf.Max(0.01f, bufferLerpSpeed);
    }
}
