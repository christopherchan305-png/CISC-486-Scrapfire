using System;
using UnityEngine;

namespace Scrapfire.Economy
{
    /// <summary>Session money owned by one player. No static/shared balance or persistence.</summary>
    [DisallowMultipleComponent]
    public sealed class Balance : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _startingAmount = 500;
        public int Amount { get; private set; }
        public event Action<int> Changed;

        private void Awake() => ResetForSession();

        public bool Credit(int amount)
        {
            if (amount <= 0 || amount > int.MaxValue - Amount) return false;
            Amount += amount;
            Changed?.Invoke(Amount);
            return true;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Amount) return false;
            Amount -= amount;
            if (amount != 0) Changed?.Invoke(Amount);
            return true;
        }

        public void ResetForSession()
        {
            Amount = Mathf.Max(0, _startingAmount);
            Changed?.Invoke(Amount);
        }
    }
}
