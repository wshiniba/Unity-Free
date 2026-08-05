using System;
using UnityEngine;

public enum TentacleUpgradeType
{
    Power,
    Toughness,
    Length,
    HitRadius,
    HurtRadius
}

/// <summary>
/// 临时触手升级输入入口。
/// 后续接正式升级 UI 时，可以复用 TryUpgrade，把按键输入替换成按钮调用。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(TentacleProgression))]
public class TentacleUpgradeInput : MonoBehaviour
{
    [Serializable]
    public class UpgradeEntry
    {
        [Tooltip("升级项名称，仅用于 Inspector 和调试日志识别。")]
        public string upgradeName = "触手力量";

        [Tooltip("临时测试升级按键。后续接正式 UI 后可以不再使用。")]
        public KeyCode key = KeyCode.U;

        [Tooltip("这条升级配置要提升的触手属性。")]
        public TentacleUpgradeType upgradeType = TentacleUpgradeType.Power;

        [Tooltip("升级消耗的物品 ID。需要和掉落物、物品表中的 ID 保持一致。")]
        public string costItemId = "special_material";

        [Tooltip("0 级升 1 级时需要消耗的基础数量。")]
        public int baseCost = 1;

        [Tooltip("每升过 1 级，下一次升级额外增加的消耗数量。")]
        public int costPerLevel = 1;

        [Tooltip("每次成功升级时提升的等级数。")]
        public int levelsPerUpgrade = 1;
    }

    [Header("组件引用")]
    [Tooltip("玩家背包。为空时自动读取当前物体上的 PlayerInventory。")]
    public PlayerInventory inventory;

    [Tooltip("触手成长组件。为空时自动读取当前物体上的 TentacleProgression。")]
    public TentacleProgression progression;

    [Header("临时升级配置")]
    [Tooltip("临时按键升级配置。默认 U 键消耗 special_material 升级触手力量。")]
    public UpgradeEntry[] upgrades =
    {
        new UpgradeEntry()
    };

    [Tooltip("升级成功或失败时是否在 Console 输出调试日志。")]
    public bool logUpgradeResult = true;

    private void Awake()
    {
        CacheComponents();
    }

    private void Update()
    {
        if (upgrades == null)
            return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeEntry entry = upgrades[i];
            EnsureEntryDefaults(entry);
            if (entry != null && Input.GetKeyDown(entry.key))
                TryUpgrade(entry);
        }
    }

    public bool TryUpgrade(UpgradeEntry entry)
    {
        CacheComponents();

        if (entry == null || inventory == null || progression == null)
            return false;

        int cost = CalculateCost(entry);
        if (!inventory.TrySpendItem(entry.costItemId, cost))
        {
            if (logUpgradeResult)
                Debug.Log($"升级失败：{entry.upgradeName} 需要 {entry.costItemId} x{cost}", this);
            return false;
        }

        ApplyUpgrade(entry);

        if (logUpgradeResult)
            Debug.Log($"升级成功：{entry.upgradeName} +{Mathf.Max(1, entry.levelsPerUpgrade)}", this);

        return true;
    }

    public int CalculateCost(UpgradeEntry entry)
    {
        if (entry == null)
            return 0;

        int currentLevel = GetCurrentLevel(entry.upgradeType);
        int baseCost = Mathf.Max(1, entry.baseCost);
        int costPerLevel = Mathf.Max(0, entry.costPerLevel);
        return baseCost + currentLevel * costPerLevel;
    }

    private void ApplyUpgrade(UpgradeEntry entry)
    {
        int levels = Mathf.Max(1, entry.levelsPerUpgrade);
        switch (entry.upgradeType)
        {
            case TentacleUpgradeType.Toughness:
                progression.UpgradeToughness(levels);
                break;
            case TentacleUpgradeType.Length:
                progression.UpgradeLength(levels);
                break;
            case TentacleUpgradeType.HitRadius:
                progression.UpgradeHitRadius(levels);
                break;
            case TentacleUpgradeType.HurtRadius:
                progression.UpgradeHurtRadius(levels);
                break;
            case TentacleUpgradeType.Power:
            default:
                progression.UpgradePower(levels);
                break;
        }
    }

    private int GetCurrentLevel(TentacleUpgradeType upgradeType)
    {
        if (progression == null)
            return 0;

        switch (upgradeType)
        {
            case TentacleUpgradeType.Toughness:
                return progression.toughnessLevel;
            case TentacleUpgradeType.Length:
                return progression.lengthLevel;
            case TentacleUpgradeType.HitRadius:
                return progression.hitRadiusLevel;
            case TentacleUpgradeType.HurtRadius:
                return progression.hurtRadiusLevel;
            case TentacleUpgradeType.Power:
            default:
                return progression.powerLevel;
        }
    }

    private void CacheComponents()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (progression == null)
            progression = GetComponent<TentacleProgression>();
    }

    private void OnValidate()
    {
        if (upgrades == null)
            return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeEntry entry = upgrades[i];
            if (entry == null)
                continue;

            EnsureEntryDefaults(entry);
        }
    }

    private void EnsureEntryDefaults(UpgradeEntry entry)
    {
        if (entry == null)
            return;

        if (entry.key == KeyCode.None)
            entry.key = KeyCode.U;

        if (string.IsNullOrWhiteSpace(entry.upgradeName))
            entry.upgradeName = "触手力量";

        if (string.IsNullOrWhiteSpace(entry.costItemId))
            entry.costItemId = "special_material";

        entry.baseCost = Mathf.Max(1, entry.baseCost);
        entry.costPerLevel = Mathf.Max(0, entry.costPerLevel);
        entry.levelsPerUpgrade = Mathf.Max(1, entry.levelsPerUpgrade);
    }
}
