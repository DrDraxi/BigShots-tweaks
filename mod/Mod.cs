using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AlterEyes.BigShots.Hangar;
using AlterEyes.BigShots.Networking;
using AlterEyes.BigShots.Player;
using AlterEyes.BigShots.Shifts;
using AlterEyes.Common;
using Fusion;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Preferences;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(BigShotsTweaks.Mod), "BigShotsTweaks", "1.1.0", "DrDraxi")]
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

[HarmonyPatch(typeof(DropshipManager), "SpawnDropships")]
internal static class Patch_DropshipManager_ClampSpawn
{
    private static readonly System.Reflection.FieldInfo? _pathsField =
        AccessTools.Field(typeof(DropshipManager), "_dropshipPaths");
    private static readonly System.Reflection.MethodInfo? _rpcActivate =
        AccessTools.Method(typeof(DropshipManager), "RPC_ActivateDropships");
    private static readonly System.Reflection.MethodInfo? _onDropShipSpawned =
        AccessTools.Method(typeof(DropshipManager), "OnDropShipSpawned");
    private static readonly System.Reflection.MethodInfo? _rpcDropShipsSpawned =
        AccessTools.Method(typeof(DropshipManager), "RPC_DropShipsSpawned");
    private static readonly System.Reflection.MethodInfo? _handlePickup =
        AccessTools.Method(typeof(DropshipManager), "HandlePickupCompleted");

    private static bool Prefix(DropshipManager __instance)
    {
        if (_pathsField == null) return true;
        if (!(_pathsField.GetValue(__instance) is System.Collections.IList paths)) return true;
        var pm = PlayerManager.Instance;
        if (pm == null) return true;

        int playerCount = pm.PlayerCount;
        int loopMax = System.Math.Min(playerCount, System.Math.Min(paths.Count, 2));
        if (playerCount <= loopMax) return true; // safe to run original

        MelonLogger.Msg($"[DropshipManager] Clamping pickup-dropships to {loopMax} (PlayerCount={playerCount}, scenePaths={paths.Count}).");

        var dropships = __instance.Dropships;
        UnityEngine.Events.UnityAction<Dropship>? handler = null;
        if (_handlePickup != null)
        {
            handler = (UnityEngine.Events.UnityAction<Dropship>)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Dropship>), __instance, _handlePickup);
        }

        for (int i = 0; i < loopMax; i++)
        {
            var path = paths[i] as Component;
            if (path == null) continue;
            var ship = path.GetComponentInChildren<Dropship>(true);
            if (ship == null) continue;
            ship.StartMovement();
            if (handler != null) ship.OnPickUpStarted.AddListener(handler);
            _rpcActivate?.Invoke(__instance, new object[] { i });
            dropships.Set(i, ship);
        }
        _onDropShipSpawned?.Invoke(__instance, null);
        _rpcDropShipsSpawned?.Invoke(__instance, null);
        return false;
    }
}

[HarmonyPatch(typeof(DropshipManager), "HandlePickupCompleted")]
internal static class Patch_DropshipManager_LowerThreshold
{
    private static readonly System.Reflection.FieldInfo? _completedField =
        AccessTools.Field(typeof(DropshipManager), "_completedPickup");
    private static readonly System.Reflection.MethodInfo? _coComplete =
        AccessTools.Method(typeof(DropshipManager), "CoCompletePickup");

    private static bool Prefix(DropshipManager __instance, Dropship dropship)
    {
        if (_completedField == null || _coComplete == null) return true;
        var pm = PlayerManager.Instance;
        if (pm == null) return true;

        var dropships = __instance.Dropships;
        int totalSpawned = 0, picked = 0;
        for (int i = 0; i < dropships.Length; i++)
        {
            var d = dropships[i];
            if (d == null) continue;
            totalSpawned++;
            if (d.HasPickedUpMech()) picked++;
        }
        if (totalSpawned == 0) return true;

        int threshold = System.Math.Max(1, System.Math.Min(pm.PlayerCount, totalSpawned));
        var completed = (bool)_completedField.GetValue(__instance);
        if (picked >= threshold && !completed)
        {
            var co = (System.Collections.IEnumerator)_coComplete.Invoke(__instance, null);
            __instance.StartCoroutine(co);
        }
        return false;
    }
}

[HarmonyPatch(typeof(DropoffManager), "CoWaitForMechInitialized")]
internal static class Patch_DropoffManager_SkipForExtras
{
    private static bool Prefix(DropoffManager __instance, AlterEyes.BigShots.Player.Player player, ref System.Collections.IEnumerator __result)
    {
        var pm = PlayerManager.Instance;
        if (pm == null) return true;
        var sorted = pm.AllPlayers
            .Where(p => p != null && p.Object != null)
            .OrderBy(p => p.Object.StateAuthority.PlayerId)
            .ToList();
        int idx = sorted.IndexOf(player);
        if (idx < 0 || idx < 2) return true;

        MelonLogger.Msg($"[DropoffManager] Player slot {idx} (#{player.Object.StateAuthority.PlayerId}) skipping dropoff cinematic.");
        __result = SkipDropoff(__instance, player);
        return false;
    }

    private static System.Collections.IEnumerator SkipDropoff(DropoffManager mgr, AlterEyes.BigShots.Player.Player player)
    {
        while (player == null || player.MechReferences == null
               || player.MechReferences.Object == null || !player.MechReferences.Object.IsValid)
            yield return null;
        while (!player.MechReferences.IsInitialized) yield return null;

        player.MechReferences.RPC_SetCharacterControllerState(true);
        player.MechReferences.RPC_SetReady(isReady: true);
        player.MechReferences.Shutters.RPC_SetShutterState(true);
        mgr.OnMechDroppedOff?.Invoke();
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
