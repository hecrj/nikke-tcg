using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Diagnostics;

public class Builder : EditorWindow
{
    private enum TextureSize
    {
        Original,
        Size512,
		Size768,
        Size1024
    }

    // On-GPU texture format for the built bundle.
    //   Uncompressed = RGBA32, 4 bytes/px — lossless, biggest memory.
    //   DXT5         = ~1/4 memory, standard quality (opaque frames auto-pack as DXT1, half again).
    //   BC7          = ~1/4 memory at higher quality than DXT5, but slower to encode at build time.
    private enum Compression
    {
        Uncompressed,
        DXT5,
        BC7
    }

    private TextureSize selectedSize = TextureSize.Original;
    private Compression selectedCompression = Compression.BC7;

    [MenuItem("Tools/Process Textures and Create Bundles")]
    static void Init()
    {
        Builder window = (Builder)EditorWindow.GetWindow(typeof(Builder));
        window.titleContent = new GUIContent("Texture Processor");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Texture Processing Settings", EditorStyles.boldLabel);
        
        selectedSize = (TextureSize)EditorGUILayout.EnumPopup("Texture Size", selectedSize);
        selectedCompression = (Compression)EditorGUILayout.EnumPopup("Texture Compression", selectedCompression);
        
        if (GUILayout.Button("Process Textures and Create Bundles"))
        {
            GenerateMaterials();
            ProcessAllTextures(selectedSize, selectedCompression);
            CreateAssetBundles();
            OpenAssetBundleFolder();
        }
    }

    static void Run()
    {
        GenerateMaterials();
        ProcessAllTextures(TextureSize.Original, Compression.BC7);
        CreateAssetBundles();
    }

    static void OpenAssetBundleFolder()
    {
        string bundleDirectory = Path.Combine(System.Environment.CurrentDirectory, "AssetBundles");
        if (Directory.Exists(bundleDirectory))
        {
            bundleDirectory = bundleDirectory.Replace("/", "\\"); // Ensure Windows path format
            Process.Start("explorer.exe", bundleDirectory);
        }
        else
        {
            UnityEngine.Debug.LogError("AssetBundles folder not found!");
        }
    }

    static void GenerateMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture");

        int processed = 0;
        int skipped = 0;

        foreach (string guid in guids)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(texturePath);

            // Must start with "_BoosterPack" or "_BoosterBox"
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
                UnityEngine.Debug.LogWarning(
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

            UnityEngine.Debug.Log(
                $"Processed Booster Pack: {fileName}\n" +
                $"Texture: {texturePath}\n" +
                $"Material: {materialPath}"
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Debug.Log(
            $"Booster Pack material generation complete. " +
            $"Processed: {processed}, Skipped Icons: {skipped}"
        );
    }

    static void ProcessAllTextures(TextureSize textureSize, Compression compression)
    {
        string[] allFiles = Directory.GetFiles("Assets", "*.png", SearchOption.AllDirectories);
        
        int processedCount = 0;
        foreach (string filePath in allFiles)
        {
            string unityPath = filePath.Replace("\\", "/");
            
            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer != null)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                // Apply size settings based on selection
                switch (textureSize)
                {
                    case TextureSize.Size512:
                        importer.maxTextureSize = 512;
                        break;
					case TextureSize.Size768:
                        importer.maxTextureSize = 768;
                        break;
                    case TextureSize.Size1024:
                        importer.maxTextureSize = 1024;
                        break;
                    case TextureSize.Original:
                        // Leave the size unchanged
                        break;
                }
                
                settings.filterMode = FilterMode.Bilinear;
                importer.filterMode = FilterMode.Bilinear;
                
                settings.mipmapEnabled = false; // card sprites don't use mipmaps
                importer.SetTextureSettings(settings);
                
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.streamingMipmaps = false; // Disable streaming textures

                // Choose the GPU format from the dropdown.
                bool compressed;
                TextureImporterFormat format;
                switch (compression)
                {
                    case Compression.BC7:
                        format = TextureImporterFormat.BC7;
                        compressed = true;
                        break;
                    case Compression.DXT5:
                        // DXT5's alpha block is wasted on fully-opaque frames — DXT1 is half the memory.
                        bool hasAlpha = importer.alphaSource != TextureImporterAlphaSource.None
                                        && importer.DoesSourceTextureHaveAlpha();
                        format = hasAlpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
                        compressed = true;
                        break;
                    default: // Uncompressed
                        format = TextureImporterFormat.RGBA32;
                        compressed = false;
                        break;
                }

                importer.textureCompression = compressed
                    ? TextureImporterCompression.Compressed
                    : TextureImporterCompression.Uncompressed;

                // Override the Standalone platform explicitly. GetPlatformTextureSettings("Standalone")
                // + overridden = true is what actually makes the chosen format stick — the previous code
                // built a fresh, untargeted TextureImporterPlatformSettings that Unity silently ignored,
                // so textures ended up in Unity's default format regardless.
                TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings("Standalone");

                if(platformSettings.overridden
                    && platformSettings.format == format
                    && platformSettings.maxTextureSize == importer.maxTextureSize
                    && platformSettings.textureCompression == importer.textureCompression)
                {
                    UnityEngine.Debug.Log($"Skipped: {unityPath}");
                    continue;
                }

                platformSettings.overridden = true;
                platformSettings.format = format;
                platformSettings.maxTextureSize = importer.maxTextureSize;
                platformSettings.textureCompression = importer.textureCompression;
                // Best-quality compression (100). Ignored for Uncompressed; for BC7/DXT it's the
                // slowest but highest-fidelity encode — worth it for card art.
                platformSettings.compressionQuality = (int)TextureCompressionQuality.Best;
                importer.SetPlatformTextureSettings(platformSettings);

                importer.SaveAndReimport();
                processedCount++;
                
                UnityEngine.Debug.Log($"Processed: {unityPath} with size: {importer.maxTextureSize}");
            }
        }
        
        UnityEngine.Debug.Log($"Completed! Processed {processedCount} textures.");
    }

    static void CreateAssetBundles()
    {
        string bundleDirectory = "AssetBundles";
        if (!Directory.Exists(bundleDirectory))
        {
            Directory.CreateDirectory(bundleDirectory);
        }

        // Clear any previous bundle-name assignments so a stale shared name (e.g. the old "animated")
        // can't linger on assets and keep colliding.
        foreach (string staleName in AssetDatabase.GetAllAssetBundleNames())
            AssetDatabase.RemoveAssetBundleName(staleName, true);

        string[] setDirs = Directory.GetDirectories("Assets", "*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetDirectories(Path.Combine("Assets", "Animated"), "*", SearchOption.TopDirectoryOnly))
            .ToArray();

        foreach (string setDir in setDirs)
        {
            string dirName = Path.GetFileName(setDir);

            if (dirName == "Editor" || dirName == "Plugins" || dirName == "Resources" || dirName == "Animated")
                continue;

            string bundleName = dirName.ToLower(); // e.g. "geneticapex", "mythicalisland" — unique per set

            if (setDir.Contains(Path.DirectorySeparatorChar + "Animated" + Path.DirectorySeparatorChar))
            {
                bundleName += "_animated";
            }

            string[] assetPaths = Directory.GetFiles(setDir, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta"))
                .Select(path => path.Replace("\\", "/"))
                .ToArray();

            foreach (string assetPath in assetPaths)
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    importer.assetBundleName = bundleName;
                }
            }
        }

        BuildPipeline.BuildAssetBundles(
            bundleDirectory, 
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        UnityEngine.Debug.Log("Asset bundles created successfully!");
    }
}
