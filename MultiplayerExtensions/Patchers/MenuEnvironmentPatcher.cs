﻿using HarmonyLib;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System.Linq;

namespace MultiplayerExtensions.Patchers
{
    [HarmonyPatch]
    public class MenuEnvironmentPatcher : IAffinity
    {
        private readonly GameplaySetupViewController _gameplaySetup;
        private readonly EnvironmentsListModel _environmentsListModel;
		private readonly Config _config;
        private readonly SiraLog _logger;

        internal MenuEnvironmentPatcher(
            GameplaySetupViewController gameplaySetup,
            EnvironmentsListModel environmentsListModel,
            Config config,
            SiraLog logger)
        {
            _gameplaySetup = gameplaySetup;
            _environmentsListModel = environmentsListModel;
            _config = config;
            _logger = logger;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameplaySetupViewController), nameof(GameplaySetupViewController.Setup))]
        private static void EnableEnvironmentTab(bool showModifiers, ref bool showEnvironmentOverrideSettings, bool showColorSchemesSettings, bool showMultiplayer, PlayerSettingsPanelController.PlayerSettingsPanelLayout playerSettingsPanelLayout)
        {
            if (showMultiplayer)
                showEnvironmentOverrideSettings = Plugin.Config.SoloEnvironment;
        }

        private EnvironmentInfoSO _originalEnvironmentInfo = null!;

        [AffinityPrefix]
        [AffinityPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), "Init")]
        private void SetEnvironmentScene(ref MultiplayerLevelScenesTransitionSetupDataSO __instance, ref BeatmapKey beatmapKey, ref BeatmapLevel beatmapLevel, ref EnvironmentInfoSO ____loadedMultiplayerEnvironmentInfo)
        {
            if (!_config.SoloEnvironment)
                return;
            EnvironmentName envName =
	            beatmapLevel.GetEnvironmentName(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty);
            _originalEnvironmentInfo = __instance.GetOrLoadMultiplayerEnvironmentInfo(); // Save original env info in a temp variable
			____loadedMultiplayerEnvironmentInfo = _environmentsListModel.GetEnvironmentInfoBySerializedNameSafe(envName);
            if (_gameplaySetup.environmentOverrideSettings.overrideEnvironments)
	            ____loadedMultiplayerEnvironmentInfo = _gameplaySetup.environmentOverrideSettings.GetOverrideEnvironmentInfoForType(____loadedMultiplayerEnvironmentInfo.environmentType);
        }

        [AffinityPostfix]
        [AffinityPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), "Init")]
        private void ResetEnvironmentScene(ref EnvironmentInfoSO ____loadedMultiplayerEnvironmentInfo)
        {
            if (_config.SoloEnvironment)
	            ____loadedMultiplayerEnvironmentInfo = _originalEnvironmentInfo;
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(ScenesTransitionSetupDataSO), "Init")]
        private void AddEnvironmentOverrides(ref SceneInfo[] scenes)
        {
	        if (_config.SoloEnvironment && scenes.Any(scene => scene.name.Contains("Multiplayer")))
	        {
		        _logger.Debug($"At least one scenes name contains Multiplayer, adding original env info");
		        // Ensures the original environment info comes before GameCore as some mods rely on GameCore being present on the scene switch callback
		        scenes = scenes.Take(1)
			        .Concat(new[] { _originalEnvironmentInfo.sceneInfo })
			        .Concat(scenes.Skip(1))
			        .ToArray();
	        }
        }
    }
}
