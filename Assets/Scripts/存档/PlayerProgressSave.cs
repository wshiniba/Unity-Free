using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家临时进度存档。
/// 当前保存背包物品和触手成长等级，后续换正式存档系统时可以迁移 SaveData 结构。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(TentacleProgression))]
public class PlayerProgressSave : MonoBehaviour
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public List<PlayerInventory.ItemStack> items = new List<PlayerInventory.ItemStack>();
        public int powerLevel;
        public int toughnessLevel;
        public int lengthLevel;
        public int hitRadiusLevel;
        public int hurtRadiusLevel;
    }

    [Header("组件引用")]
    [Tooltip("玩家背包。为空时自动读取当前物体上的 PlayerInventory。")]
    public PlayerInventory inventory;

    [Tooltip("触手成长组件。为空时自动读取当前物体上的 TentacleProgression。")]
    public TentacleProgression progression;

    [Header("存档设置")]
    [Tooltip("PlayerPrefs 使用的存档 Key。后续多存档或多角色时可以按角色 ID 区分。")]
    public string saveKey = "free_player_progress";

    [Tooltip("开启后，游戏开始时会自动读取本地测试存档。")]
    public bool loadOnStart = true;

    [Tooltip("开启后，背包物品变化时会自动保存。")]
    public bool autoSaveOnInventoryChange = true;

    [Tooltip("开启后，触手等级变化时会自动保存。")]
    public bool autoSaveOnProgressionChange = true;

    [Tooltip("开启后，游戏退出时会保存一次当前进度。")]
    public bool saveOnApplicationQuit = true;

    [Tooltip("保存或读取时是否在 Console 输出调试日志。")]
    public bool logSaveEvents = true;

    private bool isApplyingLoadedData;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (loadOnStart)
            LoadNow();

        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void OnApplicationQuit()
    {
        if (saveOnApplicationQuit)
            SaveNow();
    }

    [ContextMenu("保存玩家进度")]
    public void SaveNow()
    {
        CacheComponents();
        if (inventory == null || progression == null || string.IsNullOrWhiteSpace(saveKey))
            return;

        SaveData data = new SaveData
        {
            items = inventory.CreateSnapshot(),
            powerLevel = progression.powerLevel,
            toughnessLevel = progression.toughnessLevel,
            lengthLevel = progression.lengthLevel,
            hitRadiusLevel = progression.hitRadiusLevel,
            hurtRadiusLevel = progression.hurtRadiusLevel
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();

        if (logSaveEvents)
            Debug.Log($"玩家进度已保存：{saveKey}", this);
    }

    [ContextMenu("读取玩家进度")]
    public bool LoadNow()
    {
        CacheComponents();
        if (inventory == null || progression == null || string.IsNullOrWhiteSpace(saveKey))
            return false;

        if (!PlayerPrefs.HasKey(saveKey))
        {
            if (logSaveEvents)
                Debug.Log($"没有找到玩家进度存档：{saveKey}", this);
            return false;
        }

        string json = PlayerPrefs.GetString(saveKey);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
            return false;

        isApplyingLoadedData = true;
        inventory.ApplySnapshot(data.items);
        progression.ApplyProgressionConfig(
            data.powerLevel,
            data.toughnessLevel,
            data.lengthLevel,
            data.hitRadiusLevel,
            data.hurtRadiusLevel);
        isApplyingLoadedData = false;

        if (logSaveEvents)
            Debug.Log($"玩家进度已读取：{saveKey}", this);

        return true;
    }

    [ContextMenu("清除玩家进度")]
    public void ClearSave()
    {
        if (string.IsNullOrWhiteSpace(saveKey))
            return;

        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();

        if (logSaveEvents)
            Debug.Log($"玩家进度已清除：{saveKey}", this);
    }

    private void SubscribeEvents()
    {
        if (inventory != null)
            inventory.ItemChanged += OnInventoryChanged;

        if (progression != null)
            progression.ProgressionChanged += OnProgressionChanged;
    }

    private void UnsubscribeEvents()
    {
        if (inventory != null)
            inventory.ItemChanged -= OnInventoryChanged;

        if (progression != null)
            progression.ProgressionChanged -= OnProgressionChanged;
    }

    private void OnInventoryChanged(string itemId, int delta, int total)
    {
        if (!isApplyingLoadedData && autoSaveOnInventoryChange)
            SaveNow();
    }

    private void OnProgressionChanged()
    {
        if (!isApplyingLoadedData && autoSaveOnProgressionChange)
            SaveNow();
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
        if (string.IsNullOrWhiteSpace(saveKey))
            saveKey = "free_player_progress";
    }
}
