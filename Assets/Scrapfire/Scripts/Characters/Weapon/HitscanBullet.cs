using System;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Weapon
{
    public static class HitscanBullet
    {
        public static bool Cast(Vector3 origin, Vector3 direction, float range,
            LayerMask layers, Character owner, out RaycastHit result)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, layers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Scrapfire.Levels.RegionMovementBarrier>() != null)
                    continue;
                if (hit.collider.transform == owner.transform
                    || hit.collider.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                result = hit;
                return true;
            }

            result = default;
            return false;
        }
    }
}
