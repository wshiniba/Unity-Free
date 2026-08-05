using UnityEngine;

/// <summary>
/// 小怪简易追踪和近战攻击模板。
/// 只负责测试用基础行为，正式敌人 AI 可以在此基础上拆成状态机或行为树。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemySimpleAI : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("要追踪和攻击的玩家。为空时会自动查找场景中的 OdmController。")]
    public OdmController targetPlayer;

    [Tooltip("开启后，目标为空时会自动在场景中查找玩家。")]
    public bool autoFindPlayer = true;

    [Header("移动")]
    [Tooltip("玩家进入该距离后，小怪开始追踪。")]
    public float detectRange = 12f;

    [Tooltip("玩家未进入检测范围时，小怪是否在出生点附近来回巡逻。")]
    public bool enablePatrol = true;

    [Tooltip("小怪待机巡逻时，以出生点为中心左右移动的半径。")]
    public float patrolRadius = 2.5f;

    [Tooltip("小怪待机巡逻时的移动速度。")]
    public float patrolSpeed = 1.6f;

    [Tooltip("小怪到达巡逻边界后停顿多久再掉头。")]
    public float patrolPauseDuration = 0.35f;

    [Tooltip("小怪靠近玩家时的移动速度。")]
    public float moveSpeed = 3.5f;

    [Tooltip("小怪水平移动的加速度。数值越高越快达到目标速度。")]
    public float acceleration = 18f;

    [Tooltip("距离玩家小于该值时，小怪停止继续贴近，避免和玩家完全重叠。")]
    public float stopDistance = 1.15f;

    [Tooltip("开启后，小怪会根据移动方向翻转 SpriteRenderer。")]
    public bool flipSpriteByDirection = true;

    [Header("受击硬直")]
    [Tooltip("开启后，小怪受到伤害时会短暂停止巡逻和追踪。")]
    public bool enableHitStun = true;

    [Tooltip("小怪受到伤害后的基础硬直时间。")]
    public float hitStunDuration = 0.18f;

    [Tooltip("单次伤害每 1 点额外增加的硬直时间。")]
    public float hitStunDurationPerDamage = 0.02f;

    [Tooltip("小怪受击硬直的最大时长，避免高伤害导致停顿过久。")]
    public float maxHitStunDuration = 0.45f;

    [Header("地形检测")]
    [Tooltip("开启后，小怪巡逻和追踪时会检测前方地面，避免主动走下平台边缘。")]
    public bool avoidLedges = true;

    [Tooltip("小怪地面和墙体检测使用的层级。通常应包含地面、墙体等实体层。")]
    public LayerMask obstacleLayer = ~0;

    [Tooltip("前方地面检测点相对小怪中心的偏移。X 表示向前探测距离，Y 表示从碰撞体底部上方多少位置开始向下检测。")]
    public Vector2 groundProbeOffset = new Vector2(0.45f, 0.08f);

    [Tooltip("前方地面检测向下射线的长度。数值需要略大于脚底到地面的间隙。")]
    public float groundProbeDistance = 0.35f;

    [Tooltip("前方墙体检测射线相对小怪中心的高度。")]
    public float wallProbeHeight = 0.15f;

    [Tooltip("前方墙体检测射线的长度。")]
    public float wallProbeDistance = 0.18f;

    [Header("近战攻击")]
    [Tooltip("开启后，小怪进入攻击范围会执行当前临时近战攻击。关闭后只保留检测玩家后的追踪功能。")]
    public bool enableMeleeAttack = false;

    [Tooltip("玩家进入该距离后，小怪可以发动近战攻击。")]
    public float attackRange = 1.45f;

    [Tooltip("两次攻击之间的冷却时间。")]
    public float attackCooldown = 1.1f;

    [Tooltip("攻击起手时间。起手结束时才会真正结算攻击判定。")]
    public float attackWindup = 0.22f;

    [Tooltip("近战攻击造成的伤害。")]
    public int attackDamage = 1;

    [Tooltip("近战攻击命中玩家时施加的击退力度。")]
    public float attackKnockback = 4f;

    [Tooltip("近战攻击的攻击力。玩家触手韧性大于等于该值时才可以弹反。")]
    public int attackPower = 1;

    [Tooltip("开启后，该近战攻击可以被玩家触手弹反。")]
    public bool canBeParried = true;

    [Tooltip("近战攻击判定框大小。X 控制前后宽度，Y 控制上下高度。")]
    public Vector2 attackBoxSize = new Vector2(1.2f, 1.1f);

    [Tooltip("近战攻击判定框相对小怪中心的偏移。X 会根据朝向自动镜像。")]
    public Vector2 attackBoxOffset = new Vector2(0.75f, 0.05f);

    [Tooltip("近战攻击会检测的层级。默认检测全部层级，实际只会对玩家或触手生效。")]
    public LayerMask attackLayer = ~0;

    [Header("弹反反馈")]
    [Tooltip("攻击被弹反后，小怪暂停行动的时间。")]
    public float parriedStunDuration = 0.45f;

    [Tooltip("攻击被弹反后，小怪受到的后退速度。")]
    public float parriedKnockbackSpeed = 4f;

    [Header("调试")]
    [Tooltip("选中小怪时，在 Scene 视图显示检测范围、攻击范围和攻击判定框。")]
    public bool showGizmos = true;

    private Enemy enemy;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Collider2D attackProbe;
    private GameObject attackProbeObject;
    private float nextAttackTime;
    private float attackResolveTime;
    private float stunnedUntil;
    private bool attackPending;
    private bool facingLeft;
    private Vector2 spawnPosition;
    private int patrolDirection = 1;
    private float patrolPauseUntil;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        spawnPosition = transform.position;
        EnsureAttackProbe();
    }

    private void OnEnable()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (enemy != null)
            enemy.DamageTaken += OnEnemyDamageTaken;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.DamageTaken -= OnEnemyDamageTaken;
    }

    private void Update()
    {
        if (targetPlayer == null && autoFindPlayer)
            targetPlayer = FindAnyObjectByType<OdmController>();

        if (attackPending && Time.time >= attackResolveTime)
            ResolveAttack();
    }

    private void FixedUpdate()
    {
        if (targetPlayer == null || body == null || enemy == null || enemy.currentHealth <= 0)
            return;

        if (Time.time < stunnedUntil)
        {
            SlowHorizontalMovement();
            return;
        }

        Vector2 toPlayer = (Vector2)targetPlayer.transform.position - body.position;
        float distance = toPlayer.magnitude;
        UpdateFacing(toPlayer.x);

        if (enableMeleeAttack && !attackPending && distance <= attackRange && Time.time >= nextAttackTime)
        {
            BeginAttack();
            return;
        }

        if (attackPending || distance <= stopDistance)
        {
            SlowHorizontalMovement();
            return;
        }

        if (distance > detectRange)
        {
            ApplyPatrolMovement();
            return;
        }

        float direction = Mathf.Sign(toPlayer.x);
        if (!CanMoveHorizontally(direction))
        {
            SlowHorizontalMovement();
            return;
        }

        float targetSpeed = direction * Mathf.Max(0f, moveSpeed);
        float nextX = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);
    }

    private void BeginAttack()
    {
        attackPending = true;
        attackResolveTime = Time.time + Mathf.Max(0f, attackWindup);
        nextAttackTime = Time.time + Mathf.Max(0f, attackCooldown);
        SlowHorizontalMovement();
    }

    private void ResolveAttack()
    {
        attackPending = false;
        if (targetPlayer == null || attackProbe == null)
            return;

        PositionAttackProbe();

        if (BossHazardUtility.TryParryByPlayer(attackProbe, transform.position, Mathf.Max(0, attackPower), canBeParried, out OdmController parryController))
        {
            ApplyParriedFeedback(parryController);
            return;
        }

        Collider2D playerCollider = targetPlayer.GetComponent<Collider2D>();
        if (playerCollider != null && attackProbe.IsTouching(playerCollider))
            targetPlayer.GetDamage(Mathf.Max(0, attackDamage), transform.position, Mathf.Max(0f, attackKnockback));
        else
            BossHazardUtility.TryDamagePlayerCable(attackProbe, transform.position, Mathf.Max(0f, attackKnockback), Mathf.Max(0, attackDamage));
    }

    private void ApplyParriedFeedback(OdmController parryController)
    {
        stunnedUntil = Time.time + Mathf.Max(0f, parriedStunDuration);
        if (body == null)
            return;

        Vector2 away = parryController != null
            ? (Vector2)(transform.position - parryController.transform.position)
            : (facingLeft ? Vector2.right : Vector2.left);

        if (away.sqrMagnitude < 0.0001f)
            away = facingLeft ? Vector2.right : Vector2.left;

        body.linearVelocity = away.normalized * Mathf.Max(0f, parriedKnockbackSpeed);
    }

    private void OnEnemyDamageTaken(int damage)
    {
        if (!enableHitStun || damage <= 0)
            return;

        float duration = Mathf.Max(0f, hitStunDuration)
            + Mathf.Max(0f, hitStunDurationPerDamage) * damage;
        if (maxHitStunDuration > 0f)
            duration = Mathf.Min(duration, maxHitStunDuration);

        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        attackPending = false;
    }

    private void SlowHorizontalMovement()
    {
        if (body == null)
            return;

        float nextX = Mathf.MoveTowards(body.linearVelocity.x, 0f, Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);
    }

    private void ApplyPatrolMovement()
    {
        if (!enablePatrol || patrolRadius <= 0f || patrolSpeed <= 0f)
        {
            SlowHorizontalMovement();
            return;
        }

        if (Time.time < patrolPauseUntil)
        {
            SlowHorizontalMovement();
            return;
        }

        float offsetFromSpawn = body.position.x - spawnPosition.x;
        if (offsetFromSpawn >= patrolRadius)
        {
            patrolDirection = -1;
            patrolPauseUntil = Time.time + Mathf.Max(0f, patrolPauseDuration);
        }
        else if (offsetFromSpawn <= -patrolRadius)
        {
            patrolDirection = 1;
            patrolPauseUntil = Time.time + Mathf.Max(0f, patrolPauseDuration);
        }

        if (!CanMoveHorizontally(patrolDirection))
        {
            patrolDirection *= -1;
            patrolPauseUntil = Time.time + Mathf.Max(0f, patrolPauseDuration);
            SlowHorizontalMovement();
            return;
        }

        float targetSpeed = patrolDirection * Mathf.Max(0f, patrolSpeed);
        float nextX = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);
        UpdateFacing(patrolDirection);
    }

    private bool CanMoveHorizontally(float direction)
    {
        if (Mathf.Abs(direction) <= 0.001f)
            return false;

        float side = Mathf.Sign(direction);
        if (IsWallAhead(side))
            return false;

        if (avoidLedges && !HasGroundAhead(side))
            return false;

        return true;
    }

    private bool HasGroundAhead(float direction)
    {
        if (body == null)
            return true;

        Bounds bounds = GetBodyBounds();
        float side = Mathf.Sign(direction);
        Vector2 origin = new Vector2(
            bounds.center.x + Mathf.Abs(groundProbeOffset.x) * side,
            bounds.min.y + Mathf.Max(0f, groundProbeOffset.y));

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, Mathf.Max(0.01f, groundProbeDistance), obstacleLayer);
        return HasValidObstacleHit(hits);
    }

    private bool IsWallAhead(float direction)
    {
        if (body == null || wallProbeDistance <= 0f)
            return false;

        Bounds bounds = GetBodyBounds();
        float side = Mathf.Sign(direction);
        Vector2 origin = new Vector2(
            bounds.center.x + bounds.extents.x * side,
            bounds.center.y + wallProbeHeight);

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * side, Mathf.Max(0.01f, wallProbeDistance), obstacleLayer);
        return HasValidObstacleHit(hits);
    }

    private bool HasValidObstacleHit(RaycastHit2D[] hits)
    {
        if (hits == null)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger)
                continue;

            if (hitCollider.attachedRigidbody == body || hitCollider.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    private Bounds GetBodyBounds()
    {
        Collider2D bodyCollider = GetComponent<Collider2D>();
        return bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
    }

    private void UpdateFacing(float horizontalDelta)
    {
        if (Mathf.Abs(horizontalDelta) <= 0.01f)
            return;

        facingLeft = horizontalDelta < 0f;
        if (flipSpriteByDirection && spriteRenderer != null)
            spriteRenderer.flipX = facingLeft;
    }

    private void EnsureAttackProbe()
    {
        if (attackProbe != null)
            return;

        attackProbeObject = new GameObject("Enemy_AttackProbe");
        attackProbeObject.transform.SetParent(transform, false);

        BoxCollider2D box = attackProbeObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        attackProbe = box;

        Rigidbody2D probeBody = attackProbeObject.AddComponent<Rigidbody2D>();
        probeBody.bodyType = RigidbodyType2D.Kinematic;
        probeBody.simulated = true;
    }

    private void PositionAttackProbe()
    {
        EnsureAttackProbe();
        if (attackProbeObject == null || attackProbe == null)
            return;

        float side = facingLeft ? -1f : 1f;
        attackProbeObject.transform.position = (Vector2)transform.position + new Vector2(Mathf.Abs(attackBoxOffset.x) * side, attackBoxOffset.y);

        if (attackProbe is BoxCollider2D box)
        {
            box.size = new Vector2(Mathf.Max(0.01f, attackBoxSize.x), Mathf.Max(0.01f, attackBoxSize.y));
            box.offset = Vector2.zero;
        }
    }

    private void OnValidate()
    {
        detectRange = Mathf.Max(0f, detectRange);
        patrolRadius = Mathf.Max(0f, patrolRadius);
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        patrolPauseDuration = Mathf.Max(0f, patrolPauseDuration);
        groundProbeOffset = new Vector2(Mathf.Max(0f, groundProbeOffset.x), groundProbeOffset.y);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        wallProbeDistance = Mathf.Max(0f, wallProbeDistance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        stopDistance = Mathf.Max(0f, stopDistance);
        hitStunDuration = Mathf.Max(0f, hitStunDuration);
        hitStunDurationPerDamage = Mathf.Max(0f, hitStunDurationPerDamage);
        maxHitStunDuration = Mathf.Max(0f, maxHitStunDuration);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackDamage = Mathf.Max(0, attackDamage);
        attackKnockback = Mathf.Max(0f, attackKnockback);
        attackPower = Mathf.Max(0, attackPower);
        attackBoxSize = new Vector2(Mathf.Max(0.01f, attackBoxSize.x), Mathf.Max(0.01f, attackBoxSize.y));
        parriedStunDuration = Mathf.Max(0f, parriedStunDuration);
        parriedKnockbackSpeed = Mathf.Max(0f, parriedKnockbackSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.65f);
        Vector3 patrolCenter = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawLine(
            patrolCenter + Vector3.left * patrolRadius,
            patrolCenter + Vector3.right * patrolRadius);

        DrawMovementProbeGizmos();

        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        float side = facingLeft ? -1f : 1f;
        Vector3 center = transform.position + new Vector3(Mathf.Abs(attackBoxOffset.x) * side, attackBoxOffset.y, 0f);
        Gizmos.DrawWireCube(center, attackBoxSize);
    }

    private void DrawMovementProbeGizmos()
    {
        Bounds bounds = GetBodyBounds();
        float side = facingLeft ? -1f : 1f;
        Vector3 groundOrigin = new Vector3(
            bounds.center.x + Mathf.Abs(groundProbeOffset.x) * side,
            bounds.min.y + Mathf.Max(0f, groundProbeOffset.y),
            transform.position.z);

        Gizmos.color = new Color(0.15f, 1f, 0.45f, 0.85f);
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector3.down * Mathf.Max(0.01f, groundProbeDistance));

        Vector3 wallOrigin = new Vector3(
            bounds.center.x + bounds.extents.x * side,
            bounds.center.y + wallProbeHeight,
            transform.position.z);
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.right * side * Mathf.Max(0.01f, wallProbeDistance));
    }
}
