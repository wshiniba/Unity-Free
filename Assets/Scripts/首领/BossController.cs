using System.Collections;
using UnityEngine;

public enum BossBattleState
{
    /// <summary>未触发 Boss 战。</summary>
    Dormant,

    /// <summary>入场和封场后的短暂停顿。</summary>
    Intro,

    /// <summary>Boss 无敌并释放弹幕或陷阱。</summary>
    AttackPattern,

    /// <summary>Boss 可被玩家攻击。</summary>
    Vulnerable,

    /// <summary>Boss 受击后隐藏并准备换位。</summary>
    HitReact,

    /// <summary>Boss 已被击败。</summary>
    Defeated
}

/// <summary>
/// ODM Boss 初版状态机：无敌放招、开放受伤窗口、受击换位、死亡解除封场。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossController : CombatEntityBase
{
    [Tooltip("Boss 当前战斗状态，仅用于运行时调试观察，不建议手动修改。")]
    public BossBattleState state = BossBattleState.Dormant;

    [Header("战斗流程")]
    [Tooltip("触发 Boss 战后，进入第一轮攻击前等待的时间。")]
    public float introDuration = 0.75f;

    [Tooltip("每轮攻击结束后 Boss 保持可受伤状态的时间。")]
    public float vulnerableDuration = 3f;

    [Tooltip("Boss 被打中后隐藏多久再换位并继续战斗。")]
    public float hitDisappearDuration = 0.45f;

    [Tooltip("Boss 受击或窗口结束后可移动到的位置。未指定时会自动读取名为 BossPositions 的子物体。")]
    public Transform[] bossPositions;

    [Header("战斗区域")]
    [Tooltip("Boss 战区域中心点，用于生成陷阱和可钩锁点。")]
    public Vector2 arenaCenter = new Vector2(197.8f, 8f);

    [Tooltip("Boss 战区域尺寸，用于计算陷阱分布范围。")]
    public Vector2 arenaSize = new Vector2(73f, 24f);

    [Tooltip("Boss 生成的可钩锁点所在层级。应与玩家 OdmController 的 anchorableLayer 保持一致。")]
    public LayerMask hookableAnchorLayer = 1 << 6;

    [Header("弹幕")]
    [Tooltip("Boss 弹幕飞行速度。")]
    public float projectileSpeed = 9f;

    [Tooltip("Boss 弹幕自动销毁前存在的时间。")]
    public float projectileLifetime = 5f;

    [Tooltip("Boss 弹幕命中玩家时记录的伤害值。当前初版只用于日志，实际效果是击退。")]
    public int projectileDamage = 1;

    [Tooltip("Boss 弹幕命中玩家时施加的击退冲量。")]
    public float projectileKnockback = 12f;

    [Header("陷阱")]
    [Tooltip("地面陷阱显示预警但尚未造成击退的时间。")]
    public float trapWarningDuration = 0.65f;

    [Tooltip("地面陷阱真正生效并可击退玩家的时间。")]
    public float trapActiveDuration = 1.1f;

    [Tooltip("地面陷阱命中玩家时记录的伤害值。当前初版只用于日志，实际效果是击退。")]
    public int trapDamage = 1;

    [Tooltip("地面陷阱命中玩家时施加的击退冲量。")]
    public float trapKnockback = 16f;

    [Header("动画")]
    [Tooltip("Boss 本体 Animator。未指定时自动使用当前物体上的 Animator。")]
    public Animator bossAnimator;

    [Tooltip("Boss 受击动画 Bool 参数名。")]
    public string hurtBoolParameterName = "isHurt";

    [Tooltip("Boss 发射弹幕或释放攻击期间使用的 Bool 参数名。名称确定后在 Animator 中添加同名 Bool 即可生效。")]
    public string attackBoolParameterName = "isAttack";

    [Tooltip("没有找到 hurt 动画片段时，受击动画的兜底等待时间。")]
    public float fallbackHurtAnimationDuration = 0.5f;

    [Header("弹反受制")]
    [Tooltip("Boss 释放的可弹反攻击被玩家弹反后，Boss 暂停后续行为并保持可受击的时间。")]
    public float parryStunDuration = 0.45f;

    [Tooltip("Boss 被弹反打断时显示的颜色。")]
    public Color parryStunTint = Color.white;

    [Tooltip("反弹弹幕命中 Boss 时的伤害倍率。最终伤害 = 弹幕原伤害 * 该倍率，最低为 1。")]
    public float reflectedProjectileDamageMultiplier = 1f;

    [Header("打击反馈")]
    [Tooltip("Boss 受到有效伤害时播放的命中停顿和相机震动反馈。未指定时会自动获取或添加。")]
    public HitFeedback hitFeedback;

    /// <summary>
    /// Boss 当前是否无敌。只有 Vulnerable 状态可以被 PlayerHitBox 造成伤害。
    /// </summary>
    public bool IsInvulnerable => state != BossBattleState.Vulnerable;

    private BossArenaController arena;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Coroutine battleRoutine;
    private Coroutine hitReactRoutine;
    private Coroutine parryStunRoutine;
    private int patternIndex;

    private void Reset()
    {
        configId = "boss";
        maxHealth = 12;
        hitBoxDamage = 1;
        currentHealth = maxHealth;
    }

    /// <summary>
    /// 初始化运行时引用和 Boss 受击触发器。
    /// </summary>
    protected override void Awake()
    {
        if (configId == "combat_entity")
            configId = "boss";

        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (bossAnimator == null) bossAnimator = GetComponent<Animator>();
        if (hitFeedback == null) hitFeedback = GetComponent<HitFeedback>();
        FindBossPositionsIfNeeded();

        Collider2D hitCollider = GetComponent<Collider2D>();
        hitCollider.isTrigger = true;
    }

    /// <summary>
    /// 由 BossArenaController 调用，启动 Boss 战主循环。
    /// </summary>
    public void BeginBattle(BossArenaController arenaController)
    {
        if (battleRoutine != null || state == BossBattleState.Defeated)
            return;

        arena = arenaController;
        FindBossPositionsIfNeeded();
        OdmController playerController = FindAnyObjectByType<OdmController>();
        if (playerController != null)
            player = playerController.transform;

        gameObject.SetActive(true);
        battleRoutine = StartCoroutine(BattleLoop(true));
    }

    private IEnumerator BattleLoop(bool playIntro)
    {
        SetVisible(true);
        SetBossBool(hurtBoolParameterName, false);
        SetBossBool(attackBoolParameterName, false);

        if (playIntro)
        {
            state = BossBattleState.Intro;
            yield return new WaitForSecondsRealtime(introDuration);
        }

        while (currentHealth > 0)
        {
            state = BossBattleState.AttackPattern;
            SetBossBool(hurtBoolParameterName, false);
            SetTint(new Color(0.85f, 0.2f, 0.2f, 1f));
            yield return ExecuteNextPattern();

            state = BossBattleState.Vulnerable;
            SetBossBool(attackBoolParameterName, false);
            SetTint(new Color(0.35f, 0.9f, 1f, 1f));
            yield return new WaitForSecondsRealtime(vulnerableDuration);

            if (state == BossBattleState.Vulnerable)
                RepositionAwayFromPlayer();
        }

        Defeat();
    }

    private IEnumerator ExecuteNextPattern()
    {
        int selectedPattern = patternIndex % 3;
        patternIndex++;

        if (selectedPattern == 0)
            yield return RadialBurstPattern();
        else if (selectedPattern == 1)
            yield return TrapFieldPattern();
        else
            yield return HookAnchorAndAimedShotsPattern();
    }

    private IEnumerator RadialBurstPattern()
    {
        int rings = Mathf.Clamp(3 + patternIndex / 3, 3, 6);
        int bulletsPerRing = 12;

        SetBossBool(attackBoolParameterName, true);

        for (int ring = 0; ring < rings; ring++)
        {
            float angleOffset = ring % 2 == 0 ? 0f : 15f;
            for (int i = 0; i < bulletsPerRing; i++)
            {
                float angle = angleOffset + i * 360f / bulletsPerRing;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                SpawnProjectile(transform.position, direction);
            }

            yield return new WaitForSecondsRealtime(0.65f);
        }

        SetBossBool(attackBoolParameterName, false);
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private IEnumerator TrapFieldPattern()
    {
        int trapCount = Mathf.Clamp(4 + patternIndex, 4, 8);
        float left = arenaCenter.x - arenaSize.x * 0.42f;
        float right = arenaCenter.x + arenaSize.x * 0.42f;
        float floorY = arenaCenter.y - arenaSize.y * 0.45f;

        for (int i = 0; i < trapCount; i++)
        {
            float x = Mathf.Lerp(left, right, trapCount == 1 ? 0.5f : i / (float)(trapCount - 1));
            SpawnTrap(new Vector2(x, floorY + 0.35f), new Vector2(3f, 1.1f));
        }

        yield return new WaitForSecondsRealtime(trapWarningDuration + trapActiveDuration + 0.6f);
    }

    private IEnumerator HookAnchorAndAimedShotsPattern()
    {
        SpawnHookableAnchor(arenaCenter + new Vector2(-18f, 8f), 3.2f);
        SpawnHookableAnchor(arenaCenter + new Vector2(0f, 11f), 3.2f);
        SpawnHookableAnchor(arenaCenter + new Vector2(18f, 8f), 3.2f);

        int volleys = 8;
        SetBossBool(attackBoolParameterName, true);
        for (int i = 0; i < volleys; i++)
        {
            Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
            Vector2 direction = (target - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.left;

            SpawnProjectile(transform.position, direction);
            SpawnProjectile(transform.position, Quaternion.Euler(0f, 0f, 10f) * direction);
            SpawnProjectile(transform.position, Quaternion.Euler(0f, 0f, -10f) * direction);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        SetBossBool(attackBoolParameterName, false);
        yield return new WaitForSecondsRealtime(1.1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsInvulnerable || !other.CompareTag("PlayerHitBox"))
            return;

        PlayerAttack attack = other.GetComponentInParent<PlayerAttack>();
        int damage = attack != null ? attack.CurrentAttackDamage : 1;
        TakeDamage(damage, GetHitDirection(other), GetHitSpeedRatio(other));
    }

    /// <summary>
    /// 在可受伤窗口内扣除 Boss 生命值。
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero, 0f);
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        TakeDamage(damage, hitDirection, 0f);
    }

    public void TakeDamage(int damage, Vector2 hitDirection, float hitSpeedRatio)
    {
        if (IsInvulnerable || damage <= 0 || currentHealth <= 0)
            return;

        ApplyHealthDamage(damage);
        if (hitFeedback != null)
            hitFeedback.Play(hitDirection, hitSpeedRatio);

        StopBattleRoutine();

        if (currentHealth <= 0)
        {
            if (hitReactRoutine != null)
                StopCoroutine(hitReactRoutine);

            hitReactRoutine = StartCoroutine(DefeatAfterHurtRoutine());
            return;
        }

        if (hitReactRoutine != null)
            StopCoroutine(hitReactRoutine);

        hitReactRoutine = StartCoroutine(HitReactRoutine());
    }

    public void ApplyParryStun(float duration)
    {
        if (currentHealth <= 0 || state == BossBattleState.Defeated)
            return;

        StopBattleRoutine();
        if (parryStunRoutine != null)
            StopCoroutine(parryStunRoutine);

        parryStunRoutine = StartCoroutine(ParryStunRoutine(Mathf.Max(0.05f, duration)));
    }

    private Vector2 GetHitDirection(Collider2D hitBox)
    {
        Rigidbody2D attackerRb = hitBox.GetComponentInParent<Rigidbody2D>();
        if (attackerRb != null && attackerRb.linearVelocity.sqrMagnitude > 0.001f)
            return attackerRb.linearVelocity.normalized;

        Vector2 fromAttacker = (Vector2)transform.position - (Vector2)hitBox.transform.position;
        return fromAttacker.sqrMagnitude > 0.001f ? fromAttacker.normalized : Vector2.right;
    }

    private float GetHitSpeedRatio(Collider2D hitBox)
    {
        Rigidbody2D attackerRb = hitBox.GetComponentInParent<Rigidbody2D>();
        if (attackerRb == null)
            return 0f;

        OdmController attackerController = hitBox.GetComponentInParent<OdmController>();
        float referenceSpeed = attackerController != null ? attackerController.maxSpeed : 1f;
        return Mathf.Clamp01(attackerRb.linearVelocity.magnitude / Mathf.Max(referenceSpeed, 0.001f));
    }

    private IEnumerator HitReactRoutine()
    {
        if (parryStunRoutine != null)
        {
            StopCoroutine(parryStunRoutine);
            parryStunRoutine = null;
        }

        state = BossBattleState.HitReact;
        SetBossBool(attackBoolParameterName, false);
        PlayHurtAnimation();
        yield return new WaitForSecondsRealtime(GetHurtAnimationDuration());

        SetVisible(false);
        yield return new WaitForSecondsRealtime(hitDisappearDuration);
        ClearBossHazards();
        RepositionAwayFromPlayer();
        SetVisible(true);
        SetBossBool(hurtBoolParameterName, false);

        if (currentHealth > 0 && state != BossBattleState.Defeated)
            battleRoutine = StartCoroutine(BattleLoop(false));

        hitReactRoutine = null;
    }

    private IEnumerator ParryStunRoutine(float duration)
    {
        state = BossBattleState.Vulnerable;
        SetBossBool(attackBoolParameterName, false);
        SetBossBool(hurtBoolParameterName, false);
        SetTint(parryStunTint);

        yield return new WaitForSecondsRealtime(duration);

        parryStunRoutine = null;
        if (currentHealth > 0 && state != BossBattleState.Defeated && battleRoutine == null && hitReactRoutine == null)
            battleRoutine = StartCoroutine(BattleLoop(false));
    }

    private IEnumerator DefeatAfterHurtRoutine()
    {
        state = BossBattleState.HitReact;
        SetBossBool(attackBoolParameterName, false);
        PlayHurtAnimation();
        yield return new WaitForSecondsRealtime(GetHurtAnimationDuration());

        hitReactRoutine = null;
        Defeat();
    }

    private void RepositionAwayFromPlayer()
    {
        if (bossPositions == null || bossPositions.Length == 0)
            return;

        Vector3 playerPosition = player != null ? player.position : transform.position;
        Transform best = bossPositions[0];
        float bestDistance = -1f;

        for (int i = 0; i < bossPositions.Length; i++)
        {
            if (bossPositions[i] == null)
                continue;

            float distance = Vector2.Distance(bossPositions[i].position, playerPosition);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = bossPositions[i];
            }
        }

        transform.position = best.position;
    }

    private void FindBossPositionsIfNeeded()
    {
        if (bossPositions != null && bossPositions.Length > 0)
            return;

        GameObject root = GameObject.Find("BossPositions");
        if (root == null)
            return;

        bossPositions = new Transform[root.transform.childCount];
        for (int i = 0; i < root.transform.childCount; i++)
            bossPositions[i] = root.transform.GetChild(i);
    }

    private void SpawnProjectile(Vector2 position, Vector2 direction)
    {
        GameObject projectile = new GameObject("BossProjectile");
        projectile.name = "BossProjectile";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * 0.55f;

        AddPrimitiveVisual(projectile.transform, PrimitiveType.Sphere);
        BossProjectile bossProjectile = projectile.AddComponent<BossProjectile>();
        bossProjectile.Initialize(direction, projectileSpeed, projectileLifetime, projectileDamage, projectileKnockback, this);
    }

    private void SpawnTrap(Vector2 position, Vector2 size)
    {
        GameObject trap = new GameObject("BossTrap");
        trap.name = "BossTrap";
        trap.transform.position = position;
        trap.transform.localScale = new Vector3(size.x, size.y, 1f);

        AddPrimitiveVisual(trap.transform, PrimitiveType.Cube);
        BossTrap bossTrap = trap.AddComponent<BossTrap>();
        bossTrap.Initialize(trapWarningDuration, trapActiveDuration, trapDamage, trapKnockback, this);
    }

    private void SpawnHookableAnchor(Vector2 position, float lifetime)
    {
        GameObject anchor = new GameObject("HookableBossAnchor");
        anchor.name = "HookableBossAnchor";
        anchor.transform.position = position;
        anchor.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        anchor.layer = GetLayerFromMask(hookableAnchorLayer);

        AddPrimitiveVisual(anchor.transform, PrimitiveType.Cube);
        anchor.AddComponent<BoxCollider2D>();
        HookableBossAnchor hookableAnchor = anchor.AddComponent<HookableBossAnchor>();
        hookableAnchor.lifetime = lifetime;
    }

    private void AddPrimitiveVisual(Transform parent, PrimitiveType primitiveType)
    {
        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        visual.name = "Visual";
        visual.transform.SetParent(parent, false);

        Collider collider3D = visual.GetComponent<Collider>();
        if (collider3D != null)
            Destroy(collider3D);
    }

    private int GetLayerFromMask(LayerMask layerMask)
    {
        int mask = layerMask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
                return i;
        }

        return 0;
    }

    private void ClearBossHazards()
    {
        BossProjectile[] projectiles = FindObjectsByType<BossProjectile>();
        for (int i = 0; i < projectiles.Length; i++)
            Destroy(projectiles[i].gameObject);

        BossTrap[] traps = FindObjectsByType<BossTrap>();
        for (int i = 0; i < traps.Length; i++)
            Destroy(traps[i].gameObject);

        HookableBossAnchor[] anchors = FindObjectsByType<HookableBossAnchor>();
        for (int i = 0; i < anchors.Length; i++)
            Destroy(anchors[i].gameObject);
    }

    private void Defeat()
    {
        if (state == BossBattleState.Defeated)
            return;

        StopBattleRoutine();
        if (parryStunRoutine != null)
        {
            StopCoroutine(parryStunRoutine);
            parryStunRoutine = null;
        }

        currentHealth = 0;
        state = BossBattleState.Defeated;
        SetBossBool(hurtBoolParameterName, false);
        SetBossBool(attackBoolParameterName, false);
        ClearBossHazards();
        SetVisible(false);

        if (arena != null)
            arena.EndBattle();
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;

        Collider2D hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
            hitCollider.enabled = visible;
    }

    private void SetTint(Color color)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    private void StopBattleRoutine()
    {
        if (battleRoutine == null)
            return;

        StopCoroutine(battleRoutine);
        battleRoutine = null;
    }

    private void PlayHurtAnimation()
    {
        SetBossBool(hurtBoolParameterName, true);
    }

    private float GetHurtAnimationDuration()
    {
        float duration = GetAnimationClipDuration("hurt");
        return duration > 0f ? duration : fallbackHurtAnimationDuration;
    }

    private float GetAnimationClipDuration(string clipName)
    {
        if (bossAnimator == null || bossAnimator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
            return 0f;

        AnimationClip[] clips = bossAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && string.Equals(clips[i].name, clipName, System.StringComparison.OrdinalIgnoreCase))
                return clips[i].length;
        }

        return 0f;
    }

    private void SetBossBool(string parameterName, bool value)
    {
        if (bossAnimator == null || string.IsNullOrEmpty(parameterName) || !HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            return;

        bossAnimator.SetBool(parameterName, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        AnimatorControllerParameter[] parameters = bossAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType && parameters[i].name == parameterName)
                return true;
        }

        return false;
    }
}
