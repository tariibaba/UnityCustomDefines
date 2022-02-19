using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;
using System.Reflection;

public class CustomDefinesEditorWindow : EditorWindow
{
    SerializedObject serializedObject;
    ReorderableList list;
    private CustomDefinesData definesData;
    private const string DataSavePath = "Assets/UnityCustomDefines/Data.asset";
    private BuildTargetGroup[] supportedTargetGroups;

    [MenuItem("Window/Custom Defines")]
    public static void ShowWindow()
    {
        GetWindow(typeof(CustomDefinesEditorWindow), false, "Custom Defines");
        Debug.Log($"Show window");
    }

    private void SetSupportedTargetGroups()
    {
        var supported = new List<BuildTarget>();
        var moduleManager = Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
        var isPlatformSupportLoaded = moduleManager.GetMethod("IsPlatformSupportLoaded", BindingFlags.Static | BindingFlags.NonPublic);
        var getTargetStringFromBuildTarget = moduleManager.GetMethod("GetTargetStringFromBuildTarget", BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var build in Enum.GetValues(typeof(BuildTarget)))
        {
            var targetString = (string)getTargetStringFromBuildTarget.Invoke(null, new object[] { build });
            var isSupported = (bool)isPlatformSupportLoaded.Invoke(null, new object[] { targetString });
            if (isSupported) supported.Add((BuildTarget)build);
        }
        supportedTargetGroups = supported.Select(BuildPipeline.GetBuildTargetGroup).ToArray();
    }

    private void OnEnable()
    {
        SetSupportedTargetGroups();
        definesData = AssetDatabase.LoadAssetAtPath<CustomDefinesData>(DataSavePath);
        if (definesData == null)
        {
            definesData = CreateInstance<CustomDefinesData>();
        }
        serializedObject = new SerializedObject(definesData);
        var allPlatforms = serializedObject.FindProperty("Defines_AllPlatforms");
        list = new ReorderableList(
            serializedObject,
            allPlatforms,
            false, true, true, true);
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, 200, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("Name"), GUIContent.none);
            EditorGUI.PropertyField(
                new Rect(rect.x + rect.width - 30, rect.y, 30, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("IsEnabled"), GUIContent.none);
        };
        list.drawHeaderCallback = (rect) =>
        {
            EditorGUI.LabelField(rect, "Defines");
        };
        list.onAddCallback = (list) =>
        {
            allPlatforms.arraySize++;
            var newElement = allPlatforms.GetArrayElementAtIndex(allPlatforms.arraySize - 1);
            newElement.FindPropertyRelative("Name").stringValue = "";
            newElement.FindPropertyRelative("IsEnabled").boolValue = false;
        };
    }

    private bool unsavedChanges = false;

    private void OnGUI()
    {
        var prevCustomDefine = definesData.Clone();
        var count = prevCustomDefine.Defines_AllPlatforms.Count;
        serializedObject.Update();
        list.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
        if (!prevCustomDefine.Equal_AllPlatformsDefines(definesData))
        {
            unsavedChanges = true;
        }
        GUI.enabled = unsavedChanges;
        if (GUILayout.Button("Apply"))
        {
            var defines = definesData.Defines_AllPlatforms
                .Where((define) => define.IsEnabled)
                .Select((define) => define.Name)
                .ToArray();
            foreach (var platform in supportedTargetGroups)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(platform, defines);
            }
            unsavedChanges = false;
        }
        if (!AssetDatabase.Contains(definesData))
        {
            AssetDatabase.CreateAsset(definesData, DataSavePath);
        }
        AssetDatabase.SaveAssets();
    }
}

public class CustomDefinesData : ScriptableObject
{
    public List<CustomDefine> Defines_AllPlatforms = new List<CustomDefine>();

    public bool Equal_AllPlatformsDefines(CustomDefinesData otherData)
    {
        if (otherData == null) return false;

        return Defines_AllPlatforms
            .All((define) => otherData.Defines_AllPlatforms
                .Find((otherDefine) => define.Equals(otherDefine)) != null)
            && otherData.Defines_AllPlatforms
                .All((otherDefine) => Defines_AllPlatforms
                    .Find((define) => otherDefine.Equals(define)) != null);
    }

    public CustomDefinesData Clone()
    {
        var clone = CreateInstance<CustomDefinesData>();
        foreach (var define in Defines_AllPlatforms)
        {
            clone.Defines_AllPlatforms.Add(new CustomDefine
            {
                Name = define.Name,
                IsEnabled = define.IsEnabled
            });
        }
        return clone;
    }
}

[Serializable]
public class CustomDefine
{
    public string Name = "";
    public bool IsEnabled = true;

    public bool Equals(CustomDefine other)
    {
        return Name == other.Name && IsEnabled == other.IsEnabled;
    }
}
