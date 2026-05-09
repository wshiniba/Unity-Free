using UnityEngine;

/// <summary>
/// Boss 战区域触发与封场控制。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossArenaController : MonoBehaviour
{
    [Header("Boss 引用")]
    [Tooltip("当前区域绑定的 Boss。未指定时会在场景中自动查找 BossController。")]
    public BossController boss;

    [Header("封场墙")]
    [Tooltip("Boss 战开始后启用、Boss 被击败后关闭的封场墙。未指定时会自动查找 LeftBossWall 和 RightBossWall。")]
    public GameObject[] lockdownWalls;

    [Tooltip("场景启动时是否默认封锁区域。通常保持关闭，让玩家进入触发器后再封场。")]
    public bool startLocked;

    private bool battleStarted;

    /// <summary>
    /// 初始化触发器和自动引用，并按默认设置刷新封场墙。
    /// </summary>
    void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        if (boss == null)
            boss = FindAnyObjectByType<BossController>(FindObjectsInactive.Include);

        if (lockdownWalls == null || lockdownWalls.Length == 0)
            lockdownWalls = FindLockdownWalls();

        SetArenaLocked(startLocked);
    }

    /// <summary>
    /// 玩家进入区域时开始 Boss 战。
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (battleStarted || other.GetComponentInParent<OdmController>() == null)
            return;

        BeginBattle();
    }

    /// <summary>
    /// 开始 Boss 战并启用封场墙。
    /// </summary>
    public void BeginBattle()
    {
        if (battleStarted)
            return;

        battleStarted = true;
        SetArenaLocked(true);

        if (boss != null)
            boss.BeginBattle(this);
    }

    /// <summary>
    /// 结束 Boss 战并解除封场。
    /// </summary>
    public void EndBattle()
    {
        SetArenaLocked(false);
    }

    private void SetArenaLocked(bool locked)
    {
        if (lockdownWalls == null)
            return;

        for (int i = 0; i < lockdownWalls.Length; i++)
        {
            if (lockdownWalls[i] != null)
                lockdownWalls[i].SetActive(locked);
        }
    }

    private GameObject[] FindLockdownWalls()
    {
        GameObject left = FindSceneObjectByName("LeftBossWall");
        GameObject right = FindSceneObjectByName("RightBossWall");

        if (left != null && right != null)
            return new[] { left, right };

        if (left != null)
            return new[] { left };

        if (right != null)
            return new[] { right };

        return new GameObject[0];
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i].name == objectName && objects[i].scene.IsValid())
                return objects[i];
        }

        return null;
    }
}
