using UnityEditor;
using UnityEngine;
using Abyss.Legacy;

public static class CreateAbyssalSigilAssets
{
    [MenuItem("Tools/Abyssbound/Content/Loot/Create Abyssal Sigil Sample Assets", priority = 0)]
    public static void Create()
    {
        EnsureFolder("Assets", "Game");
        EnsureFolder("Assets/Game", "Loot");
        EnsureFolder("Assets/Game/Loot", "Items");
        EnsureFolder("Assets/Game/Loot", "DropTables");

        const string itemPath = "Assets/Game/Loot/Items/AbyssalSigil.asset";
        const string tablePath = "Assets/Game/Loot/DropTables/abyssal_sigil_basic.asset";

        var item = AssetDatabase.LoadAssetAtPath<LegacyItemDefinition>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<LegacyItemDefinition>();
            item.itemId = "AbyssalSigil";
            item.displayName = "Abyssal Sigil";
            AssetDatabase.CreateAsset(item, itemPath);
        }

        var table = AssetDatabase.LoadAssetAtPath<DropTable>(tablePath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<DropTable>();
            AssetDatabase.CreateAsset(table, tablePath);
        }

        if (table.normalDrops == null)
            table.normalDrops = new System.Collections.Generic.List<DropEntry>();

        bool alreadyLinked = false;
        for (int i = 0; i < table.normalDrops.Count; i++)
        {
            var e = table.normalDrops[i];
            if (e != null && e.item == item)
            {
                alreadyLinked = true;
                break;
            }
        }

        if (!alreadyLinked)
        {
            table.normalDrops.Add(new DropEntry
            {
                item = item,
                dropChance = 0.25f
            });
        }

        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = table;
        EditorGUIUtility.PingObject(table);
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
