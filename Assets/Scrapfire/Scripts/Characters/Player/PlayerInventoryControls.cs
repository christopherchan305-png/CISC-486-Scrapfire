using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Player
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterPlayer), typeof(MedicalItemUser))]
    public sealed class PlayerInventoryControls : MonoBehaviour
    {
        public enum Panel { None, Inventory, MedicalWheel, Attachments }
        [Min(0.05f)] public float MedicalHoldThreshold = 0.25f;
        public Panel ActivePanel { get; private set; }
        public bool BlocksGameplay => ActivePanel != Panel.None;
        public event Action<Panel, bool> PanelVisibilityChanged;
        public event Action AttachmentPanelRequested;
        private CharacterPlayer _player;
        private MedicalItemUser _medical;
        private float _medicalDownTime = float.NaN;
        private static readonly Key[] WeaponKeys = { Key.Digit1, Key.Digit2 };

        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
            _medical = GetComponent<MedicalItemUser>();
        }
        private void Update()
        {
            if (!_player.CanAct) { ClosePanels(); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    _medicalDownTime = float.NaN;
                    ShowPanel(ActivePanel == Panel.Inventory ? Panel.None : Panel.Inventory);
                }
                if (keyboard.escapeKey.wasPressedThisFrame) ClosePanels();
                if (keyboard.nKey.wasPressedThisFrame && !BlocksGameplay) AttachmentPanelRequested?.Invoke();
                if (keyboard.digit5Key.wasPressedThisFrame) MedicalPressed(Time.unscaledTime);
                if (keyboard.digit5Key.isPressed) MedicalHeld(Time.unscaledTime);
                if (keyboard.digit5Key.wasReleasedThisFrame) MedicalReleased(Time.unscaledTime);
                if (!BlocksGameplay)
                    for (int i = 0; i < WeaponKeys.Length; i++)
                        if (keyboard[WeaponKeys[i]].wasPressedThisFrame) _player.SelectWeapon(i);
            }
            var mouse = Mouse.current;
            if (!BlocksGameplay && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) _player.CycleWeapon(scroll > 0f ? 1 : -1);
            }
        }

        public void MedicalPressed(float time)
        {
            if (_player.CanAct && ActivePanel == Panel.None) _medicalDownTime = time;
        }
        public void MedicalHeld(float time)
        {
            if (_player.CanAct && !float.IsNaN(_medicalDownTime) && ActivePanel == Panel.None
                && time - _medicalDownTime >= Mathf.Max(0.05f, MedicalHoldThreshold)) ShowPanel(Panel.MedicalWheel);
        }
        public void MedicalReleased(float time)
        {
            if (float.IsNaN(_medicalDownTime)) return;
            MedicalHeld(time);
            bool tap = ActivePanel == Panel.None;
            _medicalDownTime = float.NaN;
            if (ActivePanel == Panel.MedicalWheel) ShowPanel(Panel.None);
            else if (tap) _medical.UseRecommended();
        }
        public void ShowPanel(Panel panel)
        {
            if (ActivePanel == panel) return;
            if (panel != Panel.MedicalWheel) _medicalDownTime = float.NaN;
            var previous = ActivePanel;
            ActivePanel = panel;
            if (previous != Panel.None) PanelVisibilityChanged?.Invoke(previous, false);
            if (panel != Panel.None) PanelVisibilityChanged?.Invoke(panel, true);
        }
        public void ClosePanels()
        {
            _medicalDownTime = float.NaN;
            ShowPanel(Panel.None);
        }
        private void OnDisable() => ClosePanels();
        private void OnApplicationFocus(bool focused) { if (!focused) ClosePanels(); }
    }
}
