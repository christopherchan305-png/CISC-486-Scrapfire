using UnityEngine;

namespace Scrapfire.Characters.Core
{
    public static class CharacterCollisionLayers
    {
        public static void Assign(GameObject actor, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;
            actor.layer = layer;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.gameObject.layer = layer;
        }
    }
}
