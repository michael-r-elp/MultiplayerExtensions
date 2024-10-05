using IPA.Utilities;
using MultiplayerExtensions.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace MultiplayerExtensions.Environments
{
    public class MpexAvatarPlaceLighting : MonoBehaviour
    {
        public const float SmoothTime = 2f;
        public Color TargetColor { get; private set; } = Color.black;
        public int SortIndex { get; internal set; }

        private List<TubeBloomPrePassLight> _lights = new List<TubeBloomPrePassLight>();

        private IMultiplayerSessionManager _sessionManager = null!;
        private MpexPlayerManager _mpexPlayerManager = null!;
        private Config _config = null!;

        [Inject]
        internal void Construct(
            IMultiplayerSessionManager sessionManager,
            MpexPlayerManager mpexPlayerManager,
            Config config)
        {
            _sessionManager = sessionManager;
            _mpexPlayerManager = mpexPlayerManager;
            _config = config;
        }

        private void Start()
        {
            _lights = GetComponentsInChildren<TubeBloomPrePassLight>().ToList();

            if (_sessionManager == null || _mpexPlayerManager == null || _sessionManager.localPlayer == null)
                return;

            if (_sessionManager.localPlayer.sortIndex == SortIndex)
            {
                SetColor(_config.PlayerColor, true);
                return;
            }

            foreach (var player in _sessionManager.connectedPlayers)
                if (player.sortIndex == SortIndex)
                {
                    SetColor(_mpexPlayerManager.GetPlayer(player.userId)?.Color ?? Config.DefaultPlayerColor, true);
                    return;
                }

            SetColor(Color.black);
        }

        private void OnEnable()
        {
            _mpexPlayerManager.PlayerConnectedEvent += HandlePlayerData;
            _sessionManager.playerConnectedEvent += HandlePlayerConnected;
            _sessionManager.playerDisconnectedEvent += HandlePlayerDisconnected;
        }

        private void OnDisable()
        {
            _mpexPlayerManager.PlayerConnectedEvent -= HandlePlayerData;
            _sessionManager.playerConnectedEvent -= HandlePlayerConnected;
            _sessionManager.playerDisconnectedEvent -= HandlePlayerDisconnected;
        }

        private void HandlePlayerData(IConnectedPlayer player, MpexPlayerData data)
        {
            if (player.sortIndex == SortIndex)
                SetColor(data.Color, false);
        }

        private void HandlePlayerConnected(IConnectedPlayer player)
        {
            if (player.sortIndex != SortIndex)
                return;
            if (_mpexPlayerManager.TryGetPlayer(player.userId, out MpexPlayerData data))
                SetColor(data.Color, false);
            else
                SetColor(Config.DefaultPlayerColor, false);
        }

        private void HandlePlayerDisconnected(IConnectedPlayer player)
        {
            if (player.sortIndex == SortIndex)
                SetColor(Color.black, false);
        }

        private void Update()
        {
            Color current = GetColor();
            if (current == TargetColor)
                return;
            if (IsColorVeryCloseToColor(current, TargetColor))
                SetColor(TargetColor);
            else
                SetColor(Color.Lerp(current, TargetColor, Time.deltaTime * SmoothTime));
        }

		private bool IsColorVeryCloseToColor(Color color0, Color color1)
		{
			return Mathf.Abs(color0.r - color1.r) < 0.002f && Mathf.Abs(color0.g - color1.g) < 0.002f && Mathf.Abs(color0.b - color1.b) < 0.002f && Mathf.Abs(color0.a - color1.a) < 0.002f;
		}

		public void SetColor(Color color, bool immediate)
        {
            TargetColor = color;
            if (immediate)
                SetColor(color);
        }

        public Color GetColor()
        {
            if (_lights.Count > 0)
                return _lights[0].color;
            return Color.black;
        }

        private void SetColor(Color color)
        {
            foreach(TubeBloomPrePassLight light in _lights)
            {
                light.color = color;
                light.Refresh();
            }
        }
    }
}
