using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一管理短暂停顿造成的全局时间缩放，避免多个顿帧重叠后把 Time.timeScale 恢复到错误的低倍率。
/// </summary>
public static class TimeScaleHitStop
{
    private struct Request
    {
        public int id;
        public float timeScale;

        public Request(int id, float timeScale)
        {
            this.id = id;
            this.timeScale = timeScale;
        }
    }

    private static readonly List<Request> requests = new List<Request>();
    private static int nextId = 1;
    private static float normalFixedDeltaTime = 0.02f;
    private static bool hasActiveRequests;

    public static int Begin(float timeScale)
    {
        if (!hasActiveRequests)
        {
            normalFixedDeltaTime = Time.timeScale > 0.001f
                ? Time.fixedDeltaTime / Time.timeScale
                : Time.fixedDeltaTime;
            hasActiveRequests = true;
        }

        int id = nextId++;
        requests.Add(new Request(id, Mathf.Clamp(timeScale, 0.001f, 1f)));
        ApplyCurrentScale();
        return id;
    }

    public static void End(int id)
    {
        if (id <= 0)
            return;

        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (requests[i].id == id)
                requests.RemoveAt(i);
        }

        ApplyCurrentScale();
    }

    private static void ApplyCurrentScale()
    {
        if (requests.Count == 0)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = normalFixedDeltaTime;
            hasActiveRequests = false;
            return;
        }

        float targetScale = 1f;
        for (int i = 0; i < requests.Count; i++)
            targetScale = Mathf.Min(targetScale, requests[i].timeScale);

        Time.timeScale = targetScale;
        Time.fixedDeltaTime = normalFixedDeltaTime * targetScale;
    }
}
