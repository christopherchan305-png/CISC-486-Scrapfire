using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Interaction;
using Scrapfire.Waves;

namespace Scrapfire.Prototype
{
    /// <summary>Disposable prototype diagnostics; replace with the team's HUD when ready.</summary>
    public sealed class PrototypeSessionHUD : MonoBehaviour
    {
        public CharacterPlayer Player;
        public WaveDirector Waves;
        private PlayerInteractor _interactor;
        private GUIStyle _text;
        private void Start() { if (Player != null) _interactor = Player.GetComponent<PlayerInteractor>(); }
        private void OnGUI()
        {
            if (Player == null || Player.Balance == null) return;
            Camera camera = Player.PlayerCamera;
            if (camera == null || !camera.isActiveAndEnabled) return;
            var observer = camera.GetComponent<Scrapfire.Levels.RegionViewObserver>();
            if (observer == null || observer.Target != Player.transform) return;
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                _text.normal.textColor = Color.white;
            }
            Rect viewport = camera.pixelRect;
            GUILayout.BeginArea(new Rect(viewport.x + 16f, Screen.height - viewport.yMax + 16f,
                Mathf.Min(550f, viewport.width - 32f), Mathf.Min(260f, viewport.height - 32f)));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"SCRAPFIRE   ${Player.Balance.Amount}   HP {Player.CharacterHealth.CurrentHealthValue:0}/{Player.CharacterHealth.MaximumHealthValue:0}   {Player.HealthState}", _text);
            if (Waves != null)
            {
                GUILayout.Label($"Wave {Waves.WaveNumber}   {Waves.State}   Remaining: {Waves.PendingCount} + {Waves.Spawner.AliveCount} alive", _text);
                if (!string.IsNullOrEmpty(Waves.StatusMessage)) GUILayout.Label(Waves.StatusMessage, _text);
            }
            if (_interactor != null && !string.IsNullOrEmpty(_interactor.Prompt))
            {
                GUILayout.Label(_interactor.Prompt, _text);
                GUILayout.Label("Purchases use your own balance.", _text);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
