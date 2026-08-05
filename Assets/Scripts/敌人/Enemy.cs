using System.Collections;
using UnityEngine;

/// <summary>
/// 鏁屼汉琚Е鎵嬪鍐虫椂浣跨敤鐨勫鍐崇被鍨嬨€?/// </summary>
public enum EnemyExecutionType
{
    Pierce,
    Tear
}

/// <summary>
/// 鏁屼汉鍩虹灞炴€с€佺敓鍛藉€间笌鍙椾激鍙嶉銆?/// </summary>
public class Enemy : CombatEntityBase
{
    [Header("敌人游戏属性")]
    [Tooltip("敌人的游戏质量。触手力量需要大于该值才可以稳定抓取或牵引敌人；该值不会同步到 Rigidbody2D.mass。")]
    public float enemyMass = 1f;

    [Tooltip("敌人被触手抓取后的挣扎韧性。它不决定能否抓取，只影响抓取后的牵引迟滞和拖拽感。")]
    public float grabTenacity = 0f;

    [Header("触手处决")]
    [Tooltip("开启后，敌人生命值低于处决阈值时可以被触手处决。")]
    public bool canBeExecuted = true;

    [Tooltip("敌人生命值比例低于或等于该值时，允许触手处决。")]
    [Range(0f, 1f)]
    public float executionHealthRatio = 0.25f;

    [Tooltip("单触手穿刺处决后的特殊掉落倍率。")]
    public float pierceExecutionSpecialDropMultiplier = 1.5f;

    [Tooltip("双触手撕裂处决后的特殊掉落倍率。")]
    public float tearExecutionSpecialDropMultiplier = 2f;

    [Tooltip("最近一次处决写入的掉落倍率，仅用于运行时调试和掉落系统读取。")]
    public float pendingSpecialDropMultiplier = 1f;

    [Tooltip("撕裂处决结束后临时左右碎片存在的时间。")]
    public float executionFragmentLifetime = 0.7f;

    [Tooltip("撕裂处决结束后临时碎片继续向左右分开的速度。")]
    public float executionFragmentSpeed = 4f;

    [Tooltip("撕裂碎片使用的 Shader 名称。默认使用项目内的 Free/Tear Split Sprite。")]
    public string executionFragmentShaderName = "Free/Tear Split Sprite";

    [Tooltip("撕裂中心线位置。0.5 表示沿 Sprite 正中间竖线切开。")]
    [Range(0f, 1f)]
    public float executionFragmentSplit = 0.5f;

    [Tooltip("撕裂中心线边缘染色宽度。")]
    [Range(0f, 0.2f)]
    public float executionFragmentEdgeWidth = 0.035f;

    [Tooltip("撕裂中心线边缘颜色。")]
    public Color executionFragmentEdgeColor = new Color(0.55f, 0.02f, 0.02f, 1f);

    [Header("触手投掷撞击")]
    [Tooltip("敌人被触手甩出后，投掷撞击判定持续的最长时间。")]
    public float tentacleThrowImpactWindow = 2f;

    [Tooltip("投掷撞击速度低于该值时不造成撞击伤害。")]
    public float tentacleThrowMinImpactSpeed = 6f;

    [Tooltip("投掷撞击的基础伤害。")]
    public int tentacleThrowBaseDamage = 1;

    [Tooltip("超过最低撞击速度的部分，按该倍率转为额外伤害。")]
    public float tentacleThrowSpeedDamageScale = 0.25f;

    [Tooltip("投掷撞击可以结算伤害的实体层级。")]
    public LayerMask tentacleThrowImpactLayer = ~0;

    [Header("触手抓持抱摔")]
    [Tooltip("开启后，敌人被触手抓持时撞到实体表面会按力量和速度结算抱摔伤害。")]
    public bool allowTentacleHeldSmashDamage = true;

    [Tooltip("抓持抱摔速度低于该值时不造成伤害。")]
    public float tentacleHeldSmashMinImpactSpeed = 4f;

    [Tooltip("抓持抱摔的基础伤害。")]
    public int tentacleHeldSmashBaseDamage = 1;

    [Tooltip("触手力量转为抱摔额外伤害的倍率。")]
    public float tentacleHeldSmashPowerDamageScale = 0.5f;

    [Tooltip("超过最低抱摔速度的部分，按该倍率转为额外伤害。")]
    public float tentacleHeldSmashSpeedDamageScale = 0.15f;

    [Tooltip("同一敌人两次抓持抱摔伤害之间的最短间隔。")]
    public float tentacleHeldSmashCooldown = 0.35f;

    [Tooltip("抓持抱摔可以结算伤害的实体层级。")]
    public LayerMask tentacleHeldSmashImpactLayer = ~0;

    [Header("受击击退")]
    [Tooltip("开启后，敌人受到带方向的攻击时会产生击退。")]
    public bool enableHitKnockback = true;

    [Tooltip("受击击退的基础冲量。")]
    public float hitKnockbackBaseForce = 2.5f;

    [Tooltip("攻击速度比例转为额外击退冲量的倍率。")]
    public float hitKnockbackSpeedForce = 6f;

    [Tooltip("受击击退后的最大速度。小于等于 0 时不限制。")]
    public float hitKnockbackMaxSpeed = 18f;

    [Header("受击反馈")]
    [Tooltip("受击闪烁的总时长。")]
    public float hitFlashDuration = 1f;

    [Tooltip("受击闪烁透明度切换间隔。")]
    public float hitFlashInterval = 0.08f;

    [Tooltip("受击闪烁时的最低透明度。")]
    [Range(0f, 1f)]
    public float hitFlashMinAlpha = 0.35f;

    [Header("死亡反馈")]
    [Tooltip("开启后，普通死亡时敌人会淡出后销毁。触手撕裂处决会优先使用撕裂表现。")]
    public bool enableDeathFade = true;

    [Tooltip("普通死亡淡出的持续时间。")]
    public float deathFadeDuration = 0.25f;

    [Tooltip("普通死亡淡出时是否禁用碰撞体，避免尸体继续阻挡或触发交互。")]
    public bool disableCollidersOnDeath = true;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine hitFlashRoutine;
    private Rigidbody2D cachedRigidbody;
    private bool isTentacleThrown;
    private bool isTentacleHeld;
    private bool isBeingExecuted;
    private EnemyExecutionType pendingExecutionType;
    private float tentacleThrownExpireTime;
    private float tentacleHeldExpireTime;
    private float nextTentacleHeldSmashTime;
    private float currentTentacleHeldPower;
    private Vector2 lastTentacleThrowVelocity;
    private Vector2 lastTentacleHeldVelocity;
    private SpriteRenderer tearSourceRenderer;
    private bool tearSourceRendererWasEnabled;
    private ExecutionFragmentVisual leftTearFragment;
    private ExecutionFragmentVisual rightTearFragment;
    private Vector3 leftTearFragmentOffset;
    private Vector3 rightTearFragmentOffset;
    private bool isDying;

    protected override void Awake()
    {
        base.Awake();
        cachedRigidbody = GetComponent<Rigidbody2D>();
        ApplyRigidbodySettings();
        CacheSpriteRenderers();
    }

    private void FixedUpdate()
    {
        if (isTentacleHeld && Time.time > tentacleHeldExpireTime)
            ClearTentacleHeldState();

        if (isTentacleThrown && Time.time > tentacleThrownExpireTime)
        {
            ClearTentacleThrownState();
            return;
        }

        if (cachedRigidbody != null && isTentacleThrown)
            lastTentacleThrowVelocity = cachedRigidbody.linearVelocity;

        if (cachedRigidbody != null && isTentacleHeld)
            lastTentacleHeldVelocity = cachedRigidbody.linearVelocity;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        enemyMass = Mathf.Max(0.01f, enemyMass);
        grabTenacity = Mathf.Max(0f, grabTenacity);
        executionHealthRatio = Mathf.Clamp01(executionHealthRatio);
        pierceExecutionSpecialDropMultiplier = Mathf.Max(1f, pierceExecutionSpecialDropMultiplier);
        tearExecutionSpecialDropMultiplier = Mathf.Max(1f, tearExecutionSpecialDropMultiplier);
        pendingSpecialDropMultiplier = Mathf.Max(1f, pendingSpecialDropMultiplier);
        executionFragmentLifetime = Mathf.Max(0f, executionFragmentLifetime);
        executionFragmentSpeed = Mathf.Max(0f, executionFragmentSpeed);
        executionFragmentSplit = Mathf.Clamp01(executionFragmentSplit);
        executionFragmentEdgeWidth = Mathf.Clamp(executionFragmentEdgeWidth, 0f, 0.2f);
        tentacleThrowImpactWindow = Mathf.Max(0f, tentacleThrowImpactWindow);
        tentacleThrowMinImpactSpeed = Mathf.Max(0f, tentacleThrowMinImpactSpeed);
        tentacleThrowBaseDamage = Mathf.Max(0, tentacleThrowBaseDamage);
        tentacleThrowSpeedDamageScale = Mathf.Max(0f, tentacleThrowSpeedDamageScale);
        tentacleHeldSmashMinImpactSpeed = Mathf.Max(0f, tentacleHeldSmashMinImpactSpeed);
        tentacleHeldSmashBaseDamage = Mathf.Max(0, tentacleHeldSmashBaseDamage);
        tentacleHeldSmashPowerDamageScale = Mathf.Max(0f, tentacleHeldSmashPowerDamageScale);
        tentacleHeldSmashSpeedDamageScale = Mathf.Max(0f, tentacleHeldSmashSpeedDamageScale);
        tentacleHeldSmashCooldown = Mathf.Max(0f, tentacleHeldSmashCooldown);
        hitKnockbackBaseForce = Mathf.Max(0f, hitKnockbackBaseForce);
        hitKnockbackSpeedForce = Mathf.Max(0f, hitKnockbackSpeedForce);
        hitKnockbackMaxSpeed = Mathf.Max(0f, hitKnockbackMaxSpeed);
        deathFadeDuration = Mathf.Max(0f, deathFadeDuration);

        if (!Application.isPlaying)
            currentHealth = maxHealth;

        ApplyRigidbodySettings();
    }

    /// <summary>
    /// 鍚庣画 Excel/CSV 閰嶇疆瀵煎叆鍚庤皟鐢ㄨ繖涓柟娉曪紝鎶婅〃鏍煎睘鎬у簲鐢ㄥ埌鍦烘櫙鎴?prefab 涓殑鏁屼汉瀹炰緥銆?    /// </summary>
    public void ApplyConfig(string newConfigId, int newMaxHealth, float newEnemyMass, int newHitBoxDamage)
    {
        configId = newConfigId;
        maxHealth = Mathf.Max(1, newMaxHealth);
        enemyMass = Mathf.Max(0.01f, newEnemyMass);
        hitBoxDamage = Mathf.Max(0, newHitBoxDamage);
        currentHealth = maxHealth;

        ApplyRigidbodySettings();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsActivePlayerAttackHitBox(other))
            TakeDamage(GetDamageFromHitBox(other));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyTentacleHeldSmashImpact(collision);
        TryApplyTentacleThrowImpact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryApplyTentacleHeldSmashImpact(collision);
    }

    private bool IsActivePlayerAttackHitBox(Collider2D other)
    {
        if (other == null || !other.CompareTag("PlayerHitBox"))
            return false;

        PlayerAttack playerAttack = other.GetComponentInParent<PlayerAttack>();
        return playerAttack != null && playerAttack.IsActiveAttackHitBox(other);
    }

    /// <summary>
    /// 瑙︽墜鏉惧紑鏁屼汉骞舵妸鏁屼汉鐢╁嚭鏃惰皟鐢紝寮€鍚竴娆＄煭鏃堕棿鐨勬挒鍑讳激瀹冲垽瀹氥€?    /// </summary>
    public void MarkTentacleThrown(Vector2 throwVelocity)
    {
        if (throwVelocity.sqrMagnitude <= 0.000001f)
            return;

        ClearTentacleHeldState();
        isTentacleThrown = true;
        lastTentacleThrowVelocity = throwVelocity;
        tentacleThrownExpireTime = Time.time + Mathf.Max(0f, tentacleThrowImpactWindow);
    }

    /// <summary>
    /// 鏁屼汉琚Е鎵嬫寔缁姄浣忔椂鐢?OdmController 鍒锋柊锛岀敤浜庡尯鍒嗘姄鎸佹姳鎽斿拰鏉炬墜鎶曟幏銆?    /// </summary>
    public void MarkTentacleHeld(float tentaclePower, Vector2 heldVelocity)
    {
        if (!allowTentacleHeldSmashDamage || currentHealth <= 0 || isBeingExecuted)
            return;

        isTentacleHeld = true;
        currentTentacleHeldPower = Mathf.Max(0f, tentaclePower);
        lastTentacleHeldVelocity = heldVelocity;
        tentacleHeldExpireTime = Time.time + 0.12f;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero, 0f);
    }

    public void TakeDamage(int damage, Vector2 hitDirection, float hitSpeedRatio)
    {
        if (damage <= 0 || currentHealth <= 0 || isDying) return;

        ApplyHealthDamage(damage);
        ApplyHitKnockback(hitDirection, hitSpeedRatio);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        RestartHitFlash();
    }

    public bool CanBeExecuted()
    {
        return canBeExecuted && !isBeingExecuted && currentHealth > 0 && GetHealthRatio() <= executionHealthRatio;
    }

    /// <summary>
    /// 瑙︽墜澶勫喅寮€濮嬫椂璋冪敤銆傝繖閲屽彧閿佸畾澶勫喅鐘舵€佸拰鎺夎惤鍊嶇巼锛屼笉浼氱珛鍒绘潃姝绘晫浜恒€?    /// </summary>
    public bool BeginTentacleExecution(EnemyExecutionType executionType)
    {
        if (!CanBeExecuted())
            return false;

        isBeingExecuted = true;
        pendingExecutionType = executionType;
        pendingSpecialDropMultiplier = executionType == EnemyExecutionType.Tear
            ? tearExecutionSpecialDropMultiplier
            : pierceExecutionSpecialDropMultiplier;

        ClearTentacleThrownState();
        ClearTentacleHeldState();
        if (cachedRigidbody != null)
            cachedRigidbody.linearVelocity = Vector2.zero;

        return true;
    }

    /// <summary>
    /// 瑙︽墜澶勫喅琛ㄧ幇缁撴潫鏃惰皟鐢紝姝ゆ椂鎵嶇粨绠楁渶缁堜激瀹冲拰姝讳骸銆?    /// </summary>
    public void CompleteTentacleExecution()
    {
        if (!isBeingExecuted || currentHealth <= 0)
            return;

        if (pendingExecutionType == EnemyExecutionType.Tear)
            FinishTearExecutionVisual();

        TakeDamage(currentHealth);
    }

    public void CancelTentacleExecution()
    {
        if (!isBeingExecuted)
            return;

        isBeingExecuted = false;
        pendingSpecialDropMultiplier = 1f;
        ClearTentacleHeldState();
        RestoreTearExecutionVisual();
    }

    public void BeginTearExecutionVisual(Vector2 leftAnchor, Vector2 rightAnchor)
    {
        tearSourceRenderer = FindMainSpriteRenderer();
        if (tearSourceRenderer == null || tearSourceRenderer.sprite == null || executionFragmentLifetime <= 0f)
            return;

        Shader splitShader = Shader.Find(executionFragmentShaderName);
        if (splitShader == null)
        {
            Debug.LogWarning($"找不到撕裂碎片 Shader：{executionFragmentShaderName}", this);
            return;
        }

        leftTearFragmentOffset = GetTearFragmentAnchorOffset(tearSourceRenderer, -1f);
        rightTearFragmentOffset = GetTearFragmentAnchorOffset(tearSourceRenderer, 1f);

        tearSourceRendererWasEnabled = tearSourceRenderer.enabled;
        tearSourceRenderer.enabled = false;

        leftTearFragment = SpawnExecutionFragment(tearSourceRenderer, splitShader, -1f, leftAnchor, leftTearFragmentOffset, false);
        rightTearFragment = SpawnExecutionFragment(tearSourceRenderer, splitShader, 1f, rightAnchor, rightTearFragmentOffset, false);
    }

    public void UpdateTearExecutionVisual(Vector2 leftAnchor, Vector2 rightAnchor)
    {
        if (leftTearFragment != null)
            leftTearFragment.SetFollowAnchor(leftAnchor, leftTearFragmentOffset);

        if (rightTearFragment != null)
            rightTearFragment.SetFollowAnchor(rightAnchor, rightTearFragmentOffset);
    }

    public bool IsBeingExecuted()
    {
        return isBeingExecuted;
    }

    private void ApplyRigidbodySettings()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.freezeRotation = true;
    }

    private void Die()
    {
        if (isDying)
            return;

        isDying = true;
        EnemyLootDropper lootDropper = GetComponent<EnemyLootDropper>();
        if (lootDropper != null)
            lootDropper.DropLoot(this);

        isBeingExecuted = false;
        pendingSpecialDropMultiplier = 1f;
        ClearTentacleHeldState();

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        RestoreSpriteColors();

        if (enableDeathFade && pendingExecutionType != EnemyExecutionType.Tear && deathFadeDuration > 0f)
            StartCoroutine(DeathFadeRoutine());
        else
            Destroy(gameObject);
    }

    private int GetDamageFromHitBox(Collider2D hitBox)
    {
        PlayerAttack playerAttack = hitBox.GetComponentInParent<PlayerAttack>();
        return playerAttack != null ? playerAttack.CurrentAttackDamage : hitBoxDamage;
    }

    private void ApplyHitKnockback(Vector2 hitDirection, float hitSpeedRatio)
    {
        if (!enableHitKnockback || cachedRigidbody == null || isBeingExecuted)
            return;

        if (hitDirection.sqrMagnitude <= 0.000001f)
            return;

        Vector2 direction = hitDirection.normalized;
        float force = Mathf.Max(0f, hitKnockbackBaseForce)
            + Mathf.Clamp01(hitSpeedRatio) * Mathf.Max(0f, hitKnockbackSpeedForce);

        if (force <= 0f)
            return;

        cachedRigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        float maxSpeed = Mathf.Max(0f, hitKnockbackMaxSpeed);
        if (maxSpeed > 0f && cachedRigidbody.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            cachedRigidbody.linearVelocity = cachedRigidbody.linearVelocity.normalized * maxSpeed;
    }

    private IEnumerator DeathFadeRoutine()
    {
        DisableDeathInteraction();

        float duration = Mathf.Max(0.01f, deathFadeDuration);
        float elapsed = 0f;
        Color[] deathStartColors = new Color[spriteRenderers != null ? spriteRenderers.Length : 0];

        for (int i = 0; i < deathStartColors.Length; i++)
            deathStartColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < deathStartColors.Length; i++)
            {
                if (spriteRenderers[i] == null)
                    continue;

                Color color = deathStartColors[i];
                color.a *= alpha;
                spriteRenderers[i].color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void DisableDeathInteraction()
    {
        EnemySimpleAI simpleAI = GetComponent<EnemySimpleAI>();
        if (simpleAI != null)
            simpleAI.enabled = false;

        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
        }

        if (!disableCollidersOnDeath)
            return;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void TryApplyTentacleThrowImpact(Collision2D collision)
    {
        if (!isTentacleThrown || collision == null)
            return;

        if (Time.time > tentacleThrownExpireTime)
        {
            ClearTentacleThrownState();
            return;
        }

        Collider2D otherCollider = collision.collider;
        if (otherCollider == null || otherCollider.isTrigger)
            return;

        if (IsPlayerCollider(otherCollider))
            return;

        int otherLayerMask = 1 << otherCollider.gameObject.layer;
        if ((tentacleThrowImpactLayer.value & otherLayerMask) == 0)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed <= 0.001f)
            impactSpeed = lastTentacleThrowVelocity.magnitude;

        if (impactSpeed < tentacleThrowMinImpactSpeed)
            return;

        int damage = CalculateTentacleThrowImpactDamage(impactSpeed);
        ClearTentacleThrownState();
        TakeDamage(damage);
    }

    private void TryApplyTentacleHeldSmashImpact(Collision2D collision)
    {
        if (!allowTentacleHeldSmashDamage || !isTentacleHeld || isTentacleThrown || collision == null)
            return;

        if (Time.time < nextTentacleHeldSmashTime || Time.time > tentacleHeldExpireTime)
            return;

        Collider2D otherCollider = collision.collider;
        if (otherCollider == null || otherCollider.isTrigger)
            return;

        if (IsPlayerCollider(otherCollider))
            return;

        int otherLayerMask = 1 << otherCollider.gameObject.layer;
        if ((tentacleHeldSmashImpactLayer.value & otherLayerMask) == 0)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed <= 0.001f)
            impactSpeed = lastTentacleHeldVelocity.magnitude;

        if (impactSpeed < tentacleHeldSmashMinImpactSpeed)
            return;

        int damage = CalculateTentacleHeldSmashDamage(impactSpeed);
        nextTentacleHeldSmashTime = Time.time + Mathf.Max(0f, tentacleHeldSmashCooldown);
        TakeDamage(damage);
    }

    private bool IsPlayerCollider(Collider2D otherCollider)
    {
        return otherCollider != null && otherCollider.GetComponentInParent<OdmController>() != null;
    }

    private int CalculateTentacleThrowImpactDamage(float impactSpeed)
    {
        float extraSpeed = Mathf.Max(0f, impactSpeed - tentacleThrowMinImpactSpeed);
        int speedDamage = Mathf.FloorToInt(extraSpeed * Mathf.Max(0f, tentacleThrowSpeedDamageScale));
        return Mathf.Max(1, tentacleThrowBaseDamage + speedDamage);
    }

    private int CalculateTentacleHeldSmashDamage(float impactSpeed)
    {
        int powerDamage = Mathf.FloorToInt(currentTentacleHeldPower * Mathf.Max(0f, tentacleHeldSmashPowerDamageScale));
        float extraSpeed = Mathf.Max(0f, impactSpeed - tentacleHeldSmashMinImpactSpeed);
        int speedDamage = Mathf.FloorToInt(extraSpeed * Mathf.Max(0f, tentacleHeldSmashSpeedDamageScale));
        return Mathf.Max(1, tentacleHeldSmashBaseDamage + powerDamage + speedDamage);
    }

    private void ClearTentacleThrownState()
    {
        isTentacleThrown = false;
        lastTentacleThrowVelocity = Vector2.zero;
        tentacleThrownExpireTime = 0f;
    }

    private void ClearTentacleHeldState()
    {
        isTentacleHeld = false;
        currentTentacleHeldPower = 0f;
        lastTentacleHeldVelocity = Vector2.zero;
        tentacleHeldExpireTime = 0f;
    }

    private void FinishTearExecutionVisual()
    {
        if (leftTearFragment != null)
            leftTearFragment.ReleaseFollow(new Vector3(-executionFragmentSpeed, 0.6f, 0f), executionFragmentLifetime);

        if (rightTearFragment != null)
            rightTearFragment.ReleaseFollow(new Vector3(executionFragmentSpeed, 0.6f, 0f), executionFragmentLifetime);

        leftTearFragment = null;
        rightTearFragment = null;
        tearSourceRenderer = null;
    }

    private void RestoreTearExecutionVisual()
    {
        if (tearSourceRenderer != null)
            tearSourceRenderer.enabled = tearSourceRendererWasEnabled;

        if (leftTearFragment != null)
            Destroy(leftTearFragment.gameObject);

        if (rightTearFragment != null)
            Destroy(rightTearFragment.gameObject);

        leftTearFragment = null;
        rightTearFragment = null;
        tearSourceRenderer = null;
    }

    private ExecutionFragmentVisual SpawnExecutionFragment(
        SpriteRenderer source,
        Shader splitShader,
        float side,
        Vector3 anchor,
        Vector3 anchorOffset,
        bool releaseImmediately)
    {
        GameObject fragment = new GameObject(side < 0f ? "Enemy_TearFragment_Left" : "Enemy_TearFragment_Right");
        fragment.transform.position = anchor + anchorOffset;
        fragment.transform.rotation = source.transform.rotation;
        fragment.transform.localScale = source.transform.lossyScale;

        SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = source.color;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
        renderer.material = CreateExecutionFragmentMaterial(splitShader, side);

        ExecutionFragmentVisual visual = fragment.AddComponent<ExecutionFragmentVisual>();
        visual.Initialize(source.color);
        visual.SetFollowAnchor(anchor, anchorOffset);

        if (releaseImmediately)
            visual.ReleaseFollow(new Vector3(side * executionFragmentSpeed, 0.6f, 0f), executionFragmentLifetime);

        return visual;
    }

    private Vector3 GetTearFragmentAnchorOffset(SpriteRenderer source, float side)
    {
        Bounds bounds = source.bounds;
        float sideX = side < 0f ? bounds.min.x : bounds.max.x;
        Vector3 sidePoint = new Vector3(sideX, bounds.center.y, bounds.center.z);
        return bounds.center - sidePoint;
    }

    private Material CreateExecutionFragmentMaterial(Shader splitShader, float side)
    {
        Material material = new Material(splitShader);
        material.SetFloat("_Side", side);
        material.SetFloat("_Split", executionFragmentSplit);
        material.SetFloat("_EdgeWidth", executionFragmentEdgeWidth);
        material.SetColor("_EdgeColor", executionFragmentEdgeColor);
        material.SetFloat("_Alpha", 1f);
        return material;
    }

    private SpriteRenderer FindMainSpriteRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer best = null;
        float bestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || renderer.name.StartsWith("HealthBar"))
                continue;

            Vector3 size = renderer.bounds.size;
            float area = Mathf.Abs(size.x * size.y);
            if (best != null && area <= bestArea)
                continue;

            best = renderer;
            bestArea = area;
        }

        return best;
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
}

/// <summary>
/// 瑙︽墜鎾曡澶勫喅瀹屾垚鍚庣敓鎴愮殑涓存椂纰庣墖杩愬姩鍜屾贰鍑鸿〃鐜般€?/// </summary>
public class ExecutionFragmentVisual : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Material runtimeMaterial;
    private Vector3 velocity;
    private Vector3 followOffset;
    private Color baseColor;
    private float lifetime;
    private float elapsed;
    private bool isFollowing = true;
    private bool isReleased;

    public void Initialize(Color color)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        runtimeMaterial = spriteRenderer != null ? spriteRenderer.material : null;
        baseColor = color;
    }

    public void SetFollowAnchor(Vector3 anchor, Vector3 offset)
    {
        if (!isFollowing)
            return;

        followOffset = offset;
        transform.position = anchor + followOffset;
    }

    public void ReleaseFollow(Vector3 releaseVelocity, float lifetimeValue)
    {
        isFollowing = false;
        isReleased = true;
        velocity = releaseVelocity;
        lifetime = Mathf.Max(0.01f, lifetimeValue);
        elapsed = 0f;
    }

    private void Update()
    {
        if (!isReleased)
            return;

        elapsed += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 3f);

        if (spriteRenderer != null)
        {
            Color color = baseColor;
            float alpha = 1f - Mathf.Clamp01(elapsed / lifetime);
            color.a *= alpha;
            spriteRenderer.color = color;

            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat("_Alpha", alpha);
        }

        if (elapsed >= lifetime)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
