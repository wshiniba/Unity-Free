using System;
using UnityEngine;

/// <summary>
/// 玩家基础生命值。后续 UI、存档和养成系统可以从这里读取或扩展。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("基础生命值")]
    [Tooltip("玩家最大生命值。")]
    public int maxHealth = 10;

    [Tooltip("玩家当前生命值，仅用于运行时调试观察。")]
    public int currentHealth;

    [Header("受击保护")]
    [Tooltip("玩家受伤后的无敌时间，避免同一个持续判定每帧扣血。")]
    public float invulnerableDuration = 0.35f;

    public event Action<int> DamageTaken;
    public event Action Died;

    private float invulnerableUntil;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        invulnerableDuration = Mathf.Max(0f, invulnerableDuration);
    }

    public bool TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0 || Time.time < invulnerableUntil)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        invulnerableUntil = Time.time + Mathf.Max(0f, invulnerableDuration);
        DamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
            Died?.Invoke();

        return true;
    }

    public float GetHealthRatio()
    {
        return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    }
}
