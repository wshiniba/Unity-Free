using UnityEngine;

public static class BossHazardUtility
{
    public static bool TryKnockBackPlayer(Collider2D other, Vector2 hazardPosition, float knockbackForce, int damage)
    {
        OdmController controller = other.GetComponentInParent<OdmController>();
        if (controller == null || controller.Rb == null)
            return false;

        Vector2 direction = ((Vector2)controller.transform.position - hazardPosition).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        controller.Rb.linearVelocity = Vector2.zero;
        controller.Rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
        Debug.Log($"[BossHazard] Player hit for {damage}, knockback={knockbackForce:F1}");
        return true;
    }
}
