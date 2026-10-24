using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchHorizonsMod;

public class UI
{
	private static GameObject GPButtonContainer;
	private static GameObject CustomGPContainer;
	private static GameObject GPButton;
	private static Dictionary<string, GameObject> GPDefButtons = [];
	public static bool viewingCustomGPs = false;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Scene_MainMenu), nameof(Scene_MainMenu.Start))]
	public static void MainStart()
	{
		if (!Plugin.assets) return;
		GPDefButtons.Clear();
		GameObject canvas = SceneManager.GetSceneByName("MainMenu").GetRootGameObjects().First(e => e.name == "Canvas_MainMenu");

		GPButtonContainer = canvas.transform.Find("SinglePlay").Find("SelectArea").gameObject;
		GPButton = GameObject.Instantiate(GPButtonContainer.transform.GetChild(0).gameObject);
		Transform bottomNav = canvas.transform.Find("SinglePlay").Find("BG").Find("BottomBar");
		GameObject customBtn = GameObject.Instantiate(bottomNav.GetChild(2).gameObject, bottomNav);
		customBtn.transform.localPosition += new Vector3(300, 0, 0);
		customBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Custom GPs";
		customBtn.GetComponent<Button>().onClick.m_PersistentCalls.Clear();
		customBtn.GetComponent<Button>().onClick.AddListener((Action)(() =>
		{
			viewingCustomGPs = !viewingCustomGPs;
			GPButtonContainer.active = !viewingCustomGPs;
			CustomGPContainer.active = viewingCustomGPs;
			if (viewingCustomGPs) customBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Custom GPs";
			else customBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Vanilla GPs";
		}));

		SetupButtons();
	}

	public static void SetupButtons()
	{
		CustomGPContainer = GameObject.Instantiate(Plugin.assets.LoadAsset<GameObject>("ArchHorizonsCustomGPs"));
		CustomGPContainer.transform.SetParent(GPButtonContainer.transform.parent);
		CustomGPContainer.active = false;

		var rect = CustomGPContainer.GetComponent<RectTransform>();
		rect.pivot = new(0, .5f);
		rect.anchorMin = new(0, 0);
		rect.anchorMax = new(0, 1);
		rect.sizeDelta = new(800, -300);
		rect.anchoredPosition = new(100, 0);

		foreach (var bundleIdDef in BundleParser.loadedModDefs)
		{
			// Bundle container for GPs
			var bundleObj = GameObject.Instantiate(Plugin.assets.LoadAsset<GameObject>("ArchHorizonsBundle"));
			bundleObj.transform.SetParent(CustomGPContainer.transform.GetChild(0).GetChild(0).GetChild(0));

			Button campaignBtn = bundleObj.transform.GetChild(0).GetComponent<Button>();
			campaignBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = bundleIdDef.Value.bundleName;
			campaignBtn.onClick.AddListener((Action)(() => { bundleObj.transform.GetChild(1).gameObject.active = !bundleObj.transform.GetChild(1).gameObject.active; }));

			bool odd = false;
			foreach (var GPUUID in BundleParser.BundleIDtoGPIDs[bundleIdDef.Key])
			{
				// for (int i = 0; i < 5; i++) { var GPUUID = BundleParser.bundleGPUUIDs[10][0];
				// GP row
				GameObject gpObj = GameObject.Instantiate(Plugin.assets.LoadAsset<GameObject>($"ArchHorizonsGP_{(odd ? "L" : "R")}"), bundleObj.transform.GetChild(1));

				// GP data
				var gpDef = BundleParser.GPIDtoGPDef[GPUUID];
				gpObj.transform.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>().text = gpDef._Name;
				gpObj.transform.GetChild(1).GetChild(2).GetComponent<TextMeshProUGUI>().text = $"[ Class {gpDef._Difficulty} ]";

				gpObj.GetComponent<UI_ButtonID>()._id = gpDef._ID;
				gpObj.GetComponent<UI_ButtonID>()._class = gpDef._Difficulty;
				gpObj.GetComponent<Button>().onClick.AddListener((Action)(() => UI_SinglePlay.ins.Main_Single_Decide()));

				// Hover animations
				var eventTrigger = gpObj.AddComponent<EventTrigger>();
				var triggerEntry = new EventTrigger.Entry() { eventID = EventTriggerType.PointerEnter };
				triggerEntry.callback.AddListener((Action<BaseEventData>)((data) => GPButtonPointerEnter(eventTrigger.gameObject)));
				eventTrigger.delegates.Add(triggerEntry);
				triggerEntry = new EventTrigger.Entry() { eventID = EventTriggerType.PointerExit };
				triggerEntry.callback.AddListener((Action<BaseEventData>)((data) => GPButtonPointerExit(eventTrigger.gameObject)));
				eventTrigger.delegates.Add(triggerEntry);

				odd = !odd;

				if (!GPDefButtons.ContainsKey("ArchGP_" + gpDef._Name)) GPDefButtons.Add("ArchGP_" + gpDef._Name, gpObj);
			}
		}

		RefreshRanks();
	}

	private static void GPButtonPointerEnter(GameObject obj)
	{
		obj.transform.GetChild(0).GetChild(0).GetChild(0).DOLocalMoveY(0, .2f);
		UI_SinglePlay.ins.Main_Single_LoadInfo(obj.GetComponent<UI_ButtonID>()._id, new(-1000, -1000));

	}
	private static void GPButtonPointerExit(GameObject obj)
	{
		var target = obj.transform.GetChild(0).GetChild(0).GetChild(0);
		target.DOLocalMoveY(-5, .2f).OnComplete((Action)(() => target.localPosition = new(0, 5, 0)));
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(MatchmakingUI), nameof(MatchmakingUI.ShowCreateSession))]
	public static void MatchmakingUI_EnableScrollingFieldSelect(MatchmakingUI __instance)
	{
		__instance.Create_FieldType?.transform.GetChild(2).GetComponent<ScrollRect>()?.vertical = true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(MatchmakingUI), nameof(MatchmakingUI.GetLandText))]
	public static void GetLandText(VenueData.Landscape land, ref string __result)
	{
		if (land > (VenueData.Landscape)7)
		{
			string str = LocalizationSettings.Instance.GetStringDatabase().GetTable("Single", LocalizationSettings.SelectedLocale).GetEntry("spland" + (int)land)?.Value;
			if (str != null) __result = str;
			else Plugin.Log.LogWarning("Couldn't find a translation for Land ID " + (int)land);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UI_SinglePlay), nameof(UI_SinglePlay.Main_Single_ShowUI))]
	[HarmonyPatch(typeof(UI_SinglePlay), nameof(UI_SinglePlay.ToggleHardMode))]
	public static void RefreshRanks()
	{
		foreach (var GPDefButton in GPDefButtons)
		{
			string key = GPDefButton.Key;
			if (GameVariable.IsHardMode)
				key = key.Insert(6, "H");

			int rank = DataManager.ins.GetSinglePlayResult(key);
			Plugin.LogInfo(key + " " + rank);
			Image bg = GPDefButton.Value?.transform.GetChild(0).GetChild(0).GetComponent<Image>();
			if (bg == null) continue;
			switch (rank)
			{
				case 1:
					bg.color = UI_SinglePlay.ins._GrandprixMenu.btnColors[0]; break;
				case 2:
					bg.color = UI_SinglePlay.ins._GrandprixMenu.btnColors[1]; break;
				case 3:
					bg.color = UI_SinglePlay.ins._GrandprixMenu.btnColors[2]; break;
				case 0:
				default:
					bg.color = UI_SinglePlay.ins._GrandprixMenu.btnColors[3]; break;
			}
		}
	}
}