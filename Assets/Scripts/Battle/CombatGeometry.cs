using UnityEngine;

public static class CombatGeometry
{
    // Measure reach to the target's surface, rather than its size-dependent centre.
    public static float HorizontalDistanceToTarget(Vector3 origin, Transform target)
    {
        Collider collider = target.GetComponent<Collider>();
        Vector3 point = collider != null ? collider.ClosestPoint(origin) : target.position;
        origin.y = point.y = 0f;
        return Vector3.Distance(origin, point);
    }
}
