using System;
using UnityEngine;

/// <summary>
/// 战斗单位通用基础属性。
/// 小怪、Boss、精英怪等只要需要生命值、配置 ID 和基础受伤事件，都可以继承这个脚本。
/// </summary>
public abstract class CombatEntityBase : MonoBehaviour
{
    [Header("配置表接入")]
    [Tooltip("单位配置表中的唯一 ID。后续从 Excel/CSV 导入时，用这个 ID 找到对应属性行。")]
    public string configId = "combat_entity";

    [Header("基础属性")]
    [Tooltip("单位最大生命值。后续配置表导入会覆盖这个值。")]
    public int maxHealth = 3;

    [Tooltip("单位接触玩家攻击判定框时使用的默认受击伤害。没有攻击来源时使用这个值。")]
    public int hitBoxDamage = 1;

    [Tooltip("单位当前生命值，仅用于运行时调试观察。")]
    public int currentHealth;

    public event Action<int> DamageTaken;
    public event Action Died;

    protected virtual void Awake()
    {
        InitializeHealth();
    }

    protected virtual void OnValidate()
    {
        ValidateBaseStats();
    }

    public virtual float GetHealthRatio()
    {
        return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    }

    protected void InitializeHealth()
    {
        ValidateBaseStats();
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    protected void ValidateBaseStats()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        hitBoxDamage = Mathf.Max(0, hitBoxDamage);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    protected bool ApplyHealthDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        DamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
            Died?.Invoke();

        return true;
    }
}
