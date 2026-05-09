using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 地面陷阱：先显示预警，再短暂启用击退判定。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossTrap : MonoBehaviour
{
    private BoxCollider2D hitCollider;
    private float warningDuration;
    private float activeDuration;
    private int damage;
    private float knockbackForce;

    /// <summary>
    /// 初始化陷阱预警时间、生效时间和击退参数。
    /// </summary>
    public void Initialize(float warning, float active, int trapDamage, float knockback)
    {
        warningDuration = warning;
        activeDuration = active;
        damage = trapDamage;
        knockbackForce = knockback;
        StartCoroutine(LifetimeRoutine());
    }

    /// <summary>
    /// 配置陷阱触发器，并显示预警颜色。
    /// </summary>
    void Awake()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;
        SetColor(new Color(1f, 0.1f, 0.05f, 0.35f));
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSecondsRealtime(warningDuration);

        hitCollider.enabled = true;
        SetColor(new Color(1f, 0.05f, 0.02f, 0.85f));

        yield return new WaitForSecondsRealtime(activeDuration);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BossHazardUtility.TryKnockBackPlayer(other, transform.position, knockbackForce, damage);
    }

    private void SetColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = color;
    }
}
