using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器工具：为 Player 对象自动绑定 OdmVisuals 常用引用。
/// </summary>
public static class AutoBindVisuals
{
    /// <summary>
    /// 查找 Player，绑定绳索 LineRenderer，指定 Camera.main，并把 OdmVisuals 标记为已修改。
    /// </summary>
    [MenuItem("Tools/Auto Bind OdmVisuals")]
    private static void Bind()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Player was not found.");
            return;
        }

        OdmVisuals visuals = player.GetComponent<OdmVisuals>();
        if (visuals == null)
        {
            Debug.LogError("Player does not have OdmVisuals.");
            return;
        }

        visuals.leftCableRenderer = GetLineRenderer(player, "LeftCable");
        visuals.rightCableRenderer = GetLineRenderer(player, "RightCable");
        visuals.mainCamera = Camera.main;

        EditorUtility.SetDirty(visuals);
        Debug.Log("OdmVisuals references bound.");
    }

    /// <summary>
    /// 在指定根对象下查找命名子物体，并返回其 LineRenderer。
    /// </summary>
    /// <param name="root">要搜索的父级 GameObject。</param>
    /// <param name="childName">应该带有 LineRenderer 的直接子物体名称。</param>
    /// <returns>子物体上的 LineRenderer；如果子物体或组件不存在则返回 null。</returns>
    private static LineRenderer GetLineRenderer(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);
        return child != null ? child.GetComponent<LineRenderer>() : null;
    }
}
