using UnityEngine;

/// <summary>
/// 读取玩家输入，并把 ODM 操作转发给 OdmController。
/// </summary>
[RequireComponent(typeof(OdmController))]
public class OdmInput : MonoBehaviour
{
    private OdmController controller;
    private Camera mainCamera;
    private bool suppressPullUntilQReleased;

    /// <summary>
    /// 缓存运行时需要的组件引用。
    /// </summary>
    void Awake()
    {
        controller = GetComponent<OdmController>();
        mainCamera = Camera.main;
    }

    /// <summary>
    /// 每帧读取鼠标、键盘和水平轴输入。
    /// </summary>
    void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 aimDirection = (mouseWorld - (Vector2)transform.position).normalized;

        if (Input.GetMouseButtonDown(0)) controller.ShootCable(aimDirection, true);
        if (Input.GetMouseButton(0)) controller.UpdateCableTarget(mouseWorld, true);
        if (Input.GetMouseButtonUp(0)) controller.ReleaseCable(true);

        if (Input.GetMouseButtonDown(1)) controller.ShootCable(aimDirection, false);
        if (Input.GetMouseButton(1)) controller.UpdateCableTarget(mouseWorld, false);
        if (Input.GetMouseButtonUp(1)) controller.ReleaseCable(false);

        if (Input.GetKeyDown(KeyCode.Space))
            controller.TryJump();

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (controller.TryAnchorTouchedCable())
                suppressPullUntilQReleased = true;
            else
                controller.BeginPullKey();
        }

        if (Input.GetKeyUp(KeyCode.Q))
        {
            suppressPullUntilQReleased = false;
            controller.EndPullKey();
        }

        controller.IsPullKeyHeld = Input.GetKey(KeyCode.Q) && !suppressPullUntilQReleased;

        if (Input.GetKeyDown(KeyCode.W))
            controller.ToggleHoverByInput();

        if (Input.GetKeyDown(KeyCode.E))
            controller.TryExecuteEnemy(mouseWorld);

        if (Input.GetKeyDown(KeyCode.C))
            controller.BeginCableParry();

        if (Input.GetKeyDown(KeyCode.LeftShift))
            controller.EmergencyStop();

        controller.SetHorizontalInput(Input.GetAxisRaw("Horizontal"));
    }
}
