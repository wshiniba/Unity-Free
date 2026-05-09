using System.Collections;
using UnityEngine;

/// <summary>
/// 根据玩家当前朝向短暂启用对应攻击判定框。
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("朝右攻击时启用的 HitBox。")]
    public GameObject hitBoxRight;

    [Tooltip("朝左攻击时启用的 HitBox。")]
    public GameObject hitBoxLeft;

    [Tooltip("朝上攻击时启用的 HitBox。")]
    public GameObject hitBoxUp;

    [Tooltip("朝下攻击时启用的 HitBox。")]
    public GameObject hitBoxDown;

    [Tooltip("用于判断玩家当前朝向的 SpriteRenderer。默认 Sprite 朝向应为右。")]
    public SpriteRenderer characterRenderer;

    [Tooltip("玩家本体 Animator。攻击开始时会触发 isAttack。未指定时自动查找当前物体上的 Animator。")]
    public Animator characterAnimator;

    [Tooltip("玩家攻击动画 Trigger 参数名。")]
    public string attackTriggerName = "isAttack";

    [Tooltip("攻击判定框保持启用的时间。")]
    public float attackActiveTime = 0.15f;

    [Tooltip("开启后，HitBox 启用时长会优先使用该 HitBox 攻击动画的持续时间。没有动画时使用 attackActiveTime。")]
    public bool useHitBoxAnimationDuration = true;

    [Tooltip("两次攻击之间的最短间隔。")]
    public float attackCooldown = 0.35f;

    [Tooltip("空中攻击时，速度小于这个值则回退到玩家朝向。")]
    public float airborneDirectionMinSpeed = 0.1f;

    [Header("速度伤害")]
    [Tooltip("攻击基础伤害。后续接入武器系统时，这个值可以由武器提供。")]
    public int baseDamage = 1;

    [Tooltip("达到参考速度时获得的最大额外伤害倍率。2 表示满速时额外 +200%，总伤害为基础伤害的 3 倍。")]
    public float maxSpeedDamageBonus = 2f;

    [Tooltip("速度增伤曲线指数。大于 1 会让低速增伤更克制，高速增伤更明显。")]
    public float speedDamageExponent = 1.5f;

    [Tooltip("伤害计算使用的参考速度。小于等于 0 时自动使用 OdmController.maxSpeed。")]
    public float damageReferenceSpeed = 0f;

    [Header("调试")]
    [Tooltip("开启后每帧输出当前速度、速度增伤倍率和当前预测伤害。")]
    public bool logDamageDebugEveryFrame = false;

    public int CurrentAttackDamage { get; private set; } = 1;

    private Rigidbody2D rb;
    private OdmController controller;
    private bool canAttack = true;
    private Coroutine attackRoutine;

    void Awake()
    {
        if (characterRenderer == null) characterRenderer = GetComponent<SpriteRenderer>();
        if (characterAnimator == null) characterAnimator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<OdmController>();

        if (hitBoxRight == null)
        {
            Transform right = transform.Find("HitBox_Right");
            if (right != null) hitBoxRight = right.gameObject;
        }

        if (hitBoxLeft == null)
        {
            Transform left = transform.Find("HitBox_Left");
            if (left != null) hitBoxLeft = left.gameObject;
        }

        if (hitBoxUp == null)
        {
            Transform up = transform.Find("HitBox_Up");
            if (up != null) hitBoxUp = up.gameObject;
        }

        if (hitBoxDown == null)
        {
            Transform down = transform.Find("HitBox_Down");
            if (down != null) hitBoxDown = down.gameObject;
        }

        ApplyHitBoxTag(hitBoxRight);
        ApplyHitBoxTag(hitBoxLeft);
        ApplyHitBoxTag(hitBoxUp);
        ApplyHitBoxTag(hitBoxDown);

        SetHitBoxesActive(false);
    }

    void Update()
    {
        if (logDamageDebugEveryFrame)
            LogDamageDebug();

        if (Input.GetKeyDown(KeyCode.F))
            TryAttack();
    }

    private void TryAttack()
    {
        if (!canAttack) return;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        SetHitBoxesActive(false);
        CurrentAttackDamage = CalculateDamageFromSpeed();
        TriggerCharacterAttackAnimation();

        GameObject activeHitBox = GetAttackHitBox();
        if (activeHitBox != null)
        {
            activeHitBox.SetActive(true);
            RestartHitBoxAnimation(activeHitBox);
        }

        float activeDuration = GetHitBoxActiveDuration(activeHitBox);
        yield return new WaitForSeconds(activeDuration);

        SetHitBoxesActive(false);

        float remainingCooldown = Mathf.Max(0f, attackCooldown - activeDuration);
        if (remainingCooldown > 0f)
            yield return new WaitForSeconds(remainingCooldown);

        canAttack = true;
        attackRoutine = null;
    }

    private bool IsFacingLeft()
    {
        return characterRenderer != null && characterRenderer.flipX;
    }

    private GameObject GetAttackHitBox()
    {
        if (controller != null && !controller.IsGrounded)
        {
            GameObject velocityHitBox = GetAirborneVelocityHitBox();
            if (velocityHitBox != null)
                return velocityHitBox;
        }

        return IsFacingLeft() ? hitBoxLeft : hitBoxRight;
    }

    private GameObject GetAirborneVelocityHitBox()
    {
        if (rb == null) return null;

        Vector2 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < airborneDirectionMinSpeed * airborneDirectionMinSpeed)
            return null;

        if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y))
            return velocity.x < 0f ? hitBoxLeft : hitBoxRight;

        return velocity.y < 0f ? hitBoxDown : hitBoxUp;
    }

    private void SetHitBoxesActive(bool active)
    {
        if (hitBoxRight != null) hitBoxRight.SetActive(active);
        if (hitBoxLeft != null) hitBoxLeft.SetActive(active);
        if (hitBoxUp != null) hitBoxUp.SetActive(active);
        if (hitBoxDown != null) hitBoxDown.SetActive(active);
    }

    private void ApplyHitBoxTag(GameObject hitBox)
    {
        if (hitBox != null)
            hitBox.tag = "PlayerHitBox";
    }

    private void TriggerCharacterAttackAnimation()
    {
        if (characterAnimator == null || string.IsNullOrEmpty(attackTriggerName))
            return;

        characterAnimator.ResetTrigger(attackTriggerName);
        characterAnimator.SetTrigger(attackTriggerName);
    }

    private void RestartHitBoxAnimation(GameObject hitBox)
    {
        if (hitBox == null) return;

        Animator animator = hitBox.GetComponent<Animator>();
        if (animator == null) animator = hitBox.GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.Play(0, 0, 0f);
        animator.Update(0f);
    }

    private float GetHitBoxActiveDuration(GameObject hitBox)
    {
        if (!useHitBoxAnimationDuration || hitBox == null)
            return attackActiveTime;

        float animationDuration = GetAnimatorDuration(hitBox);
        if (animationDuration <= 0f)
            animationDuration = GetLegacyAnimationDuration(hitBox);

        return animationDuration > 0f ? animationDuration : attackActiveTime;
    }

    private float GetAnimatorDuration(GameObject hitBox)
    {
        Animator animator = hitBox.GetComponent<Animator>();
        if (animator == null) animator = hitBox.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        float duration = 0f;
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                duration = Mathf.Max(duration, clips[i].length);
        }

        return duration;
    }

    private float GetLegacyAnimationDuration(GameObject hitBox)
    {
        Animation animation = hitBox.GetComponent<Animation>();
        if (animation == null) animation = hitBox.GetComponentInChildren<Animation>();
        if (animation == null || animation.clip == null)
            return 0f;

        return animation.clip.length;
    }

    private int CalculateDamageFromSpeed()
    {
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float referenceSpeed = GetDamageReferenceSpeed();

        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(referenceSpeed, 0.001f));
        float speedMultiplier = 1f + maxSpeedDamageBonus * Mathf.Pow(speedRatio, speedDamageExponent);
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * speedMultiplier));
    }

    private void LogDamageDebug()
    {
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float referenceSpeed = GetDamageReferenceSpeed();
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(referenceSpeed, 0.001f));
        float speedMultiplier = 1f + maxSpeedDamageBonus * Mathf.Pow(speedRatio, speedDamageExponent);
        int predictedDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * speedMultiplier));

        Debug.Log($"[PlayerAttack] speed={speed:F2}, speedRatio={speedRatio:F2}, damageMultiplier={speedMultiplier:F2}, predictedDamage={predictedDamage}, currentAttackDamage={CurrentAttackDamage}");
    }

    private float GetDamageReferenceSpeed()
    {
        return damageReferenceSpeed > 0f
            ? damageReferenceSpeed
            : controller != null ? controller.maxSpeed : 1f;
    }
}
