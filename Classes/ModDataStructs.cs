using System.Collections.Generic;
using System.Linq;

namespace ArchHorizonsMod;

public class ModDataStructs
{
	public class ImportedModDefinition
	{
		public string bundleName = "";
		public string bundleVersion = "";
		public List<string> landNameTranslations = [];
		public List<string> placeNameTranslations = [];
		public List<string> landGroups = [];
		public List<string> customGPs = [];
		public List<AssetPaths> circuitThumbs = [];
		public List<AssetPaths> circuitTracks = [];
	}

	public class ModDefinition
	{
		public string bundleName = "";
		public string bundleVersion = "";
		public List<NameTranslation> landNameTranslations = [];
		public List<NameTranslation> placeNameTranslations = [];
		public List<CustomGPData> customGPs = [];
		public List<LandGroup> landGroups = [];
		public List<AssetPaths> circuitThumbs = [];
		public List<AssetPaths> circuitTracks = [];
	}

	public class CustomGPData
	{
		public string GPName;
		public int id = 0;
		public int difficulty;
		// public float _PrizeMultiplier;
		public string[] enemyCodes;
		public float[] enemyLevels;
		public float[] enemyLevelsHard;
		public ModdedMatchData[] stages;
		public ModdedMatchData[] stagesHard;
		public string forceMachineKey;
		public TranslationTable translatedDescription;
		// public GrandprixDef GPDef = null;
	}

	public class ModdedMatchData
	{
		public CustomMatchData.d_Rules rules;
		public VenueData.Energy energy;
		public VenueData.Grav gravity;
		public VenueData.Temperature temperature;
		public VenueData.Time time;
		public VenueData.VenueLayout layoutType;
		public string placeKey;
		public string imageKey;
	}

	public class LandGroup
	{
		public string land;
		public string imageKey;
		public string[] placeKeys;
	}

	public class AssetPaths
	{
		public string name, path;
	}
	public class NameTranslation
	{
		public string key;
		public TranslationTable translatedName;
	}
	public class TranslationTable
	{
		public string English, Japanese, SimplifiedChinese, TraditionalChinese;
	}

	public static GrandprixDef ParseGP(List<LandGroup> landPlaces, CustomGPData gp, Dictionary<string, int> modTrackLandToId, Dictionary<string, int> modTrackPlaceToId)
	{
		List<CustomMatchData.MatchData> stages = [];
		List<CustomMatchData.MatchData> stagesHard = [];

		foreach (var item in gp.stages)
		{
			string landKey = GetLandByPlaceKey(landPlaces, item.placeKey);
			stages.Add(ParseStage(item, modTrackLandToId[landKey], modTrackPlaceToId[item.placeKey]));
		}
		foreach (var item in gp.stagesHard)
		{
			string landKey = GetLandByPlaceKey(landPlaces, item.placeKey);
			stagesHard.Add(ParseStage(item, modTrackLandToId[landKey], modTrackPlaceToId[item.placeKey]));
		}

		GrandprixDef gpdef = new()
		{
			name = $"ArchGP_{gp.GPName}",
			_Name = gp.GPName,
			_Difficulty = gp.difficulty,
			_EnemyCodes = gp.enemyCodes,
			_EnemyLevels = gp.enemyLevels,
			_EnemyLevels_Hard = gp.enemyLevelsHard,
			_ID = gp.id,
			_PrizeMultiplier = 1,//gp._PrizeMultiplier,
			_ForceMachineKey = gp.forceMachineKey,
			// _ScoreAttackTargets = gp._ScoreAttackTargets,
			_TutorialDialogType = GrandprixDef.TutorialDialogType.None,
			_Stages = stages.ToArray(),
			_Stages_Hard = stagesHard.ToArray(),
		};

		return gpdef;
	}

	public static CustomMatchData.MatchData ParseStage(ModdedMatchData data, int land, int place)
	{
		CustomMatchData.MatchData stage = new()
		{
			_Energy = data.energy,
			_Grav = data.gravity,
			_IsSet = true,
			_Layout = data.layoutType,
			_Rules = data.rules,
			_Temp = data.temperature,
			_Time = data.time,
			_Land = (VenueData.Landscape)land,
			_Place = (SinglePlayData.MatchPlace)place,
		};

		return stage;
	}

	public static string GetLandByPlaceKey(List<LandGroup> landPlaces, string placeKey)
	{
		try
		{
			return landPlaces.First(e => e.placeKeys.Contains(placeKey)).land;
		}
		catch (System.Exception)
		{
			Plugin.Log.LogWarning("No Land associated with Place " + placeKey);
			return "Grass";
		}
	}
}