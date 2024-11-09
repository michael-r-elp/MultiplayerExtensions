using HarmonyLib;
using IPA.Utilities;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Zenject.Internal;

namespace MultiplayerExtensions.Patchers
{
    [HarmonyPatch]
    public class EnvironmentPatcher : IAffinity
    {
        private readonly GameScenesManager _scenesManager;
        private readonly Config _config;
        private readonly SiraLog _logger;

        internal EnvironmentPatcher(
            GameScenesManager scenesManager,
            Config config,
            SiraLog logger)
        {
            _scenesManager = scenesManager;
            _config = config;
            _logger = logger;
        }

        private List<MonoBehaviour> _behavioursToInject = new();

        [AffinityPostfix]
		[AffinityPriority(Priority.High)]
		[AffinityPatch(typeof(SceneDecoratorContext), "GetInjectableMonoBehaviours")]
        private void PreventEnvironmentInjection(SceneDecoratorContext __instance, List<MonoBehaviour> monoBehaviours, DiContainer ____container)
        {
            var scene = __instance.gameObject.scene;
            if (_scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment)
            {
                _logger.Info($"Fixing bind conflicts on scene '{scene.name}'.");
                List<MonoBehaviour> removedBehaviours = new();

	            if (scene.name.Contains("Environment") && !scene.name.Contains("Multiplayer"))
                    removedBehaviours.AddRange(monoBehaviours.FindAll(behaviour => (behaviour is ZenjectBinding binding && binding.Components.Any(c => c is LightWithIdManager))));

                if (removedBehaviours.Any())
                {
                    string removedBehaviourStr = string.Join(", ", 
                        removedBehaviours.Select(behaviour => (behaviour is ZenjectBinding binding ? 
                            string.Join(", ", binding.Components.Select(comp => (comp.GetType() + " " + comp.gameObject.name))) : 
                            (behaviour.GetType() + " " + behaviour.gameObject.name))));

					_logger.Info($"Removing behaviours '{removedBehaviourStr}' from scene '{scene.name}'.");
                    monoBehaviours.RemoveAll(monoBehaviour => removedBehaviours.Contains(monoBehaviour));
                }

                if (scene.name.Contains("Environment") && !scene.name.Contains("Multiplayer"))
                {
                    _logger.Info($"Preventing environment injection.");
                    _behavioursToInject = new(monoBehaviours);
                    monoBehaviours.Clear();
                }
            }
            else
            {
                _behavioursToInject.Clear();
            }
        }

        private List<InstallerBase> _normalInstallers = new();
        private List<Type> _normalInstallerTypes = new();
        private List<ScriptableObjectInstaller> _scriptableObjectInstallers = new();
        private List<MonoInstaller> _monoInstallers = new();
        private List<MonoInstaller> _installerPrefabs = new();

        [AffinityPrefix]
		[AffinityPatch(typeof(SceneDecoratorContext), "InstallDecoratorInstallers")]
        private void PreventEnvironmentInstall(SceneDecoratorContext __instance, List<InstallerBase> ____normalInstallers, List<Type> ____normalInstallerTypes, List<ScriptableObjectInstaller> ____scriptableObjectInstallers, List<MonoInstaller> ____monoInstallers, List<MonoInstaller> ____installerPrefabs)
        {
            var scene = __instance.gameObject.scene;
            if (_scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment && scene.name.Contains("Environment") && !scene.name.Contains("Multiplayer"))
            {
                _logger.Info($"Preventing environment installation.");

                _normalInstallers = new(____normalInstallers);
                _normalInstallerTypes = new(____normalInstallerTypes);
                _scriptableObjectInstallers = new(____scriptableObjectInstallers);
                _monoInstallers = new(____monoInstallers);
                _installerPrefabs = new(____installerPrefabs);

                ____normalInstallers.Clear();
                ____normalInstallerTypes.Clear();
                ____scriptableObjectInstallers.Clear();
                ____monoInstallers.Clear();
                ____installerPrefabs.Clear();
            }
            else if (!_scenesManager.IsSceneInStack("MultiplayerEnvironment"))
            {
                _normalInstallers.Clear();
                _normalInstallerTypes.Clear();
                _scriptableObjectInstallers.Clear();
                _monoInstallers.Clear();
                _installerPrefabs.Clear();
            }
        }

		private List<GameObject> _objectsToEnable = new();

        [AffinityPrefix]
		[AffinityPatch(typeof(GameScenesManager), "ActivatePresentedSceneRootObjects")]
        private void PreventEnvironmentActivation(List<string> scenesToPresent)
        {
            _logger.Trace($"ScenesToPresent {string.Join(", ", scenesToPresent)}");
            string defaultScene = scenesToPresent.FirstOrDefault(scene => scene.Contains("Environment") && !scene.Contains("Multiplayer"));
            if (defaultScene != null)
            {
                if (scenesToPresent.Contains("MultiplayerEnvironment"))
                {
                    _logger.Info($"Preventing environment activation. ({defaultScene})");
                    _objectsToEnable = SceneManager.GetSceneByName(defaultScene).GetRootGameObjects().ToList();
                    scenesToPresent.Remove(defaultScene);
                }
                else
                {
                    // Make sure hud is enabled in solo
                    _logger.Trace("Ensuring HUD is enabled");
                    var sceneObjects = SceneManager.GetSceneByName(defaultScene).GetRootGameObjects().ToList();
                    foreach (GameObject gameObject in sceneObjects)
                    {
                        var hud = gameObject.transform.GetComponentInChildren<CoreGameHUDController>();
                        if (hud != null)
                            hud.gameObject.SetActive(true);
                    }
                }
            }
        }

        [AffinityPostfix]
		[AffinityPatch(typeof(GameObjectContext), "GetInjectableMonoBehaviours")]
        private void InjectEnvironment(GameObjectContext __instance, List<MonoBehaviour> monoBehaviours)
        {
	        if (__instance.transform.name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
            {
                _logger.Info($"Injecting environment.");
                monoBehaviours.AddRange(_behavioursToInject);
            }
        }

		// Fixes for Chromas TrackLaneRingInjection, see https://github.com/Aeroluna/Heck/blob/027ac8fc435afba7642aed57a251f7b991f32221/Chroma/HarmonyPatches/EnvironmentComponent/RingAwakeInstantiator.cs#L69
		[AffinityPrefix]
        [AffinityPatch(typeof(DiContainer), nameof(DiContainer.QueueForInject))]
        private bool IHateChromaTrackLaneRingInjection(DiContainer __instance,
	        ref object instance)
        {
	        if (_scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment && instance is LightPairRotationEventEffect lightPair)
	        {
		        _logger.Trace($"Preventing TrackLaneRing {lightPair.name} injection, parent go name: {lightPair.transform.parent.gameObject.name}");
		        lightPair.transform.parent.gameObject.SetActive(false);

				return false;
	        }

	        return true;
        }

        [AffinityPrefix]
		[AffinityPatch(typeof(Context), "InstallInstallers", AffinityMethodType.Normal, null, typeof(List<InstallerBase>), typeof(List<Type>), typeof(List<ScriptableObjectInstaller>), typeof(List<MonoInstaller>), typeof(List<MonoInstaller>))]
        private void InstallEnvironment(Context __instance, List<InstallerBase> normalInstallers, List<Type> normalInstallerTypes, List<ScriptableObjectInstaller> scriptableObjectInstallers, List<MonoInstaller> installers, List<MonoInstaller> installerPrefabs)
        {
            if (__instance is GameObjectContext instance && __instance.transform.name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
            {
                _logger.Info($"Installing environment.");
                normalInstallers.AddRange(_normalInstallers);
                normalInstallerTypes.AddRange(_normalInstallerTypes);
                scriptableObjectInstallers.AddRange(_scriptableObjectInstallers);
                installers.AddRange(_monoInstallers);
                installerPrefabs.AddRange(_installerPrefabs);
            }
        }

       
        [AffinityPrefix]
		[AffinityPatch(typeof(GameObjectContext), "InstallInstallers")]
        private void LoveYouCountersPlus(GameObjectContext __instance)
        {
            if (__instance.transform.name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
            {
                DiContainer container = __instance.GetProperty<DiContainer, GameObjectContext>("Container");
                var hud = (CoreGameHUDController)_behavioursToInject.Find(x => x is CoreGameHUDController);
                container.Unbind<CoreGameHUDController>();
                container.Bind<CoreGameHUDController>().FromInstance(hud).AsSingle();
                var multihud = __instance.transform.GetComponentInChildren<CoreGameHUDController>();
                multihud.gameObject.SetActive(false);
                var multiPositionHud = __instance.transform.GetComponentInChildren<MultiplayerPositionHUDController>();
                multiPositionHud.transform.position += new Vector3(0, 0.01f, 0);
            }
        }

        [AffinityPostfix]
		[AffinityPatch(typeof(GameObjectContext), "InstallSceneBindings")]
        private void ActivateEnvironment(GameObjectContext __instance)
        {
            if (__instance.transform.name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
            {
                _logger.Info($"Activating environment.");
                foreach (GameObject gameObject in _objectsToEnable)
                {
                    _logger.Trace($"Enabling GameObject: {gameObject.name}");
					gameObject.SetActive(true);
				}

				var activeObjects = __instance.transform.Find("IsActiveObjects");
                activeObjects.Find("Lasers").gameObject.SetActive(false);
                activeObjects.Find("Construction").gameObject.SetActive(false);
                activeObjects.Find("BigSmokePS").gameObject.SetActive(false);
                activeObjects.Find("DustPS").gameObject.SetActive(false);
                activeObjects.Find("DirectionalLights").gameObject.SetActive(false);

                var localActivePlayer = __instance.transform.GetComponent<MultiplayerLocalActivePlayerFacade>();
                var activeOnlyGameObjects = localActivePlayer.GetField<GameObject[], MultiplayerLocalActivePlayerFacade>("_activeOnlyGameObjects");
                var newActiveOnlyGameObjects = activeOnlyGameObjects.Concat(_objectsToEnable);
                localActivePlayer.SetField("_activeOnlyGameObjects", newActiveOnlyGameObjects.ToArray());
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Context), "InstallSceneBindings")]
        private static void HideOtherPlayerPlatforms(Context __instance)
        {
            if (__instance.transform.name.Contains("ConnectedPlayer"))
            {
                if (Plugin.Config.DisableMultiplayerPlatforms)
                    __instance.transform.Find("Construction").gameObject.SetActive(false);
                if (Plugin.Config.DisableMultiplayerLights)
                    __instance.transform.Find("Lasers").gameObject.SetActive(false);
            }
        }

        [HarmonyPrefix]
		[HarmonyPatch(typeof(EnvironmentSceneSetup), nameof(EnvironmentSceneSetup.InstallBindings))]
        private static bool RemoveDuplicateInstalls(EnvironmentSceneSetup __instance)
        {
            DiContainer container = __instance.GetProperty<DiContainer, MonoInstallerBase>("Container");
            return !container.HasBinding<EnvironmentBrandingManager.InitData>();
        }

        [AffinityPostfix]
		[AffinityPatch(typeof(GameplayCoreInstaller), nameof(GameplayCoreInstaller.InstallBindings))]
        private void LightInjectionFixes(GameplayCoreInstaller __instance)
        {
	        if (!_config.SoloEnvironment || !_scenesManager.IsSceneInStack("MultiplayerEnvironment"))
	        {
                _logger.Debug("Either SoloEnvironment disabled or MultiplayerEnvironment not in scene stack, returning");
		        return;
			}
	        _logger.Debug("Running SetEnvironmentColors Patch");

			DiContainer container = __instance.GetProperty<DiContainer, MonoInstallerBase>("Container");

			var trackLaneRingsManagers = _objectsToEnable.SelectMany(gameObject =>
				gameObject.transform.GetComponentsInChildren<TrackLaneRingsManager>());

			foreach (var trackLaneRingsManager in trackLaneRingsManagers)
			{
				if (trackLaneRingsManager == null)
                    continue;

				foreach (var rings in trackLaneRingsManager.Rings)
				{
                    if (rings == null)
                        continue;

                    _logger.Trace($"Fixing injection and enabling go {rings.gameObject.name}");

                    List<MonoBehaviour> injectables = new();
                    ZenUtilInternal.GetInjectableMonoBehavioursUnderGameObject(rings.gameObject, injectables);
                    foreach (var behaviour in injectables) container.Inject(behaviour);
                    rings.gameObject.SetActive(true);
				}
			}



            var colorManager = container.Resolve<EnvironmentColorManager>();
            container.Inject(colorManager);
            colorManager.Awake();

			var lightSwitchEventEffects = _objectsToEnable.SelectMany(gameObject => gameObject.transform.GetComponentsInChildren<LightSwitchEventEffect>());

            if (lightSwitchEventEffects == null || lightSwitchEventEffects.Count() == 0)
            {
                _logger.Warn("Could not get LightSwitchEventEffect, continuing");
            }
            else
            {
				foreach (var component in lightSwitchEventEffects)
				{
					// We have to set this manually since BG moved the below into Start() which we can't call without causing a nullref
					component._usingBoostColors = false;
					Color color = (component._lightOnStart ? component._lightColor0 : component._lightColor0.color.ColorWithAlpha(component._offColorIntensity));
					Color color2 = (component._lightOnStart ? component._lightColor0Boost : component._lightColor0Boost.color.ColorWithAlpha(component._offColorIntensity));
					component._colorTween = new ColorTween(color, color, new Action<Color>(component.SetColor), 0f, EaseType.Linear, 0f);
					component.SetupTweenAndSaveOtherColors(color, color, color2, color2);
				}
			}
        }
    }
}
