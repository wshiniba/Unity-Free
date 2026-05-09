using System.Collections;
using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class HitFeedback : MonoBehaviour
{
    [Header("命中停顿")]
    [Tooltip("是否启用命中停顿。开启后，命中 Boss 时会短暂降低全局时间倍率，制造卡肉感。")]
    public bool enableHitStop = true;

    [Tooltip("命中停顿期间的时间倍率。数值越接近 0，卡肉越明显；建议从 0.01 到 0.08 之间调试。")]
    public float hitStopTimeScale = 0.03f;

    [Tooltip("命中停顿持续时间，使用真实时间，不受 Time.timeScale 影响。建议从 0.03 到 0.1 秒之间调试。")]
    public float hitStopDuration = 0.05f;

    [Header("相机震动")]
    [Tooltip("用于发出 Cinemachine 震动事件的组件。建议手动挂在 Boss 本体并拖入。")]
    public CinemachineImpulseSource impulseSource;

    [Tooltip("接收 Cinemachine 震动事件的虚拟相机扩展。建议手动挂在 Virtual Camera 并拖入。")]
    public CinemachineImpulseListener impulseListener;

    [Tooltip("当前 Boss 战使用的 Cinemachine Virtual Camera。仅用于在未拖 Listener 时查找。")]
    public CinemachineVirtualCamera virtualCamera;

    [Tooltip("是否启用相机震动。关闭后仍可保留命中停顿。")]
    public bool enableCameraShake = true;

    [Tooltip("相机震动强度。虚拟相机视野越大，通常需要越大的数值才明显。")]
    public float cameraShakeForce = 1.4f;

    [Tooltip("是否让命中速度影响震动强度。开启后，速度越快震动越强。")]
    public bool scaleShakeByHitSpeed = true;

    [Tooltip("命中速度达到参考速度时，相机震动最多放大的倍率。1 表示不额外放大，2 表示最多变为基础强度的 2 倍。")]
    public float maxSpeedShakeMultiplier = 2f;

    [Tooltip("开启后只产生左右震动；关闭后会按照命中方向产生震动。")]
    public bool horizontalOnlyShake = true;

    [Tooltip("开启后，如果虚拟相机上没有 CinemachineImpulseListener，会在运行时自动添加。不推荐长期依赖，调试时建议手动挂载。")]
    public bool autoAddImpulseListener = false;

    [Tooltip("开启后，运行时会把 ImpulseSource 配置成短促 Bump 波形，避免默认配置导致震动不明显。")]
    public bool configureImpulseSourceOnAwake = true;

    private Coroutine hitStopRoutine;
    private float defaultFixedDeltaTime;

    void Awake()
    {
        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();

        if (configureImpulseSourceOnAwake)
            ConfigureImpulseSource();

        defaultFixedDeltaTime = Time.fixedDeltaTime;

        CacheImpulseListener();
    }

    public void Play(Vector2 hitDirection)
    {
        Play(hitDirection, 0f);
    }

    public void Play(Vector2 hitDirection, float hitSpeedRatio)
    {
        if (enableHitStop)
            StartHitStop();

        if (enableCameraShake)
            ShakeCamera(hitDirection, hitSpeedRatio);
    }

    private void StartHitStop()
    {
        if (hitStopRoutine != null)
            StopCoroutine(hitStopRoutine);

        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        float previousTimeScale = Time.timeScale;
        float previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = Mathf.Clamp(hitStopTimeScale, 0.001f, 1f);
        Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hitStopDuration));

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        hitStopRoutine = null;
    }

    private void ShakeCamera(Vector2 hitDirection, float hitSpeedRatio)
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("[HitFeedback] Missing CinemachineImpulseSource, camera shake skipped.", this);
            return;
        }

        CacheImpulseListener();
        if (impulseListener == null)
        {
            Debug.LogWarning("[HitFeedback] Missing CinemachineImpulseListener on the active virtual camera, camera shake may not be visible.", this);
        }

        Vector2 direction = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.right;
        if (horizontalOnlyShake)
            direction = new Vector2(Mathf.Sign(direction.x == 0f ? 1f : direction.x), 0f);

        float speedMultiplier = scaleShakeByHitSpeed
            ? Mathf.Lerp(1f, Mathf.Max(1f, maxSpeedShakeMultiplier), Mathf.Clamp01(hitSpeedRatio))
            : 1f;

        impulseSource.GenerateImpulseWithVelocity((Vector3)(direction * cameraShakeForce * speedMultiplier));
    }

    private void CacheImpulseListener()
    {
        if (impulseListener != null)
            return;

        if (virtualCamera == null)
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (virtualCamera == null)
            return;

        impulseListener = virtualCamera.GetComponent<CinemachineImpulseListener>();
        if (impulseListener == null && autoAddImpulseListener)
            impulseListener = virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();
    }

    private void ConfigureImpulseSource()
    {
        if (impulseSource == null || impulseSource.m_ImpulseDefinition == null)
            return;

        CinemachineImpulseDefinition definition = impulseSource.m_ImpulseDefinition;
        definition.m_ImpulseChannel = 1;
        definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        definition.m_ImpulseDuration = 0.16f;
        definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        definition.m_DissipationDistance = 100f;
        definition.m_DissipationRate = 0.25f;
        definition.m_PropagationSpeed = 343f;
    }
}
