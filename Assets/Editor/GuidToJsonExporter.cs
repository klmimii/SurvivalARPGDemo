using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GuidToJsonExporter
{
    [Serializable]
    private class GuidMapData
    {
        public List<GuidItem> items = new List<GuidItem>();
    }

    [Serializable]
    private class GuidItem
    {
        public string assetPath;
        public string guid;
    }

    [MenuItem("Tools/Export Filename-GUID Mapping")]
    private static void ExportGuidFilenameMap()
    {
        GuidMapData mapData = new GuidMapData();
        string outputPath = Path.Combine(Application.dataPath, "guid_assetpath_map.json");

        string[] allAssets = AssetDatabase.GetAllAssetPaths();

        foreach (string assetPath in allAssets)
        {
            if (assetPath.EndsWith(".meta"))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                continue;

            mapData.items.Add(new GuidItem { assetPath = assetPath, guid = guid });
        }

        string jsonData = JsonUtility.ToJson(mapData, true);
        File.WriteAllText(outputPath, jsonData);

        AssetDatabase.Refresh();
        Debug.Log($"GUID-Filename映射已保存至: {outputPath}");
    }

    [MenuItem("Tools/Replace GUID form Mapping")]
    private static void ReplaceGuidformMapping()
    {
        string outputPath = Path.Combine(Application.dataPath, "guid_assetpath_map.json");
        if (!File.Exists(outputPath))
        {
            Debug.LogError($"未找到映射文件: {outputPath}");
            return;
        }

        string jsonText = File.ReadAllText(outputPath);
        GuidMapData mapData = JsonUtility.FromJson<GuidMapData>(jsonText);

        if (mapData == null || mapData.items == null)
        {
            Debug.LogError("解析映射 JSON 失败！");
            return;
        }

        foreach (var item in mapData.items)
        {
            string filePath = item.assetPath + ".meta";
            ReplaceMetaFileGuid(filePath, item.guid);
        }

        AssetDatabase.Refresh();
        Debug.Log("GUID 替换完成，已刷新 AssetDatabase！");
    }

    private static void ReplaceMetaFileGuid(string filePath, string newGuid)
    {
        if (!File.Exists(filePath))
            return;

        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("guid:"))
            {
                lines[i] = $"guid: {newGuid}";
                break;
            }
        }
        File.WriteAllLines(filePath, lines);
    }
}