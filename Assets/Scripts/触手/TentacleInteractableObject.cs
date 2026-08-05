  using UnityEngine;

/// <summary>
/// 可被触手抓取和甩出的场景交互物。后续可作为可投掷箱子、石块、机关物体的模板组件。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TentacleInteractableObject : MonoBehaviour
{
    [Header("触手抓取")]
    [Tooltip("物体质量。触手力量大于等于该值时才能稳定抓取；运行时会同步到 Rigidbody2D.mass。")]
    public float objectMass = 1f;

    [Tooltip("物体被触手拖动时的抗拒程度。数值越高，抓取后越拖拽。")]
    public float grabTenacity = 0f;

    [Header("投掷伤害")]
    [Tooltip("被触手甩出后，投掷伤害判定持续的最长时间。")]
    public float throwImpactWindow = 2f;

    [Tooltip("投掷物撞击速度低于该值时，不造成撞击伤害。")]
    public float throwMinImpactSpeed = 5f;

    [Tooltip("投掷撞击的基础伤害。最终伤害会在此基础上叠加速度伤害。")]
    public int throwBaseDamage = 1;

    [Tooltip("超过最低撞击速度的部分，按该倍率转换为额外伤害。")]
    public float throwSpeedDamageScale = 0.25f;

    [Tooltip("投掷物可以造成伤害的目标层级。默认检测全部层级，实际只会伤害 Enemy 或 BossController。")]
    public LayerMask throwDamageLayer = ~0;

    [Tooltip("开启后，投掷物成功造成一次伤害后立刻结束本次投掷伤害窗口；关闭后可在窗口内多次撞击造成伤害。")]
    public bool clearThrowStateAfterDamage = true;

    [Header("运行时调试")]
    [Tooltip("当前是否处于被触手甩出后的撞击判定窗口内，仅用于运行时观察。")]
    public bool isTentacleThrown;

    private Rigidbody2D cachedRigidbody;
    private float thrownExpireTime;
    private Vector2 lastThrowVelocity;

    public float EffectiveMass => Mathf.Max(0.01f, objectMass);

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        ApplyPhysicsStats();
    }

    private void FixedUpdate()
    {
        if (!isTentacleThrown)
            return;

        if (Time.time > thrownExpireTime)
        {
            ClearThrownState();
            return;
        }

        if (cachedRigidbody != null)
            lastThrowVelocity = cachedRigidbody.linearVelocity;
    }

    private void OnValidate()
    {
        objectMass = Mathf.Max(0.01f, objectMass);
        grabTenacity = Mathf.Max(0f, grabTenacity);
        throwImpactWindow = Mathf.Max(0f, throwImpactWindow);
        throwMinImpactSpeed = Mathf.Max(0f, throwMinImpactSpeed);
        throwBaseDamage = Mathf.Max(0, throwBaseDamage);
        throwSpeedDamageScale = Mathf.Max(0f, throwSpeedDamageScale);
        ApplyPhysicsStats();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyThrowImpact(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyThrowImpact(other);
    }

    public void MarkTentacleThrown(Vector2 throwVelocity)
    {
        if (cachedRigidbody == null)
            cachedRigidbody = GetComponent<Rigidbody2D>();

        isTentacleThrown = true;
        lastThrowVelocity = throwVelocity;
        thrownExpireTime = Time.time + Mathf.Max(0f, throwImpactWindow);
    }

    private void ApplyPhysicsStats()
    {
        if (cachedRigidbody == null)
            cachedRigidbody = GetComponent<Rigidbody2D>();

        if (cachedRigidbody != null)
            cachedRigidbody.mass = EffectiveMass;
    }

    private void TryApplyThrowImpact(Collision2D collision)
    {
        if (!isTentacleThrown || collision == null)
            return;

        if (Time.time > thrownExpireTime)
        {
            ClearThrownState();
            return;
        }

        Collider2D hitCollider = collision.collider;
        if (hitCollider == null)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < 0.001f)
            impactSpeed = lastThrowVelocity.magnitude;

        TryApplyThrowImpact(hitCollider, impactSpeed);
    }

    private void TryApplyThrowImpact(Collider2D hitCollider)
    {
        if (!isTentacleThrown || hitCollider == null)
            return;

        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            return;

        if (Time.time > thrownExpireTime)
        {
            ClearThrownState();
            return;
        }

        float impactSpeed = cachedRigidbody != null
            ? Mathf.Max(cachedRigidbody.linearVelocity.magnitude, lastThrowVelocity.magnitude)
            : lastThrowVelocity.magnitude;
        TryApplyThrowImpact(hitCollider, impactSpeed);
    }

    private void TryApplyThrowImpact(Collider2D hitCollider, float impactSpeed)
    {
        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            return;

        int otherLayerMask = 1 << hitCollider.gameObject.layer;
        if ((throwDamageLayer.value & otherLayerMask) == 0)
            return;

        if (impactSpeed < throwMinImpactSpeed)
            return;

        int damage = CalculateThrowImpactDamage(impactSpeed);
        bool dealtDamage = TryDamageTarget(hitCollider, damage, lastThrowVelocity);
        if (dealtDamage && clearThrowStateAfterDamage)
            ClearThrownState();
    }

    private int CalculateThrowImpactDamage(float impactSpeed)
    {
        float extraSpeed = Mathf.Max(0f, impactSpeed - throwMinImpactSpeed);
        int speedDamage = Mathf.FloorToInt(extraSpeed * Mathf.Max(0f, throwSpeedDamageScale));
        return Mathf.Max(1, throwBaseDamage + speedDamage);
    }

    private bool TryDamageTarget(Collider2D hitCollider, int damage, Vector2 impactVelocity)
    {
        Enemy enemy = hitCollider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            return true;
        }

        BossController boss = hitCollider.GetComponentInParent<BossController>();
        if (boss != null)
        {
            Vector2 direction = impactVelocity.sqrMagnitude > 0.001f ? impactVelocity.normalized : Vector2.right;
            boss.TakeDamage(damage, direction, 1f);
            return true;
        }

        return false;
    }

    private void ClearThrownState()
    {
        isTentacleThrown = false;
        lastThrowVelocity = Vector2.zero;
        thrownExpireTime = 0f;
    }
}
