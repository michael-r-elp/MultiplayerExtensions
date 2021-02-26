using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities;
using Zenject;

namespace MultiplayerExtensions.OverrideClasses
{
    public class MultiplayerBeatmapLevelsModel : BeatmapLevelsModel
    {
        private BeatmapLevelsModel _baseLevelsModel = null!;
        [Inject]
        protected BeatmapLevelsModel BaseLevelsModel
        {
            get => _baseLevelsModel;
            set
            {
                _baseLevelsModel = value;
                CloneBase(value);
                //this._dlcLevelPackCollectionContainer =
                //    value.GetField<BeatmapLevelPackCollectionContainerSO, BeatmapLevelsModel>(nameof(_dlcLevelPackCollectionContainer));
                //this._ostAndExtrasPackCollection =
                //    value.GetField<BeatmapLevelPackCollectionSO, BeatmapLevelsModel>(nameof(_ostAndExtrasPackCollection));
                //this._beatmapLevelDataLoader =
                //    value.GetField<BeatmapLevelDataLoaderSO, BeatmapLevelsModel>(nameof(_beatmapLevelDataLoader));
                //this._customLevelPackCollection =
                //    value.GetField<IBeatmapLevelPackCollection, BeatmapLevelsModel>(nameof(_customLevelPackCollection));
            }
        }

        private void CloneBase(BeatmapLevelsModel blm)
        {
            Plugin.DebugLog("Cloning base into MultiplayerBeatmapLevelsModel");
            foreach (var field in typeof(BeatmapLevelsModel).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                //Plugin.DebugLog($"Cloning {field.Name}");
                field.SetValue(this, field.GetValue(blm));
            }
        }

        public override async Task<GetBeatmapLevelResult> GetBeatmapLevelAsync(string levelID, CancellationToken cancellationToken)
        {
            Plugin.DebugLog($"MultiplayerBeatmapLevelsModel: GetBeatmapLevelAsync");
            CloneBase(BaseLevelsModel);
            string? hash = Utilities.Utils.LevelIdToHash(levelID);
            if (hash != null && !SongCore.Collections.songWithHashPresent(hash))
            {
                Plugin.DebugLog($"MultiplayerBeatmapLevelsModel: Song does not exist, attempting to download.");
                await Downloader.TryDownloadSong(levelID, cancellationToken);
            }
            CloneBase(BaseLevelsModel);
            var result = await BaseLevelsModel.GetBeatmapLevelAsync(levelID, cancellationToken);
            if (result.isError)
            {
                Plugin.Log.Warn("GetBeatmapLevelAsync returned with an error, falling back to base.");
                result = await base.GetBeatmapLevelAsync(levelID, cancellationToken);
            }
            if (result.isError)
            {

                Plugin.Log.Warn("GetBeatmapLevelAsync returned with an error.");
            }
            else
                Plugin.DebugLog($"GetBeatmapLevelAsync returned: {result.beatmapLevel.songName} by {result.beatmapLevel.levelAuthorName}");
            return result;
        }
    }
}
