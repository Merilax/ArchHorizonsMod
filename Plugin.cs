using System.IO;
using ArchEmperorLib;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace ArchHorizonsMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BREAK ARTS III.exe")]
[BepInDependency("ArchEmperorLib", BepInDependency.DependencyFlags.HardDependency)]

public class Plugin : BasePlugin
{
	internal static new ManualLogSource Log;
	public static ConfigFile config;
	public static UniverseLib.AssetBundle assets;
	public override void Load()
	{
		Log = BepInEx.Logging.Logger.CreateLogSource("ArchHorizonsMod");
		var harmony = Harmony.CreateAndPatchAll(typeof(Main));
		harmony.PatchAll(typeof(UI));
		harmony.PatchAll(typeof(AddressableSceneInjection));

		string bundlePath = Path.Combine(Paths.PluginPath, "ArchHorizonsMod", "archhorizons_ui");
		if (!File.Exists(bundlePath))
			Log.LogError($"Bundle not found at: {bundlePath}");
		else
		{
			assets = UniverseLib.AssetBundle.LoadFromFile(bundlePath);
			if (assets == null) Log.LogError("Failed to load assets!");
		}

		BundleParser.PreParseBundles();
		ModRegistry.Register(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION, RequirementScope.Everyone, VersionStrictness.Exact, () => BundleParser.loadedModRecords);
	}
	public static void LogInfo(object data) => Log.LogInfo(data);
}

public class Main
{
	private static CreditBlock MakeCredits()
	{
		CreditBlock creditData = new();
		if (Plugin.assets)
			creditData.AddLogo(Plugin.assets.LoadAsset<Sprite>("HorizonsModLogo"));
		creditData.AddText(CreditBlock.FONT_SIZE.TITLE, "ARCHITECT'S HORIZONS MOD");
		creditData.AddSeparator();
		creditData.AddText(CreditBlock.FONT_SIZE.ROLE, "DEVELOPER");
		creditData.AddText(CreditBlock.FONT_SIZE.NORMAL, "Merilax");
		return creditData;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadSceneAsync), [typeof(string), typeof(LoadSceneMode)])]
	public static void LoadSceneAsync(string sceneName, ref AsyncOperation __result, ref bool __runOriginal)
	{
		if (BundleParser.modTrackPlaceIDs.ContainsKey(sceneName))
		{
			__runOriginal = false;
			__result = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive, activateOnLoad: false).WaitForCompletion().m_Operation;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Scene_MainMenu), nameof(Scene_MainMenu.Start))]
	public static void MainStart()
	{
		BundleParser.ParseBundles();
		BundleParser.PopulateLocalization();
		LocalizationSettings.add_SelectedLocaleChanged((Il2CppSystem.Action<UnityEngine.Localization.Locale>)((locale) => BundleParser.PopulateLocalization()));

		ModManager.SetCredits(MyPluginInfo.PLUGIN_GUID, MakeCredits());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetGpKey))]
	public static void GetGpKey(GrandprixDataContainer __instance, int index, ref bool __runOriginal, ref string __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (!BundleParser.GPIDtoGPDef.ContainsKey(index))
				Plugin.Log.LogError($"Tried retrieving GPDef for GP UUID {index}, but couldn't find it.");
			else
			{
				string gpName = BundleParser.GPIDtoGPDef[index]._Name;
				string bundleName = BundleParser.GPIDtoBundleName[index];
				__result = $"ArchGP_{bundleName}_{(GameVariable.IsHardMode ? "H" : "")}_{gpName}";
			}
		}
	}
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetDef))]
	public static void GetDef(GrandprixDataContainer __instance, int index, ref bool __runOriginal, ref GrandprixDef __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (BundleParser.GPIDtoGPDef.ContainsKey(index))
				__result = BundleParser.GPIDtoGPDef[index];
			else
				Plugin.Log.LogError($"Tried retrieving GPDef for GP UUID {index}, but couldn't find it.");
		}
	}
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetDifficulty))]
	public static void GetDifficulty(GrandprixDataContainer __instance, int index, ref bool __runOriginal, ref int __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (BundleParser.GPIDtoGPDef.ContainsKey(index))
				__result = BundleParser.GPIDtoGPDef[index]._Difficulty;
			else
				Plugin.Log.LogError($"Tried retrieving difficulty for GP UUID {index}, but couldn't find it.");
		}
	}
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetMatch))]
	public static void GetMatch(GrandprixDataContainer __instance, int index, bool IsHard, ref bool __runOriginal, ref Il2CppStructArray<CustomMatchData.MatchData> __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (BundleParser.GPIDtoGPDef.ContainsKey(index))

				__result = IsHard ? BundleParser.GPIDtoGPDef[index]._Stages_Hard : BundleParser.GPIDtoGPDef[index]._Stages;
			else
				Plugin.Log.LogError($"Tried retrieving stages for GP UUID {index}, but couldn't find it.");
		}
	}
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetEnemyCodes))]
	public static void GetEnemyCodes(GrandprixDataContainer __instance, int index, ref bool __runOriginal, ref Il2CppStringArray __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (BundleParser.GPIDtoGPDef.ContainsKey(index))
				__result = BundleParser.GPIDtoGPDef[index]._EnemyCodes;
			else
				Plugin.Log.LogError($"Tried retrieving enemy codes for GP UUID {index}, but couldn't find it.");
		}
	}
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GrandprixDataContainer), nameof(GrandprixDataContainer.GetEnemyLevels))]
	public static void GetEnemyLevels(GrandprixDataContainer __instance, int index, bool IsHard, ref bool __runOriginal, ref Il2CppStructArray<float> __result)
	{
		if (__instance.grandprixDatas.Length < index)
		{
			__runOriginal = false;
			if (BundleParser.GPIDtoGPDef.ContainsKey(index))
				__result = IsHard ? BundleParser.GPIDtoGPDef[index]._EnemyLevels_Hard : BundleParser.GPIDtoGPDef[index]._EnemyLevels;
			else
				Plugin.Log.LogError($"Tried retrieving enemy levels for GP UUID {index}, but couldn't find it.");
		}
	}

}