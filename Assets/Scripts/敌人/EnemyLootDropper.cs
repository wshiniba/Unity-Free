using System;
using UnityEngine;

/// <summary>
/// 敌人死亡时的简易掉落结算入口。
/// 后续接 Excel/CSV 或正式背包系统时，可以用 itemId 作为配置表中的物品 ID。
/// </summary>
[DisallowMultipleComponent]
public class EnemyLootDropper : MonoBehaviour
{
    [Serializable]
    public class LootEntry
    {
        [Tooltip("物品配置 ID。后续接 Excel/CSV 掉落表时，用这个 ID 查找正式物品数据。")]
        public string itemId = "special_material";

        [Tooltip("掉落后生成的物品预制体。为空时只记录调试日志，不会在场景中生成物体。")]
        public GameObject itemPrefab;

        [Tooltip("单次死亡结算时，该物品的基础掉落概率。0 表示不掉落，1 表示必定掉落。")]
        [Range(0f, 1f)]
        public float dropChance = 0.25f;

        [Tooltip("开启后，该物品会受到处决特殊掉落倍率影响。普通材料可以关闭，稀有材料建议开启。")]
        public bool affectedByExecutionBonus = true;

        [Tooltip("成功掉落时的最小数量。")]
        public int minAmount = 1;

        [Tooltip("成功掉落时的最大数量。")]
        public int maxAmount = 1;
    }

    [Header("掉落配置")]
    [Tooltip("敌人死亡时会逐项结算的简易掉落列表。后续可以由配置表覆盖这组数据。")]
    public LootEntry[] lootEntries = Array.Empty<LootEntry>();

    [Tooltip("生成掉落物时，相对敌人位置的偏移。一般让掉落物出现在敌人身体或血条下方附近。")]
    public Vector2 spawnOffset = new Vector2(0f, 0.25f);

    [Tooltip("同一组掉落物之间的水平散开距离，避免多个物品完全重叠。")]
    public float spawnSpread = 0.25f;

    [Tooltip("为空预制体的掉落命中时，是否在 Console 输出调试日志。接入正式物品前建议开启，方便确认倍率是否生效。")]
    public bool logDropsWithoutPrefab = true;

    [Tooltip("掉落项没有指定预制体时，是否生成一个临时可拾取测试物。接入正式物品素材后可以关闭。")]
    public bool spawnDefaultPickupWhenPrefabMissing = true;

    [Tooltip("最近一次死亡结算使用的特殊掉落倍率，仅用于运行时调试观察。")]
    public float lastSpecialDropMultiplier = 1f;

    /// <summary>
    /// 敌人死亡前由 Enemy 调用。处决倍率来自 Enemy.pendingSpecialDropMultiplier。
    /// </summary>
    public void DropLoot(Enemy enemy)
    {
        float specialMultiplier = enemy != null ? Mathf.Max(1f, enemy.pendingSpecialDropMultiplier) : 1f;
        DropLoot(specialMultiplier);
    }

    /// <summary>
    /// 直接按指定倍率结算掉落，方便后续测试、配置表或特殊死亡类型调用。
    /// </summary>
    public void DropLoot(float specialDropMultiplier)
    {
        lastSpecialDropMultiplier = Mathf.Max(1f, specialDropMultiplier);

        if (lootEntries == null || lootEntries.Length == 0)
            return;

        int spawnedIndex = 0;
        for (int i = 0; i < lootEntries.Length; i++)
        {
            LootEntry entry = lootEntries[i];
            if (entry == null)
                continue;

            float chance = Mathf.Clamp01(entry.dropChance);
            if (entry.affectedByExecutionBonus)
                chance = Mathf.Clamp01(chance * lastSpecialDropMultiplier);

            if (UnityEngine.Random.value > chance)
                continue;

            int amount = UnityEngine.Random.Range(
                Mathf.Max(1, entry.minAmount),
                Mathf.Max(Mathf.Max(1, entry.minAmount), entry.maxAmount) + 1);

            SpawnLoot(entry, amount, spawnedIndex);
            spawnedIndex++;
        }
    }

    private void SpawnLoot(LootEntry entry, int amount, int spawnedIndex)
    {
        Vector3 position = transform.position + (Vector3)spawnOffset;
        float spreadOffset = (spawnedIndex - 0.5f) * Mathf.Max(0f, spawnSpread);
        position.x += spreadOffset;

        if (entry.itemPrefab == null)
        {
            if (spawnDefaultPickupWhenPrefabMissing)
                LootPickup.CreateDefault(entry.itemId, amount, position);

            if (logDropsWithoutPrefab)
                Debug.Log($"敌人掉落结算命中：{entry.itemId} x{amount}，倍率 {lastSpecialDropMultiplier:0.##}", this);
            return;
        }

        GameObject dropObject = Instantiate(entry.itemPrefab, position, Quaternion.identity);
        dropObject.name = amount > 1
            ? $"{entry.itemPrefab.name}_{entry.itemId}_x{amount}"
            : $"{entry.itemPrefab.name}_{entry.itemId}";

        LootPickup pickup = dropObject.GetComponent<LootPickup>();
        if (pickup != null)
            pickup.Initialize(entry.itemId, amount);
    }

    private void OnValidate()
    {
        spawnSpread = Mathf.Max(0f, spawnSpread);
        lastSpecialDropMultiplier = Mathf.Max(1f, lastSpecialDropMultiplier);

        if (lootEntries == null)
            return;

        for (int i = 0; i < lootEntries.Length; i++)
        {
            LootEntry entry = lootEntries[i];
            if (entry == null)
                continue;

            entry.dropChance = Mathf.Clamp01(entry.dropChance);
            entry.minAmount = Mathf.Max(1, entry.minAmount);
            entry.maxAmount = Mathf.Max(entry.minAmount, entry.maxAmount);
        }
    }
}
