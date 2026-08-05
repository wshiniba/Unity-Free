using UnityEngine;
using System;

/// <summary>
/// 玩家触手成长属性入口。用于把力量、韧性、长度等可升级数值集中计算后同步到 OdmController。
/// 后续接入存档、Excel/CSV 配置表或升级 UI 时，优先写入这个组件，再调用 ApplyToController。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OdmController))]
public class TentacleProgression : MonoBehaviour
{
    [Header("目标控制器")]
    [Tooltip("要同步成长属性的 ODM 控制器。未指定时自动使用当前物体上的 OdmController。")]
    public OdmController controller;

    [Tooltip("开启后，Awake 时会把当前成长属性同步到 OdmController。")]
    public bool applyOnAwake = true;

    [Header("力量成长")]
    [Tooltip("触手力量等级。影响能否抓取敌人或场景物体。")]
    public int powerLevel = 0;

    [Tooltip("0 级时的触手力量。")]
    public float basePower = 2f;

    [Tooltip("每提升 1 级增加的触手力量。")]
    public float powerPerLevel = 0.5f;

    [Header("韧性成长")]
    [Tooltip("触手韧性等级。影响可弹反攻击的攻击力上限。")]
    public int toughnessLevel = 0;

    [Tooltip("0 级时的触手韧性。")]
    public float baseToughness = 2f;

    [Tooltip("每提升 1 级增加的触手韧性。")]
    public float toughnessPerLevel = 0.5f;

    [Header("长度成长")]
    [Tooltip("触手长度等级。影响触手最大伸出距离和处决搜索距离。")]
    public int lengthLevel = 0;

    [Tooltip("0 级时的触手最大长度。")]
    public float baseMaxLength = 40f;

    [Tooltip("每提升 1 级增加的触手最大长度。")]
    public float lengthPerLevel = 2f;

    [Header("判定成长")]
    [Tooltip("触手抽击命中半径等级。影响触手扫过敌人时的命中宽度。")]
    public int hitRadiusLevel = 0;

    [Tooltip("0 级时的触手抽击命中半径。")]
    public float baseHitRadius = 0.18f;

    [Tooltip("每提升 1 级增加的触手抽击命中半径。")]
    public float hitRadiusPerLevel = 0.02f;

    [Tooltip("触手受击半径等级。影响敌方攻击碰到触手时的判定宽度。")]
    public int hurtRadiusLevel = 0;

    [Tooltip("0 级时的触手受击半径。")]
    public float baseHurtRadius = 0.18f;

    [Tooltip("每提升 1 级增加的触手受击半径。")]
    public float hurtRadiusPerLevel = 0.02f;

    [Header("运行时观察")]
    [Tooltip("当前计算出的最终触手力量，仅用于运行时观察。")]
    public float currentPower;

    [Tooltip("当前计算出的最终触手韧性，仅用于运行时观察。")]
    public float currentToughness;

    [Tooltip("当前计算出的最终触手最大长度，仅用于运行时观察。")]
    public float currentMaxLength;

    public event Action ProgressionChanged;

    private void Awake()
    {
        CacheController();
        Recalculate();

        if (applyOnAwake)
            ApplyToController();
    }

    private void OnValidate()
    {
        powerLevel = Mathf.Max(0, powerLevel);
        toughnessLevel = Mathf.Max(0, toughnessLevel);
        lengthLevel = Mathf.Max(0, lengthLevel);
        hitRadiusLevel = Mathf.Max(0, hitRadiusLevel);
        hurtRadiusLevel = Mathf.Max(0, hurtRadiusLevel);

        basePower = Mathf.Max(0.01f, basePower);
        powerPerLevel = Mathf.Max(0f, powerPerLevel);
        baseToughness = Mathf.Max(0f, baseToughness);
        toughnessPerLevel = Mathf.Max(0f, toughnessPerLevel);
        baseMaxLength = Mathf.Max(0.1f, baseMaxLength);
        lengthPerLevel = Mathf.Max(0f, lengthPerLevel);
        baseHitRadius = Mathf.Max(0.01f, baseHitRadius);
        hitRadiusPerLevel = Mathf.Max(0f, hitRadiusPerLevel);
        baseHurtRadius = Mathf.Max(0.01f, baseHurtRadius);
        hurtRadiusPerLevel = Mathf.Max(0f, hurtRadiusPerLevel);

        Recalculate();
    }

    [ContextMenu("同步触手成长属性到 OdmController")]
    public void ApplyToController()
    {
        CacheController();
        Recalculate();

        if (controller == null)
            return;

        controller.tentaclePower = currentPower;
        controller.cableToughness = currentToughness;
        controller.maxCableLength = currentMaxLength;
        controller.tentacleHitRadius = CalculateHitRadius();
        controller.cableHurtRadius = CalculateHurtRadius();

        ProgressionChanged?.Invoke();
    }

    public void ApplyProgressionConfig(
        int newPowerLevel,
        int newToughnessLevel,
        int newLengthLevel,
        int newHitRadiusLevel,
        int newHurtRadiusLevel)
    {
        powerLevel = Mathf.Max(0, newPowerLevel);
        toughnessLevel = Mathf.Max(0, newToughnessLevel);
        lengthLevel = Mathf.Max(0, newLengthLevel);
        hitRadiusLevel = Mathf.Max(0, newHitRadiusLevel);
        hurtRadiusLevel = Mathf.Max(0, newHurtRadiusLevel);
        ApplyToController();
    }

    public void UpgradePower(int levels = 1)
    {
        powerLevel = Mathf.Max(0, powerLevel + Mathf.Max(1, levels));
        ApplyToController();
    }

    public void UpgradeToughness(int levels = 1)
    {
        toughnessLevel = Mathf.Max(0, toughnessLevel + Mathf.Max(1, levels));
        ApplyToController();
    }

    public void UpgradeLength(int levels = 1)
    {
        lengthLevel = Mathf.Max(0, lengthLevel + Mathf.Max(1, levels));
        ApplyToController();
    }

    public void UpgradeHitRadius(int levels = 1)
    {
        hitRadiusLevel = Mathf.Max(0, hitRadiusLevel + Mathf.Max(1, levels));
        ApplyToController();
    }

    public void UpgradeHurtRadius(int levels = 1)
    {
        hurtRadiusLevel = Mathf.Max(0, hurtRadiusLevel + Mathf.Max(1, levels));
        ApplyToController();
    }

    private void CacheController()
    {
        if (controller == null)
            controller = GetComponent<OdmController>();
    }

    private void Recalculate()
    {
        currentPower = basePower + powerLevel * powerPerLevel;
        currentToughness = baseToughness + toughnessLevel * toughnessPerLevel;
        currentMaxLength = baseMaxLength + lengthLevel * lengthPerLevel;
    }

    private float CalculateHitRadius()
    {
        return baseHitRadius + hitRadiusLevel * hitRadiusPerLevel;
    }

    private float CalculateHurtRadius()
    {
        return baseHurtRadius + hurtRadiusLevel * hurtRadiusPerLevel;
    }
}
