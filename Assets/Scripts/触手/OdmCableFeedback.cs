using System.Collections;
using UnityEngine;

public enum OdmCableAnchorFeedbackType
{
    Scene,
    Enemy,
    Object
}

/// <summary>
/// 统一管理玩家绳索的音频、顿帧和命中视觉反馈。
/// </summary>
[DisallowMultipleComponent]
public class OdmCableFeedback : MonoBehaviour
{
    [Header("音频源")]
    [InspectorName("音频源")]
    [Tooltip("播放绳索反馈音效的 AudioSource。未指定时会在运行时自动获取或添加。")]
    public AudioSource audioSource;

    [InspectorName("总音量")]
    [Tooltip("所有绳索音效的总音量倍率。")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Header("音效片段")]
    [InspectorName("发射音效")]
    [Tooltip("绳索成功发射时播放的音效。")]
    public AudioClip shootClip;

    [InspectorName("普通钩点命中音效")]
    [Tooltip("绳索钩中普通场景钩点时播放的音效。")]
    public AudioClip sceneAnchorClip;

    [InspectorName("敌人命中音效")]
    [Tooltip("绳索钩中敌人时播放的音效。")]
    public AudioClip enemyAnchorClip;

    [InspectorName("交互物命中音效")]
    [Tooltip("绳索钩中可交互物体时播放的音效。")]
    public AudioClip objectAnchorClip;

    [InspectorName("释放音效")]
    [Tooltip("主动松开或收回绳索时播放的音效。")]
    public AudioClip releaseClip;

    [InspectorName("失败音效")]
    [Tooltip("绳索发射失败或未命中时播放的音效。当前只预留入口，暂不自动触发。")]
    public AudioClip failClip;

    [Header("音效音量")]
    [InspectorName("发射音量")]
    [Tooltip("绳索发射音效音量。")]
    [Range(0f, 1f)]
    public float shootVolume = 1f;

    [InspectorName("命中音量")]
    [Tooltip("绳索钩中音效音量。")]
    [Range(0f, 1f)]
    public float anchorVolume = 1f;

    [InspectorName("释放音量")]
    [Tooltip("绳索松开音效音量。")]
    [Range(0f, 1f)]
    public float releaseVolume = 1f;

    [InspectorName("失败音量")]
    [Tooltip("绳索失败音效音量。")]
    [Range(0f, 1f)]
    public float failVolume = 1f;

    [Header("钩中停顿")]
    [InspectorName("启用钩中停顿")]
    [Tooltip("开启后，绳索钩中瞬间会短暂降低全局时间倍率，制造卡肉感。")]
    public bool enableAnchorHitStop = true;

    [InspectorName("钩中停顿时间倍率")]
    [Tooltip("钩中停顿期间的全局时间倍率。数值越接近 0，停滞越明显。")]
    [Range(0.001f, 1f)]
    public float anchorHitStopTimeScale = 0.08f;

    [InspectorName("钩中停顿持续时间")]
    [Tooltip("钩中停顿持续时间，使用真实时间，不受 Time.timeScale 影响。")]
    public float anchorHitStopDuration = 0.035f;

    [Header("视觉反馈")]
    [InspectorName("绳索视觉组件")]
    [Tooltip("负责绘制绳索视觉的 OdmVisuals。未指定时会自动获取同物体上的组件。")]
    public OdmVisuals visuals;

    [InspectorName("启用钩中波浪")]
    [Tooltip("开启后，绳索钩中时会通知 OdmVisuals 播放命中波浪。")]
    public bool enableAnchorWave = true;

    private Coroutine anchorHitStopRoutine;
    private int anchorHitStopToken;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;

        if (visuals == null)
            visuals = GetComponent<OdmVisuals>();
    }

    private void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        shootVolume = Mathf.Clamp01(shootVolume);
        anchorVolume = Mathf.Clamp01(anchorVolume);
        releaseVolume = Mathf.Clamp01(releaseVolume);
        failVolume = Mathf.Clamp01(failVolume);
        anchorHitStopTimeScale = Mathf.Clamp(anchorHitStopTimeScale, 0.001f, 1f);
        anchorHitStopDuration = Mathf.Max(0f, anchorHitStopDuration);
    }

    private void OnDestroy()
    {
        EndAnchorHitStop();
    }

    public void PlayShoot(bool isLeft)
    {
        PlayOneShot(shootClip, shootVolume);
    }

    public void PlayAnchor(bool isLeft, OdmCableAnchorFeedbackType anchorType)
    {
        PlayOneShot(GetAnchorClip(anchorType), anchorVolume);

        if (enableAnchorHitStop)
            StartAnchorHitStop();

        if (enableAnchorWave && visuals != null)
            visuals.PlayAnchorWave(isLeft);
    }

    public void PlayRelease(bool isLeft)
    {
        PlayOneShot(releaseClip, releaseVolume);
    }

    public void PlayFail(bool isLeft)
    {
        PlayOneShot(failClip, failVolume);
    }

    private AudioClip GetAnchorClip(OdmCableAnchorFeedbackType anchorType)
    {
        switch (anchorType)
        {
            case OdmCableAnchorFeedbackType.Enemy:
                return enemyAnchorClip != null ? enemyAnchorClip : sceneAnchorClip;
            case OdmCableAnchorFeedbackType.Object:
                return objectAnchorClip != null ? objectAnchorClip : sceneAnchorClip;
            default:
                return sceneAnchorClip;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume) * Mathf.Clamp01(volume));
    }

    private void StartAnchorHitStop()
    {
        if (anchorHitStopDuration <= 0f)
            return;

        EndAnchorHitStop();
        anchorHitStopRoutine = StartCoroutine(AnchorHitStopRoutine());
    }

    private IEnumerator AnchorHitStopRoutine()
    {
        anchorHitStopToken = TimeScaleHitStop.Begin(anchorHitStopTimeScale);

        yield return new WaitForSecondsRealtime(anchorHitStopDuration);

        TimeScaleHitStop.End(anchorHitStopToken);
        anchorHitStopToken = 0;
        anchorHitStopRoutine = null;
    }

    private void EndAnchorHitStop()
    {
        if (anchorHitStopRoutine != null)
        {
            StopCoroutine(anchorHitStopRoutine);
            anchorHitStopRoutine = null;
        }

        if (anchorHitStopToken != 0)
        {
            TimeScaleHitStop.End(anchorHitStopToken);
            anchorHitStopToken = 0;
        }
    }
}
