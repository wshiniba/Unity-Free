using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人生命值与受伤处理。
/// </summary>
public class Enemy : MonoBehaviour
{
    [Tooltip("敌人的最大生命值。")]
    public int maxHealth = 3;

    [Tooltip("碰到玩家攻击判定框时受到的伤害。")]
    public int hitBoxDamage = 1;

    [Tooltip("当前生命值，仅用于运行时调试观察。")]
    public int currentHealth;

    [Header("受击反馈")]
    [Tooltip("受击后闪烁的总时长。")]
    public float hitFlashDuration = 1f;

    [Tooltip("闪烁时透明度在高低状态之间切换的间隔。")]
    public float hitFlashInterval = 0.08f;

    [Tooltip("受击闪烁时的最低透明度。")]
    [Range(0f, 1f)]
    public float hitFlashMinAlpha = 0.35f;

    [Header("血条 UI")]
    [Tooltip("真实血量填充层。未指定时自动查找名为 Fill 的子物体 Image。")]
    public Image healthFillImage;

    [Tooltip("缓冲血量填充层。可选；未指定时自动查找名为 Buffer 的子物体 Image。")]
    public Image healthBufferImage;

    [Tooltip("没有 Buffer 层时，Fill 自身平滑追到目标血量的速度。")]
    public float healthFillLerpSpeed = 8f;

    [Tooltip("有 Buffer 层时，Buffer 开始减少前的延迟。")]
    public float healthBufferDelay = 0.15f;

    [Tooltip("有 Buffer 层时，Buffer 平滑追到真实血量的速度。")]
    public float healthBufferLerpSpeed = 4f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine hitFlashRoutine;
    private Coroutine healthBufferRoutine;
    private float targetHealthRatio = 1f;

    void Awake()
    {
        currentHealth = maxHealth;
        CacheSpriteRenderers();
        CacheHealthBarImages();
        SetHealthBarInstant(GetHealthRatio());
    }

    void Update()
    {
        if (healthFillImage == null || healthBufferImage != null) return;

        healthFillImage.fillAmount = Mathf.MoveTowards(
            healthFillImage.fillAmount,
            targetHealthRatio,
            healthFillLerpSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerHitBox")) return;

        TakeDamage(GetDamageFromHitBox(other));
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        RestartHitFlash();
    }

    private void Die()
    {
        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        if (healthBufferRoutine != null)
            StopCoroutine(healthBufferRoutine);

        RestoreSpriteColors();
        Destroy(gameObject);
    }

    private int GetDamageFromHitBox(Collider2D hitBox)
    {
        PlayerAttack playerAttack = hitBox.GetComponentInParent<PlayerAttack>();
        return playerAttack != null ? playerAttack.CurrentAttackDamage : hitBoxDamage;
    }

    private void RestartHitFlash()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0) return;

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float elapsed = 0f;
        bool faded = false;

        while (elapsed < hitFlashDuration)
        {
            SetSpriteAlpha(faded ? 1f : hitFlashMinAlpha);
            faded = !faded;

            float waitTime = Mathf.Max(0.01f, hitFlashInterval);
            yield return new WaitForSeconds(waitTime);
            elapsed += waitTime;
        }

        RestoreSpriteColors();
        hitFlashRoutine = null;
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i].color;
    }

    private void SetSpriteAlpha(float alpha)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null) continue;

            Color color = originalColors[i];
            color.a *= alpha;
            spriteRenderers[i].color = color;
        }
    }

    private void RestoreSpriteColors()
    {
        if (spriteRenderers == null || originalColors == null) return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    private void CacheHealthBarImages()
    {
        if (healthFillImage == null)
            healthFillImage = FindChildImage("Fill");

        if (healthBufferImage == null)
            healthBufferImage = FindChildImage("Buffer");
    }

    private Image FindChildImage(string childName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].name == childName)
                return images[i];
        }

        return null;
    }

    private void UpdateHealthBar()
    {
        targetHealthRatio = GetHealthRatio();

        if (healthFillImage == null) return;

        if (healthBufferImage == null)
            return;

        healthFillImage.fillAmount = targetHealthRatio;

        if (healthBufferRoutine != null)
            StopCoroutine(healthBufferRoutine);

        healthBufferRoutine = StartCoroutine(HealthBufferRoutine());
    }

    private IEnumerator HealthBufferRoutine()
    {
        if (healthBufferDelay > 0f)
            yield return new WaitForSeconds(healthBufferDelay);

        while (healthBufferImage != null && healthBufferImage.fillAmount > targetHealthRatio)
        {
            healthBufferImage.fillAmount = Mathf.MoveTowards(
                healthBufferImage.fillAmount,
                targetHealthRatio,
                healthBufferLerpSpeed * Time.deltaTime);

            yield return null;
        }

        if (healthBufferImage != null)
            healthBufferImage.fillAmount = targetHealthRatio;

        healthBufferRoutine = null;
    }

    private void SetHealthBarInstant(float ratio)
    {
        targetHealthRatio = ratio;

        if (healthFillImage != null)
            healthFillImage.fillAmount = ratio;

        if (healthBufferImage != null)
            healthBufferImage.fillAmount = ratio;
    }

    private float GetHealthRatio()
    {
        return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    }
}
