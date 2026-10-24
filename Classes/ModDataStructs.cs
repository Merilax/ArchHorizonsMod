using System.Collections.Generic;

namespace ArchHorizonsMod;

public class ModDataStructs
{
	public class TranslationTable
	{
		public string English;
		public string Japanese;
		public string SimplifiedChinese;
		public string TraditionalChinese;
	}

	public class ModDefinition
	{
		public string bundleName = "";
		public string bundleVersion = "";
		public List<string> grandprixDefs = [];
		public List<string> landNameTranslations = [];
		public List<string> placeNameTranslations = [];
		public List<AssetPaths> circuitThumbs = new();
		public List<AssetPaths> circuitTracks = new();
	}
	public class AssetPaths
	{
		public string name, path;
	}

	public class CustomGPData
	{
		public string _Name;
		public int _Id;
		public int _Difficulty;
		public float _PrizeMultiplier;
		public string[] _EnemyCodes;
		public float[] _EnemyLevels;
		public float[] _EnemyLevels_Hard;
		public ModdedMatchData[] _Stages;
		public ModdedMatchData[] _Stages_Hard;
		public string _ForceMachineKey;
		public TranslationTable translatedDescription;
	}

	public class ModdedMatchData
	{
		public CustomMatchData.d_Rules _Rules;
		public VenueData.Energy _Energy;
		public VenueData.Grav _Grav;
		public VenueData.Temperature _Temp;
		public VenueData.Time _Time;
		public VenueData.VenueLayout _Layout;
		public string _Land;
		public string _Place;
		public string CircuitThumbnail;
		public string CircuitTrack;

	}

	public class NameTranslation
	{
		public string key;
		public TranslationTable translatedName;
	}

	public static GrandprixDef ParseGP(CustomGPData gp, Dictionary<string, int> modTrackLandToId, Dictionary<string, int> modTrackPlaceToId)
	{
		List<CustomMatchData.MatchData> stages = [];
		List<CustomMatchData.MatchData> stagesHard = [];

		foreach (var item in gp._Stages){
			stages.Add(ParseStage(item, modTrackLandToId[item._Land], modTrackPlaceToId[item._Place]));}
		foreach (var item in gp._Stages_Hard){
			stagesHard.Add(ParseStage(item, modTrackLandToId[item._Land], modTrackPlaceToId[item._Place]));}

		GrandprixDef gpdef = new()
		{
			name = $"ArchGP_{gp._Name}",
			_Name = gp._Name,
			_Difficulty = gp._Difficulty,
			_EnemyCodes = gp._EnemyCodes,
			_EnemyLevels = gp._EnemyLevels,
			_EnemyLevels_Hard = gp._EnemyLevels_Hard,
			_ID = gp._Id,
			_PrizeMultiplier = gp._PrizeMultiplier,
			_ForceMachineKey = gp._ForceMachineKey,
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
			_Energy = data._Energy,
			_Grav = data._Grav,
			_IsSet = true,
			_Layout = data._Layout,
			_Rules = data._Rules,
			_Temp = data._Temp,
			_Time = data._Time,
			_Land = (VenueData.Landscape)land,
			_Place = (SinglePlayData.MatchPlace)place,
		};

		return stage;
	}
}