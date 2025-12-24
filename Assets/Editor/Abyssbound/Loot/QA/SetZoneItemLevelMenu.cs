#if UNITY_EDITOR
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class SetZoneItemLevelMenu
{
    private const string Root = "Tools/Abyssbound/QA/Set QA ItemLevel/";

    private static void Set(int level)
    {
        LootQaSettings.ItemLevel = Mathf.Clamp(level, 1, 20);
        LootQaSettings.DebugLogsEnabled = true;
        Debug.Log($"[LootQA] Override={LootQaSettings.ItemLevel}");
    }

    private static bool IsChecked(int level) => LootQaSettings.ItemLevel == level;

    [MenuItem(Root + "1")] public static void Set1() => Set(1);
    [MenuItem(Root + "2")] public static void Set2() => Set(2);
    [MenuItem(Root + "3")] public static void Set3() => Set(3);
    [MenuItem(Root + "4")] public static void Set4() => Set(4);
    [MenuItem(Root + "5")] public static void Set5() => Set(5);
    [MenuItem(Root + "6")] public static void Set6() => Set(6);
    [MenuItem(Root + "7")] public static void Set7() => Set(7);
    [MenuItem(Root + "8")] public static void Set8() => Set(8);
    [MenuItem(Root + "9")] public static void Set9() => Set(9);
    [MenuItem(Root + "10")] public static void Set10() => Set(10);
    [MenuItem(Root + "11")] public static void Set11() => Set(11);
    [MenuItem(Root + "12")] public static void Set12() => Set(12);
    [MenuItem(Root + "13")] public static void Set13() => Set(13);
    [MenuItem(Root + "14")] public static void Set14() => Set(14);
    [MenuItem(Root + "15")] public static void Set15() => Set(15);
    [MenuItem(Root + "16")] public static void Set16() => Set(16);
    [MenuItem(Root + "17")] public static void Set17() => Set(17);
    [MenuItem(Root + "18")] public static void Set18() => Set(18);
    [MenuItem(Root + "19")] public static void Set19() => Set(19);
    [MenuItem(Root + "20")] public static void Set20() => Set(20);

    [MenuItem(Root + "1", true)] public static bool V1() { Menu.SetChecked(Root + "1", IsChecked(1)); return true; }
    [MenuItem(Root + "2", true)] public static bool V2() { Menu.SetChecked(Root + "2", IsChecked(2)); return true; }
    [MenuItem(Root + "3", true)] public static bool V3() { Menu.SetChecked(Root + "3", IsChecked(3)); return true; }
    [MenuItem(Root + "4", true)] public static bool V4() { Menu.SetChecked(Root + "4", IsChecked(4)); return true; }
    [MenuItem(Root + "5", true)] public static bool V5() { Menu.SetChecked(Root + "5", IsChecked(5)); return true; }
    [MenuItem(Root + "6", true)] public static bool V6() { Menu.SetChecked(Root + "6", IsChecked(6)); return true; }
    [MenuItem(Root + "7", true)] public static bool V7() { Menu.SetChecked(Root + "7", IsChecked(7)); return true; }
    [MenuItem(Root + "8", true)] public static bool V8() { Menu.SetChecked(Root + "8", IsChecked(8)); return true; }
    [MenuItem(Root + "9", true)] public static bool V9() { Menu.SetChecked(Root + "9", IsChecked(9)); return true; }
    [MenuItem(Root + "10", true)] public static bool V10() { Menu.SetChecked(Root + "10", IsChecked(10)); return true; }
    [MenuItem(Root + "11", true)] public static bool V11() { Menu.SetChecked(Root + "11", IsChecked(11)); return true; }
    [MenuItem(Root + "12", true)] public static bool V12() { Menu.SetChecked(Root + "12", IsChecked(12)); return true; }
    [MenuItem(Root + "13", true)] public static bool V13() { Menu.SetChecked(Root + "13", IsChecked(13)); return true; }
    [MenuItem(Root + "14", true)] public static bool V14() { Menu.SetChecked(Root + "14", IsChecked(14)); return true; }
    [MenuItem(Root + "15", true)] public static bool V15() { Menu.SetChecked(Root + "15", IsChecked(15)); return true; }
    [MenuItem(Root + "16", true)] public static bool V16() { Menu.SetChecked(Root + "16", IsChecked(16)); return true; }
    [MenuItem(Root + "17", true)] public static bool V17() { Menu.SetChecked(Root + "17", IsChecked(17)); return true; }
    [MenuItem(Root + "18", true)] public static bool V18() { Menu.SetChecked(Root + "18", IsChecked(18)); return true; }
    [MenuItem(Root + "19", true)] public static bool V19() { Menu.SetChecked(Root + "19", IsChecked(19)); return true; }
    [MenuItem(Root + "20", true)] public static bool V20() { Menu.SetChecked(Root + "20", IsChecked(20)); return true; }
}
#endif
