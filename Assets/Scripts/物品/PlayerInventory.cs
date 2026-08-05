using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家临时物品计数器。
/// 后续接正式背包系统时，可以保留 AddItem/GetItemCount 作为物品系统入口。
/// </summary>
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public class ItemStack
    {
        [Tooltip("物品配置 ID。后续接 Excel/CSV 物品表时，用这个 ID 查找物品数据。")]
        public string itemId;

        [Tooltip("该物品当前拥有数量。")]
        public int amount;

        public ItemStack(string itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }
    }

    [Header("调试观察")]
    [Tooltip("当前拥有的物品列表，仅用于运行时调试观察。请通过 AddItem 修改数量。")]
    public List<ItemStack> items = new List<ItemStack>();

    [Tooltip("拾取物品时是否在 Console 输出当前总数量。")]
    public bool logItemChanges = true;

    public event Action<string, int, int> ItemChanged;

    public bool AddItem(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        ItemStack stack = FindStack(itemId);
        if (stack == null)
        {
            stack = new ItemStack(itemId, 0);
            items.Add(stack);
        }

        stack.amount = Mathf.Max(0, stack.amount + amount);
        ItemChanged?.Invoke(itemId, amount, stack.amount);

        if (logItemChanges)
            Debug.Log($"背包获得物品：{itemId} +{amount}，当前 {stack.amount}", this);

        return true;
    }

    public bool TrySpendItem(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        ItemStack stack = FindStack(itemId);
        if (stack == null || stack.amount < amount)
            return false;

        stack.amount -= amount;
        ItemChanged?.Invoke(itemId, -amount, stack.amount);

        if (logItemChanges)
            Debug.Log($"背包消耗物品：{itemId} -{amount}，当前 {stack.amount}", this);

        return true;
    }

    public int GetItemCount(string itemId)
    {
        ItemStack stack = FindStack(itemId);
        return stack != null ? stack.amount : 0;
    }

    public bool HasItem(string itemId, int requiredAmount = 1)
    {
        return GetItemCount(itemId) >= Mathf.Max(1, requiredAmount);
    }

    public List<ItemStack> CreateSnapshot()
    {
        List<ItemStack> snapshot = new List<ItemStack>();
        if (items == null)
            return snapshot;

        for (int i = 0; i < items.Count; i++)
        {
            ItemStack stack = items[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.itemId) || stack.amount <= 0)
                continue;

            snapshot.Add(new ItemStack(stack.itemId, stack.amount));
        }

        return snapshot;
    }

    public void ApplySnapshot(List<ItemStack> snapshot)
    {
        if (items == null)
            items = new List<ItemStack>();

        items.Clear();
        if (snapshot == null)
            return;

        for (int i = 0; i < snapshot.Count; i++)
        {
            ItemStack stack = snapshot[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.itemId) || stack.amount <= 0)
                continue;

            items.Add(new ItemStack(stack.itemId, stack.amount));
            ItemChanged?.Invoke(stack.itemId, stack.amount, stack.amount);
        }
    }

    private ItemStack FindStack(string itemId)
    {
        if (items == null)
            items = new List<ItemStack>();

        for (int i = 0; i < items.Count; i++)
        {
            ItemStack stack = items[i];
            if (stack != null && stack.itemId == itemId)
                return stack;
        }

        return null;
    }

    private void OnValidate()
    {
        if (items == null)
            items = new List<ItemStack>();

        for (int i = items.Count - 1; i >= 0; i--)
        {
            ItemStack stack = items[i];
            if (stack == null)
            {
                items.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }
    }
}
