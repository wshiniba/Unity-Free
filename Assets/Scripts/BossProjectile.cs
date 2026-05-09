using UnityEngine;

/// <summary>
/// Boss 弹幕：沿指定方向移动，命中玩家时造成击退。
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private int damage;
    private float knockbackForce;

    /// <summary>
    /// 初始化弹幕方向、速度、存在时间和击退参数。
    /// </summary>
    public void Initialize(Vector2 moveDirection, float moveSpeed, float lifetime, int projectileDamage, float knockback)
    {
        direction = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector2.left;
        speed = moveSpeed;
        damage = projectileDamage;
        knockbackForce = knockback;
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// 配置 2D 触发器、刚体和临时视觉颜色。
    /// </summary>
    /// 
















    void Awake()
    {
        CircleCollider2D hitCollider = GetComponent<CircleCollider2D>();
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
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (BossHazardUtility.TryKnockBackPlayer(other, transform.position, knockbackForce, damage))
            Destroy(gameObject);
    }

    private void SetColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = color;
    }
}
