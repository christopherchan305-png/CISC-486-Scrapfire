using UnityEngine;

namespace Scrapfire.Characters.Core {

[AddComponentMenu("Scrapfire/Character/Core/CharacterAi")]
public class CharacterAi : Character {
    public bool DisableOnDeath;

    protected override void Awake()
    {
        CharacterCollisionLayers.Assign(gameObject, "Enemies");
        base.Awake();
    }

    protected override void OnHealthStateChanged(HealthState previous, HealthState current)
    {
        base.OnHealthStateChanged(previous, current);
        if (DisableOnDeath && current == HealthState.Dead)
            gameObject.SetActive(false);
    }
}

}
