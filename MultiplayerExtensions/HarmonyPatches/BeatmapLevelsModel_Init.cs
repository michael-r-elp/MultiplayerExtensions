using HarmonyLib;
using MultiplayerExtensions.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerExtensions.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapLevelsModel), "Init", MethodType.Normal)]
    internal class BeatmapLevelsModel_Init
    {
        void Postfix(BeatmapLevelDataLoaderSO ____beatmapLevelDataLoader, IBeatmapDataAssetFileModel ____beatmapDataAssetFileModel,
            ref BeatmapLevelLoader ____beatmapLevelLoader)
        {
            //____beatmapLevelLoader = new MpExBeatmapLevelLoader(____beatmapLevelDataLoader, ____beatmapDataAssetFileModel);
        }
    }
}
