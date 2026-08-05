using UnityEngine;

/// <summary>
/// Boss 攻击期间生成的临时可钩锁点。
/// </summary>
public class HookableBossAnchor : MonoBehaviour
{
    [Header("生命周期")]
    [Tooltip("可钩锁点生成后保留的时间。时间结束后会自动销毁。")]
    public float lifetime = 3f;

    /// <summary>
    /// 设置临时可钩锁点的视觉颜色。
    /// </summary>
    void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = new Color(0.1f, 0.75f, 1f, 1f);
    }

    /// <summary>
    /// 到达生命周期后自动销毁。
    /// </summary>
    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
