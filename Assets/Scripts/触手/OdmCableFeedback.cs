using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum OdmCableAnchorFeedbackType
{
    Scene,
    Enemy,
    Object
}

/// <summary>
/// 统一管理玩家绳索的音频、钩中子弹时间和命中视觉反馈。
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

    [Header("钩中子弹时间")]
    [InspectorName("启用钩中子弹时间")]
    [Tooltip("开启后，绳索钩中瞬间会短暂降低全局时间倍率。该参数只影响钩中反馈，不和攻击命中反馈共用。玩家开始拉拽时会提前结束。")]
    [FormerlySerializedAs("enableAnchorHitStop")]
    public bool enableAnchorBulletTime = true;

    [InspectorName("钩中子弹时间倍率")]
    [Tooltip("钩中子弹时间期间的全局时间倍率。数值越接近 0 越接近冻结，数值越接近 1 越接近正常速度。该值独立于攻击命中顿帧。")]
    [Range(0.001f, 1f)]
    [FormerlySerializedAs("anchorHitStopTimeScale")]
    public float anchorBulletTimeScale = 0.2f;

    [InspectorName("钩中子弹时间持续时间")]
    [Tooltip("钩中子弹时间最多持续多久，使用真实时间，不受 Time.timeScale 影响。玩家开始拉拽时会提前结束。该值独立于攻击命中顿帧。")]
    [FormerlySerializedAs("anchorHitStopDuration")]
    public float anchorBulletTimeDuration = 0.12f;

    [Header("视觉反馈")]
    [InspectorName("绳索视觉组件")]
    [Tooltip("负责绘制绳索视觉的 OdmVisuals。未指定时会自动获取同物体上的组件。")]
    public OdmVisuals visuals;

    [InspectorName("启用钩中波浪")]
    [Tooltip("开启后，绳索钩中时会通知 OdmVisuals 播放命中波浪。")]
    public bool enableAnchorWave = true;

    private Coroutine anchorBulletTimeRoutine;
    private int anchorBulletTimeToken;

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
        anchorBulletTimeScale = Mathf.Clamp(anchorBulletTimeScale, 0.001f, 1f);
        anchorBulletTimeDuration = Mathf.Max(0f, anchorBulletTimeDuration);
    }

    private void OnDestroy()
    {
        EndAnchorBulletTime();
    }

    public void PlayShoot(bool isLeft)
    {
        PlayOneShot(shootClip, shootVolume);
    }

    public void PlayAnchor(bool isLeft, OdmCableAnchorFeedbackType anchorType)
    {
        PlayOneShot(GetAnchorClip(anchorType), anchorVolume);

        if (enableAnchorBulletTime)
            StartAnchorBulletTime();

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

    private void StartAnchorBulletTime()
    {
        if (anchorBulletTimeDuration <= 0f)
            return;

        EndAnchorBulletTime();
        anchorBulletTimeRoutine = StartCoroutine(AnchorBulletTimeRoutine());
    }

    private IEnumerator AnchorBulletTimeRoutine()
    {
        anchorBulletTimeToken = TimeScaleHitStop.Begin(anchorBulletTimeScale);

        yield return new WaitForSecondsRealtime(anchorBulletTimeDuration);

        TimeScaleHitStop.End(anchorBulletTimeToken);
        anchorBulletTimeToken = 0;
        anchorBulletTimeRoutine = null;
    }

    public void EndAnchorBulletTime()
    {
        if (anchorBulletTimeRoutine != null)
        {
            StopCoroutine(anchorBulletTimeRoutine);
            anchorBulletTimeRoutine = null;
        }

        if (anchorBulletTimeToken != 0)
        {
            TimeScaleHitStop.End(anchorBulletTimeToken);
            anchorBulletTimeToken = 0;
        }
    }
}
