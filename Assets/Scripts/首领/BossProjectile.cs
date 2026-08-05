using UnityEngine;

/// <summary>
/// Boss 弹幕：沿指定方向移动，命中玩家时造成击退。
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossProjectile : MonoBehaviour
{
    private CircleCollider2D hitCollider;
    private Vector2 direction;
    private float speed;
    private int damage;
    private float knockbackForce;
    private bool reflected;
    private float ignorePlayerUntil;
    private BossController owner;

    /// <summary>
    /// 初始化弹幕方向、速度、存在时间和击退参数。
    /// </summary>
    public void Initialize(Vector2 moveDirection, float moveSpeed, float lifetime, int projectileDamage, float knockback)
    {
        Initialize(moveDirection, moveSpeed, lifetime, projectileDamage, knockback, null);
    }

    public void Initialize(Vector2 moveDirection, float moveSpeed, float lifetime, int projectileDamage, float knockback, BossController projectileOwner)
    {
        direction = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector2.left;
        speed = moveSpeed;
        damage = projectileDamage;
        knockbackForce = knockback;
        owner = projectileOwner;
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// 配置 2D 触发器、刚体和临时视觉颜色。
    /// </summary>
    /// 
















    void Awake()
    {
        hitCollider = GetComponent<CircleCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.radius = 0.5f;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        SetColor(new Color(1f, 0.25f, 0.15f, 1f));
    }

    void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.unscaledDeltaTime);

        if (TryReflectFromParry())
            return;

        if (BossHazardUtility.TryDamagePlayerCable(hitCollider, transform.position, knockbackForce, damage))
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (reflected && TryHitReflectedTarget(other))
            return;

        if (Time.time < ignorePlayerUntil && other.GetComponentInParent<OdmController>() != null)
            return;

        if (TryReflectFromParry())
            return;

        if (BossHazardUtility.TryKnockBackPlayer(other, transform.position, knockbackForce, damage))
            Destroy(gameObject);
    }

    private bool TryHitReflectedTarget(Collider2D other)
    {
        BossController target = other.GetComponentInParent<BossController>();
        if (target == null)
            return false;

        int reflectedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * GetReflectedDamageMultiplier(target)));
        Vector2 hitDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        target.TakeDamage(reflectedDamage, hitDirection, 1f);
        Destroy(gameObject);
        return true;
    }

    private float GetReflectedDamageMultiplier(BossController target)
    {
        BossController source = owner != null ? owner : target;
        return Mathf.Max(0.01f, source.reflectedProjectileDamageMultiplier);
    }

    private bool TryReflectFromParry()
    {
        if (reflected)
            return false;

        if (!BossHazardUtility.TryParryByPlayer(hitCollider, transform.position, damage, true, out OdmController controller))
            return false;

        Vector2 reflectDirection = ((Vector2)transform.position - (Vector2)controller.transform.position).normalized;
        if (reflectDirection.sqrMagnitude < 0.001f)
            reflectDirection = -direction;

        direction = reflectDirection;
        reflected = true;
        ignorePlayerUntil = Time.time + 0.2f;
        if (owner != null)
            owner.ApplyParryStun(owner.parryStunDuration);

        SetColor(new Color(0.35f, 0.9f, 1f, 1f));
        return true;
    }

    private void SetColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = color;
    }
}
