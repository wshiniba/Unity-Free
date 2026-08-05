using UnityEngine;

public static class BossHazardUtility
{
    public static bool TryKnockBackPlayer(Collider2D other, Vector2 hazardPosition, float knockbackForce, int damage)
    {
        OdmController controller = other.GetComponentInParent<OdmController>();
        if (controller == null || controller.Rb == null)
            return false;

        return controller.GetDamage(damage, hazardPosition, knockbackForce);
    }

    public static bool TryDamagePlayerCable(Collider2D hazardCollider, Vector2 hazardPosition, float knockbackForce, int damage)
    {
        if (hazardCollider == null)
            return false;

        OdmController[] controllers = Object.FindObjectsByType<OdmController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            OdmController controller = controllers[i];
            if (controller != null && controller.TryDamageCableFromHazard(hazardCollider, hazardPosition, knockbackForce, damage))
                return true;
        }

        return false;
    }

    public static bool TryParryByPlayer(Collider2D hazardCollider, Vector2 hazardPosition, int attackPower, bool canBeParried, out OdmController parryController)
    {
        parryController = null;
        if (hazardCollider == null)
            return false;

        OdmController[] controllers = Object.FindObjectsByType<OdmController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            OdmController controller = controllers[i];
            if (controller == null || !controller.TryParryHazard(hazardCollider, hazardPosition, attackPower, canBeParried))
                continue;

            parryController = controller;
            return true;
        }

        return false;
    }
}
