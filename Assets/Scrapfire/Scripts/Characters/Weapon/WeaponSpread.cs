using UnityEngine;

namespace Scrapfire.Characters.Weapon
{
    public static class WeaponSpread
    {
        public static float GetAngle(WeaponData data, WeaponUseContext context)
        {
            float angle = context.IsAiming ? data.RecoilValueOnAim : data.RecoilValue;

            if (context.IsSprinting)
            {
                angle *= data.RecoilMultiplierOnSprint;
            }
            else if (context.IsMoving)
            {
                angle *= data.RecoilMultiplierOnMove;
            }

            return Mathf.Clamp(angle, 0f, 90f);
        }

        public static Vector3 Apply(Vector3 direction, float angle)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            direction.Normalize();
            float radians = Mathf.Clamp(angle, 0f, 90f) * Mathf.Deg2Rad;
            // Uniform solid-angle sampling inside a cone around the requested aim direction.
            float cosine = Mathf.Lerp(1f, Mathf.Cos(radians), Random.value);
            float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
            float azimuth = Random.value * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(azimuth) * sine,
                Mathf.Sin(azimuth) * sine, cosine);
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up) * local;
        }
    }
}
