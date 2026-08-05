using UnityEngine;

/// <summary>
/// 负责保存和消耗玩家的 ODM 气体资源。
/// </summary>
public class OdmGasSystem : MonoBehaviour
{
    [Header("气体")]
    [Tooltip("场景开始时恢复到的最大气体容量。")]
    public float maxGasCapacity = 100f;

    [Tooltip("当前气体数量。运行时数值，降到 0 后会禁止 ODM 动作。")]
    public float currentGas;

    [Tooltip("玩家按住 Q 主动拉向锚点时，每秒消耗的气体数量。")]
    public float gasConsumeRate = 5f;

    /// <summary>气体耗尽时返回 true。</summary>
    public bool IsGasEmpty => currentGas <= 0f;

    /// <summary>
    /// 场景开始时把气体恢复到最大容量。
    /// </summary>
    void Start()
    {
        currentGas = maxGasCapacity;
    }

    /// <summary>
    /// 消耗固定数量的气体，并保证不会低于 0。
    /// </summary>
    /// <param name="amount">要扣除的气体数量。</param>
    public void ConsumeGas(float amount)
    {
        currentGas = Mathf.Max(0f, currentGas - amount);
    }

    /// <summary>
    /// 按照 gasConsumeRate 和经过时间持续消耗气体。
    /// </summary>
    /// <param name="deltaTime">经过的时间，通常传入 Time.fixedDeltaTime。</param>
    public void HandleContinuousConsumption(float deltaTime)
    {
        ConsumeGas(gasConsumeRate * deltaTime);
    }
}
