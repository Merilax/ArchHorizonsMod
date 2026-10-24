using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArchEmperorLib;
using BepInEx;
using Fusion;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace ArchHorizonsMod;

public class BundleParser
{
	public static Dictionary<int, ModDataStructs.ModDefinition> pendingLoadModDefs = [];
	public static Dictionary<int, ModDataStructs.ModDefinition> loadedModDefs = [];
	public static List<BundleRecord> loadedModRecords = [];
	public static Dictionary<int, string> GPIDtoBundleName = [];
	public static Dictionary<int, List<int>> BundleIDtoGPIDs = []; // Bundle ID : [GP IDs]
	public static Dictionary<int, GrandprixDef> GPIDtoGPDef = []; //  GP ID : GP Def
	public static Dictionary<int, ModDataStructs.CustomGPData> GPIDtoCustomGPData = []; //  GP ID : GP Def
	public static Dictionary<string, int> modTrackPlaceToId = new()
	{
		{"Grass_Day", 0},
		{"Grass_Night", 1},
		{"Canyon_Day_Layout01", 2},
		{"Canyon_Day_Layout02", 3},
		{"Canyon_Day_Battle", 4},
		{"Cyber_Racing01", 5},
		{"Cyber_Racing02", 6},
		{"Cyber_Battle", 7},
		{"Sky_Racing01", 8},
		{"Sky_Battle", 9},
		{"Ocean_Racing01", 10},
		{"Ocean_Racing02", 11},
		{"Ocean_Battle", 12},
		{"Ocean_Battle_Night", 13},
		{"Canyon_Night_Layout01", 14},
		{"Canyon_Night_Layout02", 15},
		{"Canyon_Night_Battle", 16},
		{"Moon_Battle", 17},
		{"Labo", 18},
		{"Sky_Racing02", 19}, // Buffer, unused.
		{"Test", 20}, // Buffer, unused.	
	};
	public static Dictionary<string, int> modTrackLandToId = new()
	{
		{"TestPlay", 0},
		{"Grass", 1},
		{"Canyon", 2},
		{"CloudSea", 3},
		{"Sea", 4},
		{"Cyber", 5},
		{"Moon", 6},
		{"Labo", 7},
	};
	public static Dictionary<string, int> circuitImageToId = new()
	{
		{"c01", 0},
		{"c02", 1},
		{"c03", 2},
		{"c04", 3},
		{"c05", 4},
		{"c06", 5},
		{"c07", 6},
		{"c08", 7},
		{"c09", 8},
		{"Unused_Test", 9},
	};
	public static Dictionary<string, int> fieldImageToId = new()
	{
		{"Lands_Grass_Day", 0},
		{"Lands_Canyon", 1},
		{"Lands_Cloud", 2},
		{"Lands_Sea", 3},
		{"Lands_Cyber", 4},
		{"Lands_Moon", 5},
		{"Unused_Test", 6},
	};

	public static Dictionary<int, ModDataStructs.TranslationTable> GPDescriptionTranslations = [];
	public static Dictionary<int, ModDataStructs.TranslationTable> PlaceNameTranslations = [];
	public static Dictionary<int, ModDataStructs.TranslationTable> LandNameTranslations = [];

	private static bool bundlesParsed = false;

	public static bool PreParseBundles()
	{
		if (bundlesParsed) return false;
		CheckFolderIntegrity();

		string trackFolder = Path.Combine(Paths.PluginPath, "ArchHorizonsMod", "AddonBundles");

		foreach (var folder in System.IO.Directory.EnumerateDirectories(trackFolder))
		{
			try
			{
				Plugin.LogInfo("Pre parsing bundle: " + folder);
				if (!File.Exists(Path.Combine(folder, "manifest.json")))
				{
					Plugin.Log.LogError("Could not find a manifest.json file in mod directory: " + folder);
					continue;
				}
				string jsonStr = File.ReadAllText(Path.Combine(folder, "manifest.json"));
				ModDataStructs.ModDefinition modDef = JsonSerializer.Deserialize<ModDataStructs.ModDefinition>(jsonStr, new JsonSerializerOptions() { IncludeFields = true });

				if (!File.Exists(Path.Combine(folder, modDef.bundleName + "_assets")))
				{
					Plugin.Log.LogError("Could not find mod bundle file at: " + Path.Combine(folder, modDef.bundleName + "_assets"));
					continue;
				}
				if (!File.Exists(Path.Combine(folder, modDef.bundleName + "_scenes")))
				{
					Plugin.Log.LogError("Could not find mod bundle file at: " + Path.Combine(folder, modDef.bundleName + "_scenes"));
					continue;
				}
				if (modDef.bundleName == null || modDef.bundleName.Length <= 0)
				{
					Plugin.Log.LogError("Manifest error: bundleName is empty.");
					continue;
				}
				if (modDef.grandprixDefs == null || modDef.grandprixDefs.Count <= 0)
				{
					Plugin.Log.LogError("Manifest error: grandprixDefs is empty.");
					continue;
				}

				loadedModRecords.Add(new(modDef.bundleName, modDef.bundleVersion));
			}
			catch (System.Exception ex)
			{
				Plugin.Log.LogError(ex);
			}
		}
		
		Plugin.LogInfo($"Detected {loadedModRecords.Count} circuit bundles.");

		return true;
	}

	public static bool ParseBundles()
	{
		if (bundlesParsed) return false;
		CheckFolderIntegrity();

		string trackFolder = Path.Combine(Paths.PluginPath, "ArchHorizonsMod", "AddonBundles");

		int modIdCount = 10; // GP ID. Each bundle gets its own ID. Difficulty is then used to sort the GPs per-bundle.
		int GPID = 100;

		{
			var allSprites = MatchmakingUI.ins.fieldImages.ToList();
			allSprites.Add(null);
			MatchmakingUI.ins.fieldImages = new(allSprites.ToArray());
			allSprites = MatchmakingUI.ins.circuitImages.ToList();
			allSprites.Add(null);
			MatchmakingUI.ins.circuitImages = new(allSprites.ToArray());
			allSprites = UI_SinglePlay.ins.CircuitSprites.ToList();
			allSprites.Add(null);
			UI_SinglePlay.ins.CircuitSprites = new(allSprites.ToArray());
		}

		foreach (var folder in System.IO.Directory.EnumerateDirectories(trackFolder))
		{
			Plugin.LogInfo("Parsing bundle: " + folder);
			if (!loadedModRecords.Any((e) => e.Name.ToLower() == folder.Split('\\').Last().ToLower()))
			{
				Plugin.Log.LogWarning("Bundle failed preloading earlier, skipping.");
				continue;
			}
			try
			{
				if (!File.Exists(Path.Combine(folder, "manifest.json")))
				{
					Plugin.Log.LogError("Could not find a manifest.json file in mod directory: " + folder);
					continue;
				}
				string jsonStr = File.ReadAllText(Path.Combine(folder, "manifest.json"));
				ModDataStructs.ModDefinition modDef = JsonSerializer.Deserialize<ModDataStructs.ModDefinition>(jsonStr, new JsonSerializerOptions() { IncludeFields = true });

				if (!File.Exists(Path.Combine(folder, modDef.bundleName + "_assets")))
				{
					Plugin.Log.LogError("Could not find mod bundle file at: " + Path.Combine(folder, modDef.bundleName + "_assets"));
					continue;
				}
				if (!File.Exists(Path.Combine(folder, modDef.bundleName + "_scenes")))
				{
					Plugin.Log.LogError("Could not find mod bundle file at: " + Path.Combine(folder, modDef.bundleName + "_scenes"));
					continue;
				}
				if (modDef.bundleName == null || modDef.bundleName.Length <= 0)
				{
					Plugin.Log.LogError("Manifest error: bundleName is empty.");
					continue;
				}
				if (modDef.grandprixDefs == null || modDef.grandprixDefs.Count <= 0)
				{
					Plugin.Log.LogError("Manifest error: grandprixDefs is empty.");
					continue;
				}

				// Register map assets

				// var sceneBundle = UniverseLib.AssetBundle.LoadFromFile(Path.Combine(folder, modDef.bundleName + "_scenes"));
				// UniverseLib.AssetBundle.LoadFromFile(Path.Combine(folder, modDef.bundleName + "_assets"));

				var map = new ResourceLocationMap($"{modDef.bundleName}ContentCatalogAssets", 300);
				var bundleLoc = new ResourceLocationBase(
					modDef.bundleName + "_assets",
					Path.Combine(folder, modDef.bundleName + "_assets"),
					typeof(AssetBundleProvider).FullName,
					Il2CppType.From(typeof(IAssetBundleResource)));

				map.Add(modDef.bundleName + "_assets", bundleLoc.Cast<IResourceLocation>());
				var deps = new Il2CppReferenceArray<IResourceLocation>([bundleLoc.Cast<IResourceLocation>()]);

				var mapScenes = new ResourceLocationMap($"{modDef.bundleName}ContentCatalogScenes", 100);
				var sceneLoc = new ResourceLocationBase(
					modDef.bundleName + "_scenes",
					Path.Combine(folder, modDef.bundleName + "_scenes"),
					typeof(AssetBundleProvider).FullName,
					Il2CppType.From(typeof(IAssetBundleResource)));

				mapScenes.Add(modDef.bundleName + "_scenes", sceneLoc.Cast<IResourceLocation>());
				var sceneDeps = new Il2CppReferenceArray<IResourceLocation>([sceneLoc.Cast<IResourceLocation>()]);//bundleLoc.Cast<IResourceLocation>(), 

				// Add scene thumbnails

				Dictionary<string, int> circuitThumbIndexes = [];
				foreach (var item in modDef.circuitThumbs)
				{
					RegisterAsset<Sprite>($"{modIdCount}_thumb_{item.name}", item.path, typeof(BundledAssetProvider), map, deps);
					circuitThumbIndexes.Add(item.name, MatchmakingUI.ins.fieldImages.Count);
					fieldImageToId.Add(item.name, fieldImageToId.Count);

					var allImages = SinglePlayData.AllCircuitsImgData.ToList();
					allImages.Add(new() { offset = new(0, 0), rot = 0, scale = 1024 });
					SinglePlayData.AllCircuitsImgData = new(allImages.ToArray());
				}

				// Add circuit layout images

				Dictionary<string, int> circuitTrackIndexes = [];
				foreach (var item in modDef.circuitTracks)
				{
					RegisterAsset<Sprite>($"{modIdCount}_track_{item.name}", item.path, typeof(BundledAssetProvider), map, deps);
					circuitTrackIndexes.Add(item.name, UI_SinglePlay.ins.CircuitSprites.Count);
					circuitImageToId.Add(item.name, circuitImageToId.Count);
				}

				// Register GP Land and Places with an unique ID each.

				List<ModDataStructs.NameTranslation> landNameTranslations = [];
				List<string> translatedLandNames = [];
				foreach (var item in modDef.landNameTranslations)
				{
					var landNameTranslation = JsonSerializer.Deserialize<ModDataStructs.NameTranslation>(item, new JsonSerializerOptions() { IncludeFields = true });
					landNameTranslations.Add(landNameTranslation);
				}

				List<ModDataStructs.NameTranslation> placeNameTranslations = [];
				List<string> translatedPlaceNames = [];
				foreach (var item in modDef.placeNameTranslations)
				{
					var placeNameTranslation = JsonSerializer.Deserialize<ModDataStructs.NameTranslation>(item, new JsonSerializerOptions() { IncludeFields = true });
					placeNameTranslations.Add(placeNameTranslation);
				}

				BundleIDtoGPIDs.Add(modIdCount, []);
				foreach (var item in modDef.grandprixDefs)
				{
					var gpdef = JsonSerializer.Deserialize<ModDataStructs.CustomGPData>(item, new JsonSerializerOptions() { IncludeFields = true });

					ModDataStructs.ModdedMatchData[] stages = gpdef._Stages.Concat(gpdef._Stages_Hard).ToArray();
					foreach (var stage in stages)
					{
						if (!modTrackLandToId.ContainsKey(stage._Land))
						{
							if (!modTrackLandToId.ContainsKey(stage._Land))
								modTrackLandToId.Add(stage._Land, modTrackLandToId.Count);

							landNameTranslations.DoIf(
								(e) => e.key == stage._Land && !translatedLandNames.Contains(e.key),
								(e) => LandNameTranslations.Add(modTrackLandToId[stage._Land], e.translatedName)
							);
						}

						if (!modTrackPlaceToId.ContainsKey(stage._Place))
						{
							if (!circuitThumbIndexes.ContainsKey(stage.CircuitThumbnail) || !circuitTrackIndexes.ContainsKey(stage.CircuitTrack))
							{
								Plugin.Log.LogError($"GrandPrix definition {gpdef._Name} contains an invalid thumbnail or track name.");
								continue;
							}

							modTrackPlaceToId.Add(stage._Place, modTrackPlaceToId.Count);

							// TODO Why does non-0 for circuitLayout cause the track to not load? // circuitLayout -> fieldImageToId[stage.CircuitThumbnail], OR fieldImageToId[stage.CircuitThumbnail] + 2??
							AddCustomPlace(stage._Place, modTrackPlaceToId[stage._Place], 0, circuitImageToId[stage.CircuitTrack], new(0, 0), 100);

							string scenePath = $"Assets/{modDef.bundleName}/Maps/{stage._Place}/{stage._Place}.unity";
							RegisterAsset<SceneInstance>(stage._Place, scenePath, typeof(BundledAssetProvider), mapScenes, sceneDeps);
							RegisterAsset<SceneInstance>(scenePath, scenePath, typeof(BundledAssetProvider), mapScenes, sceneDeps);
							AddressableSceneInjection.ScenePaths.Add(scenePath);
							MatchManager.ins.networkScenes.Add(stage._Place, SceneRef.FromPath(scenePath));

							placeNameTranslations.DoIf(
								(e) => e.key == stage._Place && !translatedPlaceNames.Contains(e.key),
								(e) => PlaceNameTranslations.Add(modTrackPlaceToId[stage._Place], e.translatedName)
							);
						}

						if (stage._Rules._Type == (GameVariable.MatchType)4 && !CustomMatchData.CustomFields_Battle.Any((e) => e._NameKey == modTrackPlaceToId[stage._Place]))
						{
							var customFieldData = CustomMatchData.CustomFields_Battle.ToList();
							customFieldData.Add(new()
							{
								_NameKey = modTrackPlaceToId[stage._Place],
								_CircuitSpriteNumber = circuitImageToId[stage.CircuitTrack] + 1,
								_Land = (VenueData.Landscape)modTrackLandToId[stage._Land],
								_Place = (SinglePlayData.MatchPlace)modTrackPlaceToId[stage._Place],
								_Time = stage._Time,
								_Suffix = 0, // Adds a number to the map name (Plains 1, Plains 2...)
							});
							CustomMatchData.CustomFields_Battle = customFieldData.ToArray();
						}
						// Must be duplicated to avoid pointer magic
						if (stage._Rules._Type != (GameVariable.MatchType)4 && !CustomMatchData.CustomFields_Racing.Any((e) => e._NameKey == modTrackPlaceToId[stage._Place]))
						{
							var customFieldData = CustomMatchData.CustomFields_Racing.ToList();
							customFieldData.Add(new()
							{
								_NameKey = modTrackPlaceToId[stage._Place], // ok (-1 is Labo "???")
								_CircuitSpriteNumber = circuitImageToId[stage.CircuitTrack] + 1, // There's a missing circuitImage, so we inject it earlier.
								_Land = (VenueData.Landscape)modTrackLandToId[stage._Land], // Same here. This way we don't need a -1 here, which would set the GP to the wrong Land.
								_Place = (SinglePlayData.MatchPlace)modTrackPlaceToId[stage._Place], // Again.
								_Time = stage._Time,
								_Suffix = 0,
							});
							CustomMatchData.CustomFields_Racing = customFieldData.ToArray();
						}
					}
					gpdef._Id = GPID;
					GPDescriptionTranslations.Add(GPID + 1, gpdef.translatedDescription);

					List<GrandprixDef> gpDatas = MatchManager.ins._GrandprixData.grandprixDatas.ToList();
					var grandprixDef = ModDataStructs.ParseGP(gpdef, modTrackLandToId, modTrackPlaceToId);
					gpDatas.Add(grandprixDef);
					MatchManager.ins._GrandprixData.grandprixDatas = new Il2CppReferenceArray<GrandprixDef>(gpDatas.ToArray());

					GPIDtoBundleName.Add(GPID, modDef.bundleName);
					GPIDtoGPDef.Add(GPID, grandprixDef);
					GPIDtoCustomGPData.Add(GPID, gpdef);
					BundleIDtoGPIDs[modIdCount].Add(GPID);
					GPID++;
				}

				pendingLoadModDefs.Add(modIdCount, modDef);
				modIdCount++;

				Addressables.AddResourceLocator(map.Cast<IResourceLocator>());
				Addressables.AddResourceLocator(mapScenes.Cast<IResourceLocator>());

				// sceneBundle.Unload(false); // false keeps already-loaded assets alive
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Unexpected error while loading mod bundle: " + folder);
				Plugin.Log.LogError(ex);
				continue;
			}
		}

		// var tempScene = Addressables.LoadSceneAsync("D72", UnityEngine.SceneManagement.LoadSceneMode.Additive, false).WaitForCompletion();
		// tempScene.ReleaseHandleOnCompletion();
		// tempScene.WaitForCompletion();
		// tempScene = null;
		// Addressables.UnloadSceneAsync(tempScene);

		modIdCount = 10;
		foreach (var modDef in pendingLoadModDefs)
		{
			foreach (var item in modDef.Value.circuitThumbs)
			{
				// var allImages = SinglePlayData.AllCircuitsImgData.ToList();
				// allImages.Add(new() { offset = new(0, 0), rot = 0, scale = 1024 });
				// SinglePlayData.AllCircuitsImgData = new(allImages.ToArray());

				Sprite sprite = Addressables.LoadAssetAsync<Sprite>($"{modIdCount}_thumb_{item.name}").WaitForCompletion();
				if (!sprite)
				{
					Plugin.Log.LogError($"Incorrect asset path \"{item.name}\" in mod manifest: " + item.path);
					continue;
				}
				var allSprites = MatchmakingUI.ins.fieldImages.ToList();
				allSprites.Add(sprite);
				MatchmakingUI.ins.fieldImages = new(allSprites.ToArray());
			}

			foreach (var item in modDef.Value.circuitTracks)
			{
				Sprite sprite = Addressables.LoadAssetAsync<Sprite>($"{modIdCount}_track_{item.name}").WaitForCompletion();
				if (!sprite)
				{
					Plugin.Log.LogError($"Incorrect asset path \"{item.name}\" in mod manifest: " + item.path);
					continue;
				}
				var allImages = UI_SinglePlay.ins.CircuitSprites.ToList();
				allImages.Add(sprite);
				UI_SinglePlay.ins.CircuitSprites = new(allImages.ToArray());

				var allSprites = MatchmakingUI.ins.circuitImages.ToList();
				allSprites.Add(sprite);
				MatchmakingUI.ins.circuitImages = new(allSprites.ToArray());
			}

			loadedModDefs.Add(modDef.Key, modDef.Value);
			modIdCount++;
		}

		Plugin.LogInfo($"Loaded {loadedModDefs.Count} circuit bundles.");
		bundlesParsed = true;
		return true;
	}

	private static bool CheckFolderIntegrity()
	{
		bool flag = true;
		if (!Directory.Exists(Path.Combine(Paths.PluginPath, "ArchHorizonsMod")))
		{
			try { Directory.CreateDirectory(Path.Combine(Paths.PluginPath, "ArchHorizonsMod")); }
			catch (System.Exception ex) { Plugin.Log.LogError(ex); }
			flag = false;
		}
		if (!Directory.Exists(Path.Combine(Paths.PluginPath, "ArchHorizonsMod", "AddonBundles")))
		{
			try { Directory.CreateDirectory(Path.Combine(Paths.PluginPath, "ArchHorizonsMod", "AddonBundles")); }
			catch (System.Exception ex) { Plugin.Log.LogError(ex); }
			flag = false;
		}

		return flag;
	}

	private static void RegisterAsset<T>(string key, string path, Type provider, ResourceLocationMap map, Il2CppReferenceArray<IResourceLocation> deps = null)
	{
		var assetLoc = new ResourceLocationBase(
			key,
			path,
			provider.FullName,
			Il2CppType.Of<T>(),
			deps);

		map.Add(key, assetLoc.Cast<IResourceLocation>());
	}

	private static void AddCustomPlace(string sceneName, int trackNumber, int circuitLayout, int circuitImage, Vector2 respawnCenter, float respawnRadius)
	{
		IntPtr klassPtr = Il2CppClassPointerStore<d_MatchPlace>.NativeClassPtr;
		IntPtr rawObj = IL2CPP.il2cpp_object_new(klassPtr);

		unsafe
		{
			byte* p = (byte*)rawObj;
			// _SceneName, struct offset 0x0 -> absolute 16
			*(int*)(p + 16) = sceneName.Length;
			uint* chars = (uint*)(p + 20);
			for (int i = 0; i < 32; i++)
				chars[i] = i < sceneName.Length ? sceneName[i] : 0u;
			// _CircuitLayoutNumber, 0x84 -> absolute 148
			*(int*)(p + 148) = circuitLayout;
			// _CircuitImgNumber, 0x88 -> absolute 152
			*(int*)(p + 152) = circuitImage;
			// _RespawnCenter, 0x8C -> absolute 156 / 160
			*(float*)(p + 156) = respawnCenter.x;
			*(float*)(p + 160) = respawnCenter.y;
			// _RespawnRadius, 0x94 -> absolute 164
			*(float*)(p + 164) = respawnRadius;
		}

		AddMatchPlaceRaw((SinglePlayData.MatchPlace)trackNumber, rawObj);
	}

	private static unsafe void AddMatchPlaceRaw(SinglePlayData.MatchPlace key, IntPtr valueRawObj)
	{
		IntPtr dictNative = ((Il2CppObjectBase)(object)SinglePlayData.AllMatchPlace).Pointer;
		IntPtr dictKlass = IL2CPP.il2cpp_object_get_class(dictNative);
		IntPtr addMethod = IL2CPP.il2cpp_class_get_method_from_name(dictKlass, "Add", 2);

		int keyVal = (int)key;
		void* valuePtr = (void*)(valueRawObj + 16);

		void*[] args = [&keyVal, valuePtr];
		fixed (void** argsPin = args)
		{
			IntPtr exc = IntPtr.Zero;
			IL2CPP.il2cpp_runtime_invoke(addMethod, dictNative, argsPin, ref exc);
			if (exc != IntPtr.Zero)
				Il2CppException.RaiseExceptionIfNecessary(exc);
		}
	}

	public static void PopulateLocalization()
	{
		Plugin.LogInfo("Registering GP translations...");
		var strDB = LocalizationSettings.Instance.GetStringDatabase();
		var tableChineseSimplified = strDB.GetTable("Single", LocalizationSettings.AvailableLocales.Locales[0]);
		var tableChineseTraditional = strDB.GetTable("Single", LocalizationSettings.AvailableLocales.Locales[1]);
		var tableEnglish = strDB.GetTable("Single", LocalizationSettings.AvailableLocales.Locales[2]);
		var tableJapanese = strDB.GetTable("Single", LocalizationSettings.AvailableLocales.Locales[3]);

		foreach (var desc in GPDescriptionTranslations)
		{
			Plugin.Log.LogDebug("Registering translations for GP " + desc.Key);
			string id = "spd" + desc.Key;

			tableChineseSimplified.AddEntry(id, desc.Value.English);
			tableChineseTraditional.AddEntry(id, desc.Value.TraditionalChinese);
			tableEnglish.AddEntry(id, desc.Value.English);
			tableJapanese.AddEntry(id, desc.Value.Japanese);
		}
		foreach (var place in PlaceNameTranslations)
		{
			Plugin.Log.LogDebug("Registering translations for Place " + place.Key);
			string id = "spm" + place.Key;

			tableChineseSimplified.AddEntry(id, place.Value.English);
			tableChineseTraditional.AddEntry(id, place.Value.TraditionalChinese);
			tableEnglish.AddEntry(id, place.Value.English);
			tableJapanese.AddEntry(id, place.Value.Japanese);
		}
		foreach (var place in LandNameTranslations)
		{
			Plugin.Log.LogDebug("Registering translations for Land " + place.Key);
			string id = "spland" + place.Key;

			tableChineseSimplified.AddEntry(id, place.Value.English);
			tableChineseTraditional.AddEntry(id, place.Value.TraditionalChinese);
			tableEnglish.AddEntry(id, place.Value.English);
			tableJapanese.AddEntry(id, place.Value.Japanese);
		}
	}
}

internal static class AddressableSceneInjection
{
	internal static readonly HashSet<string> ScenePaths = new();

	[HarmonyPatch(typeof(NetworkSceneManagerDefault), nameof(NetworkSceneManagerDefault.TryGetAddressableScenes))]
	[HarmonyPostfix]
	private static void TryGetAddressableScenes(ref bool __result, ref Il2CppStringArray addressableScenes)
	{
		if (ScenePaths.Count == 0) return;

		var merged = new List<string>();
		if (__result && addressableScenes != null)
			foreach (var s in addressableScenes) merged.Add(s);
		foreach (var p in ScenePaths)
			if (!merged.Contains(p)) merged.Add(p);

		addressableScenes = new Il2CppStringArray(merged.ToArray());
		__result = true;
	}
}