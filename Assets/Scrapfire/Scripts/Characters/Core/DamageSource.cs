using UnityEngine;

namespace Scrapfire.Characters.Core
{
    public enum DamageType
    {
        Projectile,
        Melee,
        Explosive,
        Environmental
    }

    [System.Serializable]
    public struct DamageSource
    {
        public float Damage;
        public DamageType Type;
        public GameObject CausedBy;
        public GameObject Victim;
        public Health HealthOnVictim;

        public DamageSource(float damage, DamageType type, GameObject causedBy, Health healthOnVictim)
        {
            Damage = damage;
            Type = type;
            CausedBy = causedBy;
            HealthOnVictim = healthOnVictim;
            Victim = healthOnVictim != null ? healthOnVictim.gameObject : null;
        }
    }
}
