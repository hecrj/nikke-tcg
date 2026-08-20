using UnityEditor;
using UnityEngine;

public static class BoosterPackMaterialGenerator
{
    [MenuItem("Tools/Booster Pack/Generate Materials")]
    public static void GenerateMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture");

        int processed = 0;
        int skipped = 0;

        foreach (string guid in guids)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(texturePath);

            // Must start with "_BoosterPack"
            if (!fileName.StartsWith("_BoosterPack") && !fileName.StartsWith("_BoosterBox"))
                continue;

            // Must NOT end with "Icon"
            if (fileName.EndsWith("Icon"))
            {
                skipped++;
                continue;
            }

            // Get the texture importer
            TextureImporter importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;

            if (importer == null)
                continue;

            // Switch Texture Type to Default
            bool importerChanged = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importerChanged = true;
            }

            if (importerChanged)
            {
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceUpdate
                );
            }

            // Load the texture
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                texturePath
            );

            if (texture == null)
            {
                Debug.LogWarning(
                    $"Could not load texture: {texturePath}"
                );
                continue;
            }

            // Create material path next to the texture
            string directory = System.IO.Path.GetDirectoryName(texturePath);
            string materialPath = $"{directory}/{fileName}.mat";

            // Check whether material already exists
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.name = fileName;

                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.name = fileName;
            }

            // Assign texture to albedo
            material.SetTexture("_MainTex", texture);

            EditorUtility.SetDirty(material);

            processed++;

            Debug.Log(
                $"Processed Booster Pack: {fileName}\n" +
                $"Texture: {texturePath}\n" +
                $"Material: {materialPath}"
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Booster Pack material generation complete. " +
            $"Processed: {processed}, Skipped Icons: {skipped}"
        );
    }
}