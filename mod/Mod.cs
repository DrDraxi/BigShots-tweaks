using System.Collections;
using System.Collections.Generic;
using AlterEyes.BigShots.Hangar;
using AlterEyes.BigShots.Networking;
using AlterEyes.BigShots.Player;
using AlterEyes.Common;
using Fusion;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Preferences;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[assembly: MelonInfo(typeof(BigShotsTweaks.Mod), "BigShotsTweaks", "1.0.0", "DrDraxi")]
[assembly: MelonGame("AlterEyes", "BigShots")]

namespace BigShotsTweaks;

public class Mod : MelonMod
{
    private static MelonPreferences_Category _cat = null!;
    private static MelonPreferences_Entry<int> _maxPlayers = null!;
    private static MelonPreferences_Entry<bool> _autoContinueOffline = null!;

    public static int MaxPlayers => _maxPlayers.Value;
    public static bool AutoContinueOffline => _autoContinueOffline.Value;

    internal const int SliderMin = 2;
    internal const int SliderMax = 8;

    internal static void SetMaxPlayers(int v)
    {
        _maxPlayers.Value = Mathf.Clamp(v, 2, 200);
        _cat.SaveToFile(printmsg: false);
    }

    internal static GameObject? InjectedSliderRow;
    internal static GameObject? MirrorSourceRow;

    public override void OnInitializeMelon()
    {
        _cat = MelonPreferences.CreateCategory("BigShotsTweaks");
        _cat.SetFilePath("UserData/BigShotsTweaks.cfg");

        _maxPlayers = _cat.CreateEntry(
            "MaxPlayers", 8,
            "Max Players",
            "Maximum players per session. Vanilla cap is 2. Photon Fusion Shared mode supports up to 200; the lobby UI only has 2 visible slots, so players 3+ join via room code.",
            validator: new ValueRange<int>(2, 200));

        _autoContinueOffline = _cat.CreateEntry(
            "AutoContinueOffline", true,
            "Auto-Continue Offline",
            "Automatically click the 'Continue Offline' button when the startup connection-failed popup appears.");

        _cat.SaveToFile(printmsg: false);
        LoggerInstance.Msg($"Loaded. MaxPlayers={_maxPlayers.Value} AutoContinue={_autoContinueOffline.Value}");
    }

    public override void OnUpdate()
    {
        if (InjectedSliderRow != null && MirrorSourceRow != null
            && InjectedSliderRow.activeSelf != MirrorSourceRow.activeSelf)
        {
            InjectedSliderRow.SetActive(MirrorSourceRow.activeSelf);
        }
    }
}

[HarmonyPatch(typeof(NetworkManager), "MakeStartGameArgs")]
internal static class Patch_MakeStartGameArgs
{
    private static void Postfix(ref StartGameArgs __result)
    {
        __result.PlayerCount = Mod.MaxPlayers;
    }
}

[HarmonyPatch(typeof(NetworkManager), "HandlePlayerJoined")]
internal static class Patch_HandlePlayerJoined
{
    private static bool Prefix(NetworkRunner arg0)
    {
        if (arg0 != null && arg0.SessionInfo != null
            && arg0.ActivePlayers.Count() >= Mod.MaxPlayers)
        {
            arg0.SessionInfo.IsOpen = false;
        }
        return false;
    }
}

[HarmonyPatch(typeof(NetworkManager), "HandlePlayerLeft")]
internal static class Patch_HandlePlayerLeft
{
    private static bool Prefix(NetworkRunner arg0)
    {
        if (arg0 != null && arg0.SessionInfo != null
            && arg0.ActivePlayers.Count() < Mod.MaxPlayers)
        {
            arg0.SessionInfo.IsOpen = true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(GameTab), "Update")]
internal static class Patch_GameTab_PartyHeaderCount
{
    private static TMP_Text? _mainHeaderText;

    private static void Postfix()
    {
        if (_mainHeaderText == null)
        {
            var headerInst = Singleton<Header>.Instance;
            if (headerInst == null) return;
            var f = AccessTools.Field(typeof(Header), "_mainScreenHeader");
            if (f == null) return;
            _mainHeaderText = f.GetValue(headerInst) as TMP_Text;
            if (_mainHeaderText == null) return;
        }

        int count = 1;
        var pm = PlayerManager.Instance;
        if (pm != null)
        {
            int c = pm.PlayerCount;
            if (c > 0) count = c;
        }
        _mainHeaderText.text = $"Party {count}/{Mod.MaxPlayers}";
    }
}

[HarmonyPatch(typeof(SettingsTab), "OnEnter")]
internal static class Patch_SettingsTab_InjectMaxPlayersSlider
{
    private static void Postfix(SettingsTab __instance)
    {
        MelonCoroutines.Start(InjectAfterDelay(__instance));
    }

    private static IEnumerator InjectAfterDelay(SettingsTab tab)
    {
        yield return null;
        yield return null;
        TryInject(tab);
    }

    private static void TryInject(SettingsTab tab)
    {
        if (tab == null || tab.gameObject == null) return;

        Transform? content = null;
        Transform? regionRow = null;
        var allTransforms = tab.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t.name == "Option_EnumRegions(Clone)") { regionRow = t; content = t.parent; break; }
        }
        if (content == null || regionRow == null)
        {
            MelonLogger.Warning("[SettingsTab] Could not find Option_EnumRegions(Clone); slider not injected.");
            return;
        }

        var existing = content.Find("Option_Slider_BST_MaxPlayers");
        if (existing != null)
        {
            existing.SetSiblingIndex(regionRow.GetSiblingIndex() + 1);
            existing.gameObject.SetActive(regionRow.gameObject.activeSelf);
            Mod.InjectedSliderRow = existing.gameObject;
            Mod.MirrorSourceRow = regionRow.gameObject;
            return;
        }

        Slider? tmplSlider = null;
        foreach (var s in tab.gameObject.GetComponentsInChildren<Slider>(true))
        {
            var rowName = s.transform.parent != null ? s.transform.parent.name : "";
            if (rowName.StartsWith("Option_Slider")) { tmplSlider = s; break; }
        }
        if (tmplSlider == null) return;
        var templateRow = tmplSlider.transform.parent.gameObject;

        var clone = Object.Instantiate(templateRow, content);
        clone.name = "Option_Slider_BST_MaxPlayers";
        clone.transform.SetSiblingIndex(regionRow.GetSiblingIndex() + 1);

        TextMeshProUGUI? titleText = null;
        TextMeshProUGUI? valueText = null;
        Slider? cloneSlider = null;
        foreach (Transform c in clone.transform)
        {
            if (c.name == "Text_Title") titleText = c.GetComponent<TextMeshProUGUI>();
            else if (c.name == "Text (TMP)") valueText = c.GetComponent<TextMeshProUGUI>();
            else if (c.name == "Slider") cloneSlider = c.GetComponent<Slider>();
        }
        if (titleText == null || cloneSlider == null) return;

        int current = Mod.MaxPlayers;
        titleText.text = "Max Players";
        cloneSlider.onValueChanged.RemoveAllListeners();
        cloneSlider.minValue = Mod.SliderMin;
        cloneSlider.maxValue = Mod.SliderMax;
        cloneSlider.wholeNumbers = true;
        cloneSlider.value = Mathf.Clamp(current, Mod.SliderMin, Mod.SliderMax);
        if (valueText != null) valueText.text = ((int)cloneSlider.value).ToString();

        cloneSlider.onValueChanged.AddListener((UnityEngine.Events.UnityAction<float>)(v =>
        {
            int n = Mathf.RoundToInt(v);
            if (valueText != null) valueText.text = n.ToString();
            Mod.SetMaxPlayers(n);
        }));

        clone.SetActive(regionRow.gameObject.activeSelf);
        Mod.InjectedSliderRow = clone;
        Mod.MirrorSourceRow = regionRow.gameObject;
        MelonLogger.Msg($"[SettingsTab] Injected Max Players slider after Region (current={current}).");
    }
}

[HarmonyPatch(typeof(LoginTab), "Update")]
internal static class Patch_LoginTab_AutoContinueOffline
{
    private static readonly HashSet<int> _autoclicked = new();
    private static readonly System.Reflection.FieldInfo? _panelField =
        AccessTools.Field(typeof(LoginTab), "_connectionFailedPanel");
    private static readonly System.Reflection.MethodInfo? _continueMethod =
        AccessTools.Method(typeof(LoginTab), "HandleContinueOfflineButtonClicked");

    private static void Postfix(LoginTab __instance)
    {
        if (!Mod.AutoContinueOffline || _panelField == null || _continueMethod == null) return;

        var panel = _panelField.GetValue(__instance) as UnityEngine.CanvasGroup;
        if (panel == null || panel.gameObject == null) return;

        int id = __instance.GetInstanceID();
        if (!panel.gameObject.activeInHierarchy)
        {
            _autoclicked.Remove(id);
            return;
        }
        if (!_autoclicked.Add(id)) return;

        MelonLogger.Msg("[AutoContinueOffline] Connection-failed panel detected — clicking Continue Offline.");
        _continueMethod.Invoke(__instance, null);
    }
}
