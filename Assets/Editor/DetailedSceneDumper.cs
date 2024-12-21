using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

public class DetailedSceneDumper : EditorWindow
{
    [MenuItem("Tools/Dump Detailed Scene")]
    static void DumpDetailedScene()
    {
        string path = "DetailedSceneDump.txt";
        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("=== PROJECT STRUCTURE ===\n");
            DumpProjectStructure(Application.dataPath, writer);
            
            writer.WriteLine("\n=== SCENE HIERARCHY ===");
            writer.WriteLine($"Scene: {SceneManager.GetActiveScene().path}\n");
            
            foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                DumpGameObject(obj, 0, writer);
            }
        }
        Debug.Log($"Scene dump written to {Path.GetFullPath(path)}");
        AssetDatabase.Refresh();
    }

    static void DumpProjectStructure(string path, StreamWriter writer, int depth = 0)
    {
        string[] excludeExtensions = { ".sln", ".csproj", ".meta" };
        
        foreach (string dir in Directory.GetDirectories(path))
        {
            string dirName = Path.GetFileName(dir);
            if (!dirName.StartsWith("."))
            {
                writer.WriteLine($"{new string(' ', depth * 2)}└─ {dirName}/");
                DumpProjectStructure(dir, writer, depth + 1);
            }
        }

        foreach (string file in Directory.GetFiles(path)
            .Where(f => !excludeExtensions.Contains(Path.GetExtension(f))))
        {
            writer.WriteLine($"{new string(' ', depth * 2)}   {Path.GetFileName(file)}");
        }
    }

    static void DumpGameObject(GameObject go, int depth, StreamWriter writer)
    {
        string indent = new string(' ', depth * 2);
        writer.WriteLine($"{indent}└─ {go.name}");

        // Only process scripts from our own assemblies
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb != null && 
                !mb.GetType().FullName.StartsWith("UnityEngine") && 
                !mb.GetType().FullName.StartsWith("Unity.") &&
                !mb.GetType().FullName.StartsWith("TMPro."))
            {
                writer.WriteLine($"{indent}   ├─ {mb.GetType().Name}");
                
                SerializedObject so = new SerializedObject(mb);
                SerializedProperty prop = so.GetIterator();
                
                if (prop.NextVisible(true))
                {
                    do
                    {
                        if (prop.name != "m_Script")
                        {
                            string value = GetPropertyValue(prop);
                            if (value != "(not shown)")
                            {
                                writer.WriteLine($"{indent}   │  {prop.name}: {value}");
                            }
                        }
                    }
                    while (prop.NextVisible(false));
                }
            }
        }

        foreach (Transform child in go.transform)
        {
            DumpGameObject(child.gameObject, depth + 1, writer);
        }
    }

    static string GetPropertyValue(SerializedProperty prop)
    {
        return prop.propertyType switch
        {
            SerializedPropertyType.Integer => prop.intValue.ToString(),
            SerializedPropertyType.Boolean => prop.boolValue.ToString(),
            SerializedPropertyType.Float => prop.floatValue.ToString(),
            SerializedPropertyType.String => prop.stringValue,
            SerializedPropertyType.Vector2 => prop.vector2Value.ToString(),
            SerializedPropertyType.Vector3 => prop.vector3Value.ToString(),
            SerializedPropertyType.Color => prop.colorValue.ToString(),
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue ? prop.objectReferenceValue.name : "None",
            _ => "(not shown)"
        };
    }
} 