using UnityEngine;

namespace Scrapfire.Levels
{
    /// <summary>Semantic marker: stops actors, but is never a bullet obstruction.</summary>
    public sealed class RegionMovementBarrier : MonoBehaviour
    {
        public const string LayerName = "RegionBarrier";
    }
}
