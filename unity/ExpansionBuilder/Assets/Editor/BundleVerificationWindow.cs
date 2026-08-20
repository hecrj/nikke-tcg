using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public class BundleVerificationWindow : EditorWindow
{
    private static Dictionary<string, BundleData> bundleData = new Dictionary<string, BundleData>();
    private Vector2 scrollPosition;
    private static List<string> bundleNames = new List<string>();
    private static List<string> displayNames = new List<string>();
    private Dictionary<string, bool> buildToggles = new Dictionary<string, bool>();
    private int selectedTab = 0;
    private Dictionary<string, bool> showItems = new Dictionary<string, bool>();
    private Dictionary<string, bool> showPrefabs = new Dictionary<string, bool>();
    private Dictionary<string, bool> showAssemblies = new Dictionary<string, bool>();
    private ItemEntryDrawer itemEntryDrawer;
    private Dictionary<string, bool> showCardSets = new Dictionary<string, bool>();
    private Dictionary<string, bool> expandedCards = new Dictionary<string, bool>();
    private Dictionary<string, bool> showCardsInSection = new Dictionary<string, bool>();
    private PrefabEntryDrawer prefabEntryDrawer;
    private Dictionary<string, bool> showCustomShops = new Dictionary<string, bool>();
    private CustomShopEntryDrawer customShopEntryDrawer;

    public enum PackGenerationStrategy
    {
        Guaranteed,
        AllRandomWeighted,
        AllRandom
    }

    [Flags]
    public enum Slots
    {
        Slot1 = 1 << 0,
        Slot2 = 1 << 1,
        Slot3 = 1 << 2,
        Slot4 = 1 << 3,
        Slot5 = 1 << 4,
        Slot6 = 1 << 5,
        Slot7 = 1 << 6,
    }

    public enum ECardBorderType
    {
        Base,
        FirstEdition,
        Silver,
        Gold,
        EX,
        FullArt
    }

    public enum BulkBoxType
    {
        WhiteBox,
        YellowBox,
        PinkBox,
        RedBox,
        GreenBox
    }

    [System.Serializable]
    public class BundleData
    {
        public string BundleName = string.Empty;
        public string BundleId = string.Empty;
        public bool AutoCompressTextures = true;
        public int MaxTextureSize = 1024;
        public string TextureCompression = "DXT5";
        public bool EnableMipMaps = false;
        public bool ItemPricesSuppliedByApi = false;
        public List<ItemEntry> Items = new List<ItemEntry>();
        public List<PrefabEntry> Prefabs = new List<PrefabEntry>();
        public List<string> Assemblies = new List<string>();
        public List<CardExpansionEntry> CardExpansions = new List<CardExpansionEntry>();
        public List<CustomShopEntry> CustomShops = new List<CustomShopEntry>();
    }

    [System.Serializable]
    public class CustomShopEntry
    {
        [Tooltip("Unique ID (Has to be the name of your folder in Phone Overhaul App)")]
        public string PhoneAppId;
        [Tooltip("The name that will display under your app on the phone")]
        public string AppDisplayName;
        [Tooltip("The name of the main Icon for your phone app (no extension)")]
        public string AppIcon;
        [Tooltip("The name of the inner background image for your phone app (no extension)")]
        public string AppInnerBGImage;
        [Tooltip("The name of the outer background image for your phone app (no extension)")]
        public string AppOuterBGImage;
        [Tooltip("The name of the sprite to use in the store header")]
        public string SiteLogo;
        [Tooltip("The name of the sprite to use as the store header")]
        public string SiteHeader;
        [Tooltip("The name of the sprite to use as the stores background")]
        public string SiteBackground;
    }


    [System.Serializable]
    public class GenerationWeightsEntry
    {
        public string Rarity;
        public Slots GuaranteedSlots;
        public float NormalWeight;
        public float GodPackWeight;
    }

    [System.Serializable]
    public class RarityWeightEntry
    {
        public string Rarity;
        public float Weight = 1f;
    }

    [System.Serializable]
    public class SlotWeightEntry
    {
        [Tooltip("Which pack slot this weight table applies to")]
        public Slots Slot;
        public List<RarityWeightEntry> Weights = new List<RarityWeightEntry>();
    }

    [System.Serializable]
    public class CardExpansionEntry
    {
        [Tooltip("Name of the expansion. This is what is used to display in game")]
        public string Name;
        [Tooltip("HAS TO BE GLOBALLY UNIQUE. Should not appear in game UI")]
        public string CardExpansion;
        [Tooltip("This expansion will be nested under this category in the selection screens.")]
        public string MenuCategory;
        [Tooltip("Player level at which trades for this expansion become unlocked. Set to 0 to unlock immediately.")]
        public int TradeUnlockLevel;
        [Tooltip("Texture name for the texture to be used for the card back. Texture names need to be unique across the bundle")]
        public string CardbackSprite;
        [Tooltip("Foil mask to use for the card set. This determines where the foil shows on the card")]
        public string FoilMask;
        [Tooltip("Rarities in the set")]
        public List<string> Rarities = new List<string>();
        public List<CardEntry> Cards = new List<CardEntry>();
        [Tooltip("If true, ALL cards in the set will have a foil and non-foil versions.")]
        public bool HasRandomFoils;
        [Tooltip("Chance (0–1) that any given card drawn from this expansion will be foil. Only used when Has Foils is enabled. Leave at 0 to use the default of 0.025 (1-in-40).")]
        public float FoilChance = 0f;
        [Tooltip("How card market prices are calculated for this expansion. 'Default' uses border type as the primary driver with rarity as a modifier. 'RarityDriven' uses rarity as the base with border and foil as light multipliers.")]
        public string CardPriceStrategy = "Default";
        [Tooltip("Card price generation is supplied by a companion API mod via ICardExpansionService.SetCardPriceGenerator.")]
        public bool CardPriceStrategySuppliedByApi;

        [Tooltip("Border multiplier base for Default strategy. Price is multiplied by this value raised to the power of the border type index.")]
        public float DefaultBorderMultiplierBase = 3.15f;
        [Tooltip("Foil multiplier for Default strategy. Foil cards have their price multiplied by this value.")]
        public float DefaultFoilMultiplier = 2.9f;

        [Tooltip("Base floor price for RarityDriven strategy. The minimum price before any rarity stepping is applied.")]
        public float RarityDrivenFloor = 4.0f;
        [Tooltip("Price increase per rarity tier for RarityDriven strategy. Each rarity index adds this amount to the base price.")]
        public float RarityDrivenStepSize = 1026f;
        [Tooltip("Border multiplier base for RarityDriven strategy. Price is multiplied by this value raised to the power of the border type index.")]
        public float RarityDrivenBorderMultiplierBase = 1.15f;
        [Tooltip("Foil multiplier for RarityDriven strategy. Foil cards have their price multiplied by this value.")]
        public float RarityDrivenFoilMultiplier = 1.4f;

        [Tooltip("Material asset names from the bundle to use as card texture sheets on play tables. If provided, customers may use these when playing at tables.")]
        public List<string> PlayCardMaterials = new List<string>();
    }

    [System.Serializable]
    public class CardEntry
    {
        [Tooltip("Name of the card")]
        public string Name;
        [Tooltip("Element type of the card. This only effects the day to day price fluctuations")]
        public string ElementType;
        [Tooltip("Rarity of the card")]
        public string Rarity;
        public int CardNumber;
        [Tooltip("Sprite that is to be used as the card image. Sprite names need to be unique across the bundle")]
        public string Sprite;
        [Tooltip("Border type of the card. This is only used in intial price generation where base border will generate with the least price. Full art will generate the highest prices")]
        public string BorderType;
        [Tooltip("Is this card always foil? Can not be true if the set can have random foils")]
        public bool IsFoil;
        [Tooltip("This cards foil mask if it needs a different foil mask. Will fallback to the sets foilmask if not set")]
        public string FoilMask;
        [Tooltip("This card will use this cardback. If this is blank it will continue using the expansion cardback")]
        public string CardBack;

        [System.NonSerialized]
        public System.Type BorderTypeEnumType;
        [System.NonSerialized]
        public System.Type ElementTypeEnumType;
        [System.NonSerialized]
        public System.Type RarityEnumType;
    }

    [MenuItem("Assets/Build AssetBundles with Verification")]
    static void Init()
    {
        CollectAllBundleData();
        GetWindow<BundleVerificationWindow>("Verify Bundle Data");
    }

    [System.Serializable]
    public class ItemEntry
    {
        public string Name;
        [Tooltip("The shop category the item belongs to")]
        public string ItemCategory;
        [Tooltip("The name of the sprite that will appear in the store, box, and price tags. Sprite names do need to be unique across the bundle.")]
        public string SpriteName;
        [Tooltip("Base cost PER ITEM")]
        public float BaseCost;
        [Tooltip("This is App ID of the custom shop you want to place this item in.")]
        public string PhoneAppId;
        public bool AddItemAsAccessory;
        public bool AddItemAsFigurine;
        public bool AddItemAsBoardGame;
        public bool IsSprayCan;
        [Tooltip("Uniquely identifies this item. HAS TO BE GLOBALLY UNIQUE ACROSS ALL BUNDLES")]
        public string ItemType;
        [Tooltip("The mesh that will be used for this item")]
        public string Mesh;
        [Tooltip("Optional secondary mesh for two-part items (e.g. a box lid). Leave blank if the item has a single mesh.")]
        public string MeshSecondary;
        public bool UsesBaseGameMesh;
        public string MeshToUse;
        [Tooltip("The material that will be used for this item (not required for cardboxes)")]
        public string Material;
        [Tooltip("Optional material for the secondary mesh. Leave blank if the item has a single mesh.")]
        public string MaterialSecondary;
        [Tooltip("List of materials to use for this item. This takes precedence over the previous material. If is cardpack or cardbox, the packs/boxes will be set with a random material from this list")]
        public List<string> MaterialList;
        public bool HasBigBox;
        public bool BigBoxHideTillUnlocked;
        public float BigBoxLicensePrice;
        public int BigBoxLicenseLevelRequirement;
        public bool BigBoxIgnoreDoubleImage;
        public bool HasSmallBox;
        public bool SmallBoxHideTillUnlocked;
        public float SmallBoxLicensePrice;
        public int SmallBoxLicenseLevelRequirement;
        public bool SmallBoxIgnoreDoubleImage;
        public bool IsBulkBox;
        public bool UseCustomMinValue;
        public int MinValue;
        [Tooltip("This is a card box that spawns packs when opened")]
        public bool IsCardBox;
        [Tooltip("This is a card pack that spawns cards when opened")]
        public bool IsCardPack;
        [Tooltip("Pack generation is provided by a companion API mod via SetPackGenerator. Strategy, weights, and pack type are not required.")]
        public bool ApiManaged;
        [Tooltip("Sets box price equal to the price of the pack it produces * 8")]
        public bool AutoSetBoxPrice = false;
        public float MinMarketPricePercent;
        public float MaxMarketPricePercent;
        [Tooltip("Used for cardboxes so that their price are set to 8x the price of the pack they produce")]
        public string FollowItemPrice;
        public bool IsTallItem;
        public float InBoxOffsetY;
        public float InBoxOffsetScale;
        public Vector3 ItemDeminsion;
        public Vector3 CollidorPosOffset;
        public Vector3 ColliderScale;
        public List<string> PriceAffectedBy;
        [Tooltip("The expansion this pack spawns cards from")]
        public string CardExpansion;
        public bool CanHaveDuplicates;
        [Tooltip("How the pack generates cards")]
        public PackGenerationStrategy PackGenerationStrategy = PackGenerationStrategy.Guaranteed;
        [Tooltip("Used in RandomWeighted and Gauranteed generation strategies")]
        public List<GenerationWeightsEntry> GenerationWeights = new List<GenerationWeightsEntry>();
        [Tooltip("Rarity weight table used by AllRandomWeighted. Rarity names must match the linked expansion's rarities.")]
        public List<RarityWeightEntry> NormalWeights = new List<RarityWeightEntry>();
        [Tooltip("Rarity weight table used when a God Pack is triggered.")]
        public List<RarityWeightEntry> GodWeights = new List<RarityWeightEntry>();
        [Tooltip("Per-slot rarity weight tables used by Guaranteed. One entry per card in the pack.")]
        public List<SlotWeightEntry> SlotWeights = new List<SlotWeightEntry>();
        public bool CanHaveGodPacks;
        public float GodPackPercentage = 1.0f / 5000.0f;
        [Tooltip("The Item Type of the pack this box spawns")]
        public string SpawnsPackType;
        [Tooltip("HAS TO BE GLOBALLY UNIQUE. Doesn't currently effect anything important, but is required.")]
        public string PackType;
        [Tooltip("Normal weights are supplied by a companion API mod via IItemService.SetNormalGenerationWeight.")]
        public bool NormalWeightsSuppliedByApi;
        [Tooltip("God pack weights are supplied by a companion API mod via IItemService.SetGodGenerationWeight.")]
        public bool GodWeightsSuppliedByApi;
        [Tooltip("Slot weights are supplied by a companion API mod via IItemService.SetSlotWeight.")]
        public bool SlotWeightsSuppliedByApi;
        [Tooltip("Whether this pack can trigger god packs is supplied by a companion API mod via IItemService.SetCanHaveGodPacks.")]
        public bool CanHaveGodPacksSuppliedByApi;
        [Tooltip("The god pack chance is supplied by a companion API mod via IItemService.SetGodPackPercentage.")]
        public bool GodPackPercentageSuppliedByApi;
        [Tooltip("Min/max market price percent are supplied by a companion API mod via IItemService.SetMarketPricePercent.")]
        public bool MarketPricePercentSuppliedByApi;
        [Tooltip("Follow item price is supplied by a companion API mod via IItemService.SetFollowItemPrice.")]
        public bool FollowItemPriceSuppliedByApi;
        [Tooltip("The box price source is supplied by a companion API mod via IItemService.SetAutoBoxPrice.")]
        public bool AutoBoxPriceSuppliedByApi;
        [Tooltip("License prices are supplied by a companion API mod via IItemService.SetLicensePrice.")]
        public bool LicensePriceSuppliedByApi;
        [Tooltip("License levels are supplied by a companion API mod via IItemService.SetLicenseLevel.")]
        public bool LicenseLevelSuppliedByApi;

        [System.NonSerialized]
        public System.Type PriceAffectedByEnumType;
        [System.NonSerialized]
        public System.Type FollowItemPriceEnumType;
        [System.NonSerialized]
        public System.Type ItemCategoryEnumType;
        [System.NonSerialized]
        public System.Type MeshToUseEnumType;
    }

    [System.Serializable]
    public class PrefabEntry
    {
        public string Name;
        [Tooltip("Name of the Prefab Object. Will not be displayed in game")]
        public string PrefabName;
        [Tooltip("The name of the sprite that will appear in the store, box, and price tags. Sprite names do need to be unique across the bundle.")]
        public string SpriteName;
        [Tooltip("The price it will cost to buy this item in game")]
        public float Price;
        [Tooltip("Level it requires to buy this item in game")]
        public int LevelRequirement;
        [Tooltip("The level requirement is supplied by a companion API mod via IDecoService/IFurnitureService.SetLevelRequirement.")]
        public bool LevelRequirementSuppliedByApi;
        [Tooltip("Does this item belong in the DIY Shop?")]
        public bool AddItemToFurnitureShop;
        public string Description;
        [Tooltip("HAS TO BE GLOBALLY UNIQUE, Does not appear in game UI ever.")]
        public string ObjectType;
        public List<VideoClipEntry> VideoClips;
        [Tooltip("Is this going in the Deco store as a wall texture?")]
        public bool IsDecoWallTexture;
        [Tooltip("Is this going in the Deco store as a floor texture?")]
        public bool IsDecoFloorTexture;
        [Tooltip("Is this going in the Deco store as a ceiling texture?")]
        public bool IsDecoCeilingTexture;
        [Tooltip("Is this a deco statue or sign?")]
        public bool IsDecoObject;
        [Tooltip("HAS TO BE GLOBALLY UNIQUE. Does not appear in game ui.")]
        public string DecoObject;
        public string DecoType;
        [Tooltip("Main texture for a wall, ceiling, or floor deco texture")]
        public string Texture;
        [Tooltip("Roughness texture for a wall, ceiling, or floor deco texture")]
        public string RoughnessMap;
        [Tooltip("Normal map for a wall, ceiling, or floor deco texture")]
        public string NormalMap;
        [Tooltip("The smoothing of the textures.")]
        public float Smoothness;
        public Color Color;
        public bool IsUIPrefab;
        [Tooltip("When scene changes to the title scene the prefab will be instantiated and placed as a child to the scenes Canvas object.")]
        public bool InstantiateInTitle;
        [Tooltip("When scene changes to the start scene the prefab will be instantiated and placed as a child to the scenes Canvas object.")]
        public bool InstantiateInStart;
        public float DecoBonus;

        [System.NonSerialized]
        public System.Type DecoTypeEnumType;
    }

    [System.Serializable]
    public class VideoClipEntry
    {
        public string Name;
        public string VideoPlayerName;
    }



    [System.Serializable]
    public class AssetBundleCatalog
    {
        public string Name;
        public string BundleId;
        public bool AutoCompressTextures;
        public int MaxTextureSize;
        public string TextureCompression;
        public bool EnableMipMaps;
        public bool ItemPricesSuppliedByApi;
        public List<string> Assemblies;
        public List<ItemEntry> Items;
        public List<PrefabEntry> Prefabs;
        public List<CardExpansionEntry> CardExpansions;
        public List<CustomShopEntry> CustomShops;
    }

    static AssetBundleCatalog LoadExistingCatalog(string bundleName)
    {
        string assetBundleRoot = "Assets/AssetBundles";
        string bundleOutputPath = Path.Combine(assetBundleRoot, Path.GetDirectoryName(bundleName));
        string bundleFileName = Path.GetFileName(bundleName);

        // Try the editor catalog file first (list-based weights, JsonUtility-compatible)
        string catalogJsonPath = Path.Combine(bundleOutputPath, $"{bundleFileName}.catalog.json");
        if (File.Exists(catalogJsonPath))
        {
            try
            {
                string json = File.ReadAllText(catalogJsonPath);
                var catalog = JsonUtility.FromJson<AssetBundleCatalog>(json);
                InferApiManagedPacks(catalog?.Items);
                return catalog;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load catalog from {catalogJsonPath}: {e.Message}");
            }
        }

        // Fall back to the bundle descriptor JSON. Dict-format files (new-style item
        // weights) need the manual parser — JsonUtility cannot read them. Old-format
        // files migrate GenerationWeights to the new fields.
        string jsonPath = Path.Combine(bundleOutputPath, $"{bundleFileName}.json");
        if(File.Exists(jsonPath))
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                if (IsDictFormatDescriptorJson(json))
                {
                    var dictCatalog = LoadFromDictFormatDescriptorJson(json);
                    InferApiManagedPacks(dictCatalog?.Items);
                    return dictCatalog;
                }
                var catalog = JsonUtility.FromJson<AssetBundleCatalog>(json);
                if (catalog?.Items is not null)
                {
                    MigrateItemsFromOldFormat(catalog.Items);
                }
                InferApiManagedPacks(catalog?.Items);
                return catalog;
            }
            catch(Exception e)
            {
                Debug.LogError($"Failed to load catalog from {jsonPath}: {e.Message}. Do NOT build this bundle until the descriptor loads — building now would overwrite it with empty data and a regenerated BundleId, orphaning existing save data.");
                return null;
            }
        }

        return null;
    }

    static void CollectAllBundleData()
    {
        bundleData.Clear();
        bundleNames.Clear();
        displayNames.Clear();

        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        var bundleDirectories = allAssetPaths
            .Where(p => Directory.Exists(p) && !string.IsNullOrEmpty(AssetImporter.GetAtPath(p).assetBundleName))
            .ToList();

        foreach(string bundleDir in bundleDirectories)
        {
            string assetBundleName = AssetImporter.GetAtPath(bundleDir).assetBundleName;
            bundleNames.Add(assetBundleName);

            string displayName = Path.GetFileName(bundleDir);
            displayNames.Add(displayName);

            BundleData data = new BundleData();

            // Collect DLL assemblies
            foreach(string dllPath in Directory.GetFiles(bundleDir, "*.dll", SearchOption.AllDirectories))
            {
                data.Assemblies.Add(Path.GetFileName(dllPath));
            }

            var existingCatalog = LoadExistingCatalog(assetBundleName);
            if(existingCatalog is not null)
            {
                if (existingCatalog.Items is not null)
                {
                    data.Items = existingCatalog.Items;
                }
                if (existingCatalog.Prefabs is not null)
                {
                    data.Prefabs = existingCatalog.Prefabs;
                }
                if (existingCatalog.CardExpansions is not null)
                {
                    data.CardExpansions = existingCatalog.CardExpansions;
                }
                if (existingCatalog.CustomShops is not null)
                {
                    data.CustomShops = existingCatalog.CustomShops;
                }
                if (existingCatalog.Assemblies is not null)
                {
                    data.Assemblies = existingCatalog.Assemblies;
                }

                data.BundleName = existingCatalog.Name ?? string.Empty;
                data.BundleId = existingCatalog.BundleId ?? string.Empty;
                data.AutoCompressTextures = existingCatalog.AutoCompressTextures;
                data.MaxTextureSize = existingCatalog.MaxTextureSize;
                data.TextureCompression = existingCatalog.TextureCompression;
                data.EnableMipMaps = existingCatalog.EnableMipMaps;
                data.ItemPricesSuppliedByApi = existingCatalog.ItemPricesSuppliedByApi;
            }

            if (string.IsNullOrEmpty(data.BundleId))
            {
                string namePart = string.IsNullOrEmpty(data.BundleName)
                    ? "bundle"
                    : new string(data.BundleName.ToLower().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
                data.BundleId = $"{namePart}_{Guid.NewGuid():N}";
            }

            bundleData.Add(assetBundleName, data);
        }

        var window = GetWindow<BundleVerificationWindow>();
        if(window)
        {
            foreach(string bundleName in bundleNames)
            {
                window.buildToggles[bundleName] = false;
                window.showItems[bundleName] = true;
                window.showPrefabs[bundleName] = true;
                window.showAssemblies[bundleName] = true;
                window.showCardSets[bundleName] = true;
                window.showCustomShops[bundleName] = true;
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Verify Bundle Data", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if(bundleNames.Count > 0)
        {
            int previousTab = selectedTab;
            selectedTab = EditorGUILayout.Popup("Selected Bundle", selectedTab, displayNames.ToArray());
            string selectedBundle = bundleNames[selectedTab];

            if (previousTab != selectedTab || itemEntryDrawer is null)
            {
                itemEntryDrawer = new ItemEntryDrawer(bundleData);
                prefabEntryDrawer = new PrefabEntryDrawer();
                customShopEntryDrawer = new CustomShopEntryDrawer();
            }

            if(!buildToggles.ContainsKey(selectedBundle))
            {
                buildToggles[selectedBundle] = false;
            }
            buildToggles[selectedBundle] = EditorGUILayout.ToggleLeft("Include in Build", buildToggles[selectedBundle]);

            DisplayBundleContents(selectedBundle);
        }
        else
        {
            EditorGUILayout.HelpBox("No bundles found to verify.", MessageType.Info);
        }

        EditorGUILayout.Space();
        if(bundleNames.Count > 0 && GUILayout.Button(new GUIContent("Save Bundle JSON (no build)", "Writes the .catalog.json and .json descriptor files for the selected bundle without building the asset bundle. Protects unsaved edits from script recompiles and editor restarts.")))
        {
            SaveBundleMetadata(bundleNames[selectedTab], "Assets/AssetBundles");
            AssetDatabase.Refresh();
        }

        if(GUILayout.Button("Build Selected Bundles"))
        {
            BuildAllAssetBundles();
            Close();
        }
    }

    void DisplayBundleContents(string bundleName)
    {
        var currentData = bundleData[bundleName];

        EditorGUILayout.BeginVertical("box");
        currentData.BundleName = EditorGUILayout.TextField(new GUIContent("Bundle Name", "Display name for this bundle. Shown in-game UI where applicable."), currentData.BundleName);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(new GUIContent("Bundle ID", "Stable unique identifier used for save data. Auto-generated — do not edit."), currentData.BundleId);
        EditorGUI.EndDisabledGroup();
        currentData.AutoCompressTextures = EditorGUILayout.Toggle(new GUIContent("Auto Compress Textures", "Applies the texture settings below (max size, compression format, mipmaps, read/write) to every texture in the bundle at build time."), currentData.AutoCompressTextures);
        if (!currentData.AutoCompressTextures)
        {
            EditorGUILayout.HelpBox("Texture optimization is OFF — the settings below will not be applied at build time. Textures ship at their current import settings, which can significantly increase VRAM usage.", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(!currentData.AutoCompressTextures);
        string[] textureSizeOptions = { "2048", "1024", "512", "256" };
        int currentSizeIndex = Array.IndexOf(textureSizeOptions, currentData.MaxTextureSize.ToString());
        if (currentSizeIndex == -1)
        {
            currentSizeIndex = 1; // Default to 1024
        }
        int newSizeIndex = EditorGUILayout.Popup("Max Texture Size", currentSizeIndex, textureSizeOptions);
        currentData.MaxTextureSize = int.Parse(textureSizeOptions[newSizeIndex]);

        string[] compressionOptions = { "DXT5", "BC7" };
        int currentCompressionIndex = Array.IndexOf(compressionOptions, currentData.TextureCompression);
        if (currentCompressionIndex == -1)
        {
            currentCompressionIndex = 0; // Default to DXT5
        }
        int newCompressionIndex = EditorGUILayout.Popup(new GUIContent("Texture Compression", "DXT5: High quality standard compression (opaque textures are packed as DXT1 automatically)\nBC7: Highest quality (newer GPUs)"), currentCompressionIndex, compressionOptions);
        currentData.TextureCompression = compressionOptions[newCompressionIndex];
        currentData.EnableMipMaps = EditorGUILayout.Toggle(new GUIContent("Enable MipMaps", "When enabled, MipMaps are generated for card textures using Kaiser filtering. When disabled, MipMaps are stripped from card and sprite textures."), currentData.EnableMipMaps);
        EditorGUI.EndDisabledGroup();
        currentData.ItemPricesSuppliedByApi = EditorGUILayout.Toggle(new GUIContent("Item Prices Supplied by API", "A companion API mod registers an IItemPriceGenerator for this bundle via IItemService.SetItemPriceGenerator. Market price %, Follow Item Price, and Auto Set Box Price for every item in the bundle are treated as API-supplied."), currentData.ItemPricesSuppliedByApi);
        EditorGUILayout.EndVertical();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if(!showItems.ContainsKey(bundleName))
        {
            showItems[bundleName] = true;
        }
        if(!showPrefabs.ContainsKey(bundleName))
        {
            showPrefabs[bundleName] = true;
        }
        if(!showAssemblies.ContainsKey(bundleName))
        {
            showAssemblies[bundleName] = true;
        }
        if(!showCardSets.ContainsKey(bundleName))
        {
            showCardSets[bundleName] = true;
        }
        if (!showCustomShops.ContainsKey(bundleName))
        {
            showCustomShops[bundleName] = true;
        }

        // Custom Shops Section
        showCustomShops[bundleName] = EditorGUILayout.Foldout(showCustomShops[bundleName], $"Custom Shops ({currentData.CustomShops.Count})", true);
        if (showCustomShops[bundleName])
        {
            customShopEntryDrawer.Draw(currentData.CustomShops);
            if (GUILayout.Button("Add New Custom Shop"))
            {
                currentData.CustomShops.Add(new CustomShopEntry
                {
                    PhoneAppId = null,
                    AppDisplayName = null,
                    AppIcon = null,
                    AppInnerBGImage = null,
                    AppOuterBGImage = null,
                    SiteLogo = null,
                    SiteHeader = null,
                    SiteBackground = null
                });
            }
        }

        EditorGUILayout.Space();

        // Card Sets Section
        showCardSets[bundleName] = EditorGUILayout.Foldout(showCardSets[bundleName], $"Card Sets ({currentData.CardExpansions.Count})", true);
        if(showCardSets[bundleName])
        {
            DisplayCardSets(currentData.CardExpansions);
            if(GUILayout.Button("Add New Card Set"))
            {
                currentData.CardExpansions.Add(new CardExpansionEntry
                {
                    Name = "New Card Set",
                    CardExpansion = "",
                    MenuCategory = "",
                    CardbackSprite = "",
                    FoilMask = "",
                    Rarities = new List<string>(),
                    Cards = new List<CardEntry>(),
                    HasRandomFoils = false,
                    CardPriceStrategy = "Default"
                });
            }
        }

        EditorGUILayout.Space();

        // Items Section
        showItems[bundleName] = EditorGUILayout.Foldout(showItems[bundleName], $"Items ({currentData.Items.Count})", true);
        if(showItems[bundleName])
        {
            itemEntryDrawer.Draw(currentData.Items, currentData);
            if(GUILayout.Button("Add New Item"))
            {
                currentData.Items.Add(new ItemEntry
                {
                    Name = "New Item",
                    ItemCategory = "",
                    SpriteName = "",
                    BaseCost = 0,
                    PhoneAppId = "",
                    AddItemAsAccessory = false,
                    AddItemAsFigurine = false,
                    AddItemAsBoardGame = false,
                    IsSprayCan = false,
                    ItemType = "",
                    Mesh = "",
                    UsesBaseGameMesh = false,
                    Material = "",
                    MaterialList = new List<string>(),
                    HasBigBox = false,
                    BigBoxHideTillUnlocked = false,
                    BigBoxLicensePrice = 0,
                    BigBoxLicenseLevelRequirement = 0,
                    BigBoxIgnoreDoubleImage = false,
                    HasSmallBox = false,
                    SmallBoxHideTillUnlocked = false,
                    SmallBoxLicensePrice = 0,
                    SmallBoxLicenseLevelRequirement = 0,
                    SmallBoxIgnoreDoubleImage = false,
                    IsBulkBox = false,
                    UseCustomMinValue = false,
                    MinValue = 0,
                    IsCardBox = false,
                    IsCardPack = false,
                    MinMarketPricePercent = 1.0f,
                    MaxMarketPricePercent = 1.2f,
                    FollowItemPrice = "",
                    IsTallItem = false,
                    InBoxOffsetY = 0,
                    InBoxOffsetScale = 0,
                    ItemDeminsion = new Vector3(1, 1, 1),
                    CollidorPosOffset = new Vector3(0, 0, 0),
                    ColliderScale = new Vector3(1, 1, 1),
                    PriceAffectedBy = new List<string>(),
                    CardExpansion = "",
                    CanHaveDuplicates = false,
                    PackGenerationStrategy = PackGenerationStrategy.Guaranteed,
                    GenerationWeights = new List<GenerationWeightsEntry>(),
                    NormalWeights = new List<RarityWeightEntry>(),
                    GodWeights = new List<RarityWeightEntry>(),
                    SlotWeights = new List<SlotWeightEntry>(),
                    CanHaveGodPacks = false,
                    SpawnsPackType = "",
                    PackType = ""
                });
            }
        }

        EditorGUILayout.Space();

        // Prefabs Section
        showPrefabs[bundleName] = EditorGUILayout.Foldout(showPrefabs[bundleName], $"Prefabs & Decos ({currentData.Prefabs.Count})", true);
        if(showPrefabs[bundleName])
        {
            prefabEntryDrawer.Draw(currentData.Prefabs);
            if(GUILayout.Button("Add New Prefab/Deco"))
            {
                currentData.Prefabs.Add(new PrefabEntry
                {
                    Name = "New Prefab",
                    PrefabName = "",
                    AddItemToFurnitureShop = false,
                    SpriteName = "",
                    Description = "",
                    LevelRequirement = 1,
                    Price = 0,
                    ObjectType = "",
                    VideoClips = new List<VideoClipEntry>(),
                    IsDecoWallTexture = false,
                    IsDecoFloorTexture = false,
                    IsDecoCeilingTexture = false,
                    IsDecoObject = false,
                    DecoObject = "",
                    DecoType = "",
                    Texture = "",
                    RoughnessMap = "",
                    NormalMap = "",
                    Smoothness = 0.5f,
                    Color = Color.white,
                    IsUIPrefab = false,
                    InstantiateInTitle = false,
                    InstantiateInStart = false,
                    DecoBonus = 0.0f
                });
            }
        }

        EditorGUILayout.Space();

        // Assemblies Section
        if(currentData.Assemblies.Count > 0)
        {
            showAssemblies[bundleName] = EditorGUILayout.Foldout(showAssemblies[bundleName], $"Assemblies ({currentData.Assemblies.Count})", true);
            if(showAssemblies[bundleName])
            {
                EditorGUI.indentLevel++;
                for(int i = 0; i < currentData.Assemblies.Count; i++)
                {
                    EditorGUILayout.BeginVertical("box");
                    var assembly = currentData.Assemblies[i];
                    assembly = EditorGUILayout.TextField("DLL Name", assembly);
                    EditorGUILayout.EndVertical();
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndScrollView();
    }




    private class CustomShopEntryDrawer
    {
        private Dictionary<string, bool> expandedCustomShops = new Dictionary<string, bool>();
        private const float C_LABEL_WIDTH = 200f;

        private string DrawTextField(GUIContent label, string value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.TextField(fieldRect, value);
        }

        public void Draw(List<CustomShopEntry> customShops)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < customShops.Count; i++)
            {
                string customShopKey = $"customshop_{i}";
                if (!expandedCustomShops.ContainsKey(customShopKey))
                {
                    expandedCustomShops[customShopKey] = false;
                }

                EditorGUILayout.BeginHorizontal();
                expandedCustomShops[customShopKey] = EditorGUILayout.Foldout(expandedCustomShops[customShopKey], customShops[i].PhoneAppId ?? "New Custom Shop", true);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    customShops.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (!expandedCustomShops[customShopKey])
                {
                    continue;
                }

                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;

                var customShop = customShops[i];

                customShop.PhoneAppId = DrawTextField(new GUIContent("Phone App Id", "Unique ID (Has to be the name of your folder in Phone Overhaul App)"), customShop.PhoneAppId);
                customShop.AppDisplayName = DrawTextField(new GUIContent("App Display Name", "The name that will display under your app on the phone"), customShop.AppDisplayName);
                customShop.AppIcon = DrawTextField(new GUIContent("App Icon", "The name of the main Icon for your phone app (no extension)"), customShop.AppIcon);
                customShop.AppInnerBGImage = DrawTextField(new GUIContent("App Inner BG Image", "The name of the inner background image for your phone app (no extension)"), customShop.AppInnerBGImage);
                customShop.AppOuterBGImage = DrawTextField(new GUIContent("App Outer BG Image", "The name of the outer background image for your phone app (no extension)"), customShop.AppOuterBGImage);
                customShop.SiteLogo = DrawTextField(new GUIContent("Site Logo", "The name of the sprite to use in the store header"), customShop.SiteLogo);
                customShop.SiteHeader = DrawTextField(new GUIContent("Site Header", "The name of the sprite to use as the store header"), customShop.SiteHeader);
                customShop.SiteBackground = DrawTextField(new GUIContent("Site Background", "The name of the sprite to use as the stores background"), customShop.SiteBackground);

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUI.indentLevel--;
        }
    }

    private class ItemEntryDrawer
    {
        private Dictionary<string, bool> expandedItems = new Dictionary<string, bool>();
        private Dictionary<string, BundleData> bundleData;
        private const float C_LABEL_WIDTH = 200f;

        public ItemEntryDrawer(Dictionary<string, BundleData> bundleData)
        {
            this.bundleData = bundleData;
        }

        public void Draw(List<ItemEntry> items, BundleData currentBundle)
        {
            bool bundleItemPricesApi = currentBundle.ItemPricesSuppliedByApi;
            EditorGUI.indentLevel++;

            for (int i = 0; i < items.Count; i++)
            {
                string itemKey = $"item_{i}";
                if (!expandedItems.ContainsKey(itemKey))
                {
                    expandedItems[itemKey] = false;
                }

                EditorGUILayout.BeginHorizontal();
                expandedItems[itemKey] = EditorGUILayout.Foldout(expandedItems[itemKey], items[i].Name, true);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    items.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (!expandedItems[itemKey])
                {
                    continue;
                }

                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;

                var item = items[i];

                item.Name = DrawTextField(new GUIContent("Name"), item.Name);
                item.PhoneAppId = DrawTextField(new GUIContent("Phone App Id", "This is App ID of the custom shop you want to place this item in."), item.PhoneAppId);

                if (item.ItemCategoryEnumType is null)
                {
                    item.ItemCategoryEnumType = Type.GetType("EItemCategory, Assembly-CSharp");
                }
                if (item.ItemCategoryEnumType is not null && item.ItemCategoryEnumType.IsEnum)
                {
                    Enum currentCategory = null;
                    try { currentCategory = (Enum)Enum.Parse(item.ItemCategoryEnumType, item.ItemCategory ?? ""); } catch { }
                    if (currentCategory is null) { currentCategory = (Enum)Enum.GetValues(item.ItemCategoryEnumType).GetValue(0); }
                    item.ItemCategory = DrawEnumPopup(new GUIContent("Category", "The shop category the item belongs to"), currentCategory).ToString();
                }
                else
                {
                    item.ItemCategory = DrawTextField(new GUIContent("Category"), item.ItemCategory);
                    EditorGUILayout.HelpBox("Could not find EItemCategory enum", MessageType.Warning);
                }

                item.SpriteName = DrawTextField(new GUIContent("Sprite", "The name of the sprite that will appear in the store, box, and price tags. Sprite names do need to be unique across the bundle."), item.SpriteName);
                item.ItemType = DrawTextField(new GUIContent("Item Type", "Uniquely identifies this item. HAS TO BE GLOBALLY UNIQUE ACROSS ALL BUNDLES"), item.ItemType);

                bool isTCG = item.ItemCategory == "TCG";
                if (isTCG)
                {
                    bool newIsBulkBox = item.IsBulkBox;
                    bool newIsCardBox = item.IsCardBox;
                    bool newIsCardPack = item.IsCardPack;

                    if (!newIsCardBox && !newIsCardPack)
                    {
                        newIsBulkBox = DrawToggle(new GUIContent("Is Bulk Box"), newIsBulkBox);
                    }
                    if (!newIsBulkBox && !newIsCardPack)
                    {
                        newIsCardBox = DrawToggle(new GUIContent("Is Booster Box", "This is a card box that spawns packs when opened"), newIsCardBox);
                    }
                    if (!newIsBulkBox && !newIsCardBox)
                    {
                        newIsCardPack = DrawToggle(new GUIContent("Is Booster Pack", "This is a card pack that spawns cards when opened"), newIsCardPack);
                    }

                    if (newIsBulkBox != item.IsBulkBox) { item.IsBulkBox = newIsBulkBox; if (item.IsBulkBox) { item.IsCardBox = false; item.IsCardPack = false; item.UsesBaseGameMesh = true; item.MeshToUse = "BulkBox_TetramonBase"; } else { item.UsesBaseGameMesh = false; item.MeshToUse = ""; } }
                    if (newIsCardBox != item.IsCardBox) { item.IsCardBox = newIsCardBox; if (item.IsCardBox) { item.IsBulkBox = false; item.IsCardPack = false; item.UsesBaseGameMesh = true; item.MeshToUse = "BasicCardBox"; } else { item.UsesBaseGameMesh = false; item.MeshToUse = ""; } }
                    if (newIsCardPack != item.IsCardPack) { item.IsCardPack = newIsCardPack; if (item.IsCardPack) { item.IsBulkBox = false; item.IsCardBox = false; item.UsesBaseGameMesh = true; item.MeshToUse = "BasicCardPack"; } else { item.UsesBaseGameMesh = false; item.MeshToUse = ""; } }
                }
                else { item.IsBulkBox = false; item.IsCardBox = false; item.IsCardPack = false; }

                if (item.IsBulkBox)
                {
                    item.HasBigBox = false; item.BigBoxHideTillUnlocked = false; item.BigBoxLicensePrice = 0; item.BigBoxLicenseLevelRequirement = 0; item.BigBoxIgnoreDoubleImage = false; item.HasSmallBox = false; item.SmallBoxHideTillUnlocked = false; item.SmallBoxLicensePrice = 0; item.SmallBoxLicenseLevelRequirement = 0; item.SmallBoxIgnoreDoubleImage = false; item.AddItemAsAccessory = false; item.AddItemAsFigurine = false; item.AddItemAsBoardGame = false; item.IsSprayCan = false;

                    EditorGUILayout.LabelField("Bulk Box Settings", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical("box");
                    item.UseCustomMinValue = DrawToggle(new GUIContent("Use Custom Min Value"), item.UseCustomMinValue);
                    if (item.UseCustomMinValue)
                    {
                        item.MinValue = DrawIntField(new GUIContent("Min Value"), item.MinValue);
                    }
                    else
                    {
                        BulkBoxType bulkBoxType = GetBulkBoxTypeFromValue(item.MinValue);
                        bulkBoxType = (BulkBoxType)DrawEnumPopup(new GUIContent("Box Type"), bulkBoxType);
                        item.MinValue = GetValueFromBulkBoxType(bulkBoxType);
                    }

                    string[] cardExpansionNames = bundleData.Values.SelectMany(b => b.CardExpansions).Select(cs => cs.CardExpansion).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
                    int currentCardExpansionIndex = Array.IndexOf(cardExpansionNames, item.CardExpansion);
                    if (currentCardExpansionIndex < 0)
                    {
                        currentCardExpansionIndex = 0;
                    }

                    if (cardExpansionNames.Length > 0)
                    {
                        currentCardExpansionIndex = DrawPopup(new GUIContent("Card Expansion", "The expansion this pack spawns cards from"), currentCardExpansionIndex, cardExpansionNames);
                        item.CardExpansion = cardExpansionNames[currentCardExpansionIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Card Expansions found in bundles", MessageType.Warning);
                        item.CardExpansion = DrawTextField(new GUIContent("Card Expansion Name"), item.CardExpansion);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }
                else
                {
                    item.HasSmallBox = DrawToggle(new GUIContent("Has Small Box"), item.HasSmallBox);
                    item.HasBigBox = DrawToggle(new GUIContent("Has Big Box"), item.HasBigBox);

                    if (item.HasSmallBox || item.HasBigBox)
                    {
                        item.LicensePriceSuppliedByApi = DrawToggle(new GUIContent("License Price Supplied by API", "License prices are supplied by a companion API mod via IItemService.SetLicensePrice."), item.LicensePriceSuppliedByApi);
                        item.LicenseLevelSuppliedByApi = DrawToggle(new GUIContent("License Level Supplied by API", "License levels are supplied by a companion API mod via IItemService.SetLicenseLevel."), item.LicenseLevelSuppliedByApi);
                    }

                    if (item.HasSmallBox)
                    {
                        EditorGUILayout.LabelField("Small Box Settings", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical("box");
                        if (!item.LicensePriceSuppliedByApi)
                        {
                            item.SmallBoxLicensePrice = DrawFloatField(new GUIContent("License Price", "Price that it costs to unlock this item in the small box"), item.SmallBoxLicensePrice);
                        }
                        if (!item.LicenseLevelSuppliedByApi)
                        {
                            item.SmallBoxLicenseLevelRequirement = DrawIntField(new GUIContent("License Level", "Level required to unlock this item in the small box"), item.SmallBoxLicenseLevelRequirement);
                        }
                        item.SmallBoxHideTillUnlocked = DrawToggle(new GUIContent("Hide Until Unlocked", "Sets the sprite and name to a Question Mark until the player buys the licence."), item.SmallBoxHideTillUnlocked);
                        item.SmallBoxIgnoreDoubleImage = DrawToggle(new GUIContent("Ignore Double Image"), item.SmallBoxIgnoreDoubleImage);
                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }

                    if (item.HasBigBox)
                    {
                        EditorGUILayout.LabelField("Big Box Settings", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical("box");
                        if (!item.LicensePriceSuppliedByApi)
                        {
                            item.BigBoxLicensePrice = DrawFloatField(new GUIContent("License Price", "Price that it costs to unlock this item in the big box"), item.BigBoxLicensePrice);
                        }
                        if (!item.LicenseLevelSuppliedByApi)
                        {
                            item.BigBoxLicenseLevelRequirement = DrawIntField(new GUIContent("License Level", "Level required to unlock this item in the big box"), item.BigBoxLicenseLevelRequirement);
                        }
                        item.BigBoxHideTillUnlocked = DrawToggle(new GUIContent("Hide Until Unlocked", "Sets the sprite and name to a Question Mark until the player buys the licence."), item.BigBoxHideTillUnlocked);
                        item.BigBoxIgnoreDoubleImage = DrawToggle(new GUIContent("Ignore Double Image"), item.BigBoxIgnoreDoubleImage);
                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }

                    bool isBoosterBoxOrPack = item.IsCardBox || item.IsCardPack;
                    if (!isBoosterBoxOrPack && !isTCG)
                    {
                        item.AddItemAsAccessory = DrawToggle(new GUIContent("Add to Accessory store"), item.AddItemAsAccessory);
                        item.AddItemAsFigurine = DrawToggle(new GUIContent("Add to Figurine store"), item.AddItemAsFigurine);
                        item.AddItemAsBoardGame = DrawToggle(new GUIContent("Add to Board Game store"), item.AddItemAsBoardGame);
                        item.IsSprayCan = DrawToggle(new GUIContent("Is Spray Can"), item.IsSprayCan);
                    }
                    else { item.AddItemAsAccessory = false; item.AddItemAsFigurine = false; item.AddItemAsBoardGame = false; item.IsSprayCan = false; }
                }

                EditorGUILayout.LabelField("Price Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("box");

                if (bundleItemPricesApi)
                {
                    EditorGUILayout.HelpBox("Item prices are supplied by the API for this bundle (IItemService.SetItemPriceGenerator). Market Price %, Follow Item Price, and Auto Set Box Price are covered by that bundle-level setting.", MessageType.Info);
                }

                if (item.IsCardBox && !bundleItemPricesApi)
                {
                    item.AutoBoxPriceSuppliedByApi = DrawToggle(new GUIContent("Auto Box Price Supplied by API", "The box price source is supplied by a companion API mod via IItemService.SetAutoBoxPrice."), item.AutoBoxPriceSuppliedByApi);
                }
                if (item.IsCardBox && !bundleItemPricesApi && !item.AutoBoxPriceSuppliedByApi)
                {
                    if (item.FollowItemPrice == "None")
                    {
                        item.AutoSetBoxPrice = DrawToggle(new GUIContent("Auto Set Box Price", "Sets box price equal to the price of the pack it produces * 8"), item.AutoSetBoxPrice);
                    }
                    else
                    {
                        item.AutoSetBoxPrice = false;
                    }
                }

                if (!bundleItemPricesApi)
                {
                    item.FollowItemPriceSuppliedByApi = DrawToggle(new GUIContent("Follow Item Price Supplied by API", "Follow item price is supplied by a companion API mod via IItemService.SetFollowItemPrice."), item.FollowItemPriceSuppliedByApi);
                }

                bool followItemPriceApiSupplied = bundleItemPricesApi || item.FollowItemPriceSuppliedByApi;
                if (!item.AutoSetBoxPrice && !followItemPriceApiSupplied)
                {
                    if (item.FollowItemPriceEnumType is null)
                    {
                        item.FollowItemPriceEnumType = Type.GetType("EItemType, Assembly-CSharp");
                    }
                    if (item.FollowItemPriceEnumType is not null && item.FollowItemPriceEnumType.IsEnum)
                    {
                        var enumValues = new List<string> { "None" };
                        enumValues.AddRange(Enum.GetNames(item.FollowItemPriceEnumType));

                        int selectedIndex = enumValues.IndexOf(item.FollowItemPrice);
                        if (selectedIndex < 0)
                        {
                            selectedIndex = 0;
                        }

                        selectedIndex = DrawPopup(new GUIContent("Follow Item Price", "Set this items Base Cost and Min/Max Market price percentages equal to the same as the item selected"), selectedIndex, enumValues.ToArray());
                        item.FollowItemPrice = enumValues[selectedIndex];
                    }
                    else
                    {
                        item.FollowItemPrice = DrawTextField(new GUIContent("Follow Item Price"), item.FollowItemPrice);
                        EditorGUILayout.HelpBox("Could not find EItemType enum", MessageType.Warning);
                    }
                }

                bool followsAnotherItem = !followItemPriceApiSupplied && !string.IsNullOrEmpty(item.FollowItemPrice) && item.FollowItemPrice != "None";
                if (!followsAnotherItem)
                {
                    if (item.IsCardBox && item.AutoSetBoxPrice)
                    {
                        item.BaseCost = 0;
                    }
                    else
                    {
                        item.BaseCost = DrawFloatField(new GUIContent("Base Cost", "Base cost PER ITEM"), item.BaseCost);
                    }

                    if (!bundleItemPricesApi)
                    {
                        item.MarketPricePercentSuppliedByApi = DrawToggle(new GUIContent("Market Price % Supplied by API", "Min/max market price percent are supplied by a companion API mod via IItemService.SetMarketPricePercent."), item.MarketPricePercentSuppliedByApi);
                    }
                    if (!bundleItemPricesApi && !item.MarketPricePercentSuppliedByApi)
                    {
                        item.MinMarketPricePercent = DrawFloatField(new GUIContent("Min Market Price %"), item.MinMarketPricePercent);
                        item.MaxMarketPricePercent = DrawFloatField(new GUIContent("Max Market Price %"), item.MaxMarketPricePercent);
                    }
                }
                else
                {
                    item.BaseCost = 0;
                    item.MinMarketPricePercent = 1.0f;
                    item.MaxMarketPricePercent = 1.2f;
                }

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Price Affected By", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("box");
                if (item.PriceAffectedByEnumType is null)
                {
                    item.PriceAffectedByEnumType = Type.GetType("EPriceChangeType, Assembly-CSharp");
                }
                if (item.PriceAffectedByEnumType is not null && item.PriceAffectedByEnumType.IsEnum)
                {
                    for (int j = 0; j < item.PriceAffectedBy.Count; j++)
                    {
                        Rect lineRect = EditorGUILayout.GetControlRect();
                        Rect fieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - 85, lineRect.height);
                        Rect buttonRect = new Rect(fieldRect.xMax + 5, lineRect.y, 80, lineRect.height);
                        
                        Enum currentValue = null;
                        try { currentValue = (Enum)Enum.Parse(item.PriceAffectedByEnumType, item.PriceAffectedBy[j]); } catch{}
                        if(currentValue is not null)
                        {
                            Enum newValue = EditorGUI.EnumPopup(fieldRect, currentValue);
                            item.PriceAffectedBy[j] = newValue.ToString();
                        }
                        else
                        {
                           item.PriceAffectedBy[j] = EditorGUI.TextField(fieldRect, item.PriceAffectedBy[j]);
                        }

                        if (GUI.Button(buttonRect, "Remove")) { item.PriceAffectedBy.RemoveAt(j); j--; }
                    }
                    if (GUILayout.Button("Add Price Affected By")) { Array enumValues = Enum.GetValues(item.PriceAffectedByEnumType); if (enumValues.Length > 0) { item.PriceAffectedBy.Add(enumValues.GetValue(0).ToString()); } }
                }
                else
                {
                    EditorGUILayout.HelpBox("Could not find EPriceChangeType enum", MessageType.Warning);
                     for (int j = 0; j < item.PriceAffectedBy.Count; j++)
                    {
                        Rect lineRect = EditorGUILayout.GetControlRect();
                        Rect fieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - 85, lineRect.height);
                        Rect buttonRect = new Rect(fieldRect.xMax + 5, lineRect.y, 80, lineRect.height);
                        item.PriceAffectedBy[j] = EditorGUI.TextField(fieldRect, item.PriceAffectedBy[j]);
                        if (GUI.Button(buttonRect, "Remove")) { item.PriceAffectedBy.RemoveAt(j); j--; }
                    }
                    if (GUILayout.Button("Add Price Affected By")) { item.PriceAffectedBy.Add(""); }
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Item Configuration", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("box");
                item.Material = DrawTextField(new GUIContent("Material", "The material that will be used for this item (not required for cardboxes)"), item.Material);
                item.MaterialSecondary = DrawTextField(new GUIContent("Material Secondary", "Optional material for the secondary mesh. Leave blank if the item has a single mesh."), item.MaterialSecondary);
                EditorGUILayout.LabelField(new GUIContent("Material List", "List of materials to use for this item..."), EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("box");
                for (int j = 0; j < item.MaterialList.Count; j++)
                {
                    Rect lineRect = EditorGUILayout.GetControlRect();
                    Rect fieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - 85, lineRect.height);
                    Rect buttonRect = new Rect(fieldRect.xMax + 5, lineRect.y, 80, lineRect.height);
                    item.MaterialList[j] = EditorGUI.TextField(fieldRect, $"Material {j + 1}", item.MaterialList[j]);
                    if (GUI.Button(buttonRect, "Remove")) { item.MaterialList.RemoveAt(j); j--; }
                }
                if (GUILayout.Button("Add Material")) { item.MaterialList.Add(""); }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                
                item.UsesBaseGameMesh = DrawToggle(new GUIContent("Uses Base Game Mesh"), item.UsesBaseGameMesh);
                if (item.UsesBaseGameMesh)
                {
                    item.Mesh = ""; item.MeshSecondary = ""; item.MaterialSecondary = ""; item.IsTallItem = false; item.InBoxOffsetY = 0; item.InBoxOffsetScale = 0; item.CollidorPosOffset = Vector3.zero; item.ColliderScale = Vector3.one; item.ItemDeminsion = Vector3.one;
                    if (item.MeshToUseEnumType is null)
                    {
                        item.MeshToUseEnumType = Type.GetType("EItemType, Assembly-CSharp");
                    }
                    if (item.MeshToUseEnumType is not null && item.MeshToUseEnumType.IsEnum)
                    {
                        Enum currentMeshValue = null;
                        try { currentMeshValue = (Enum)Enum.Parse(item.MeshToUseEnumType, item.MeshToUse ?? ""); } catch { }
                        if (currentMeshValue is null) { currentMeshValue = (Enum)Enum.GetValues(item.MeshToUseEnumType).GetValue(0); }
                        item.MeshToUse = DrawEnumPopup(new GUIContent("Item Mesh To Use"), currentMeshValue).ToString();
                    }
                    else
                    {
                        item.MeshToUse = DrawTextField(new GUIContent("Item Mesh To Use"), item.MeshToUse);
                        EditorGUILayout.HelpBox("Could not find EItemType enum", MessageType.Warning);
                    }
                }
                else
                {
                    item.MeshToUse = "";
                    item.Mesh = DrawTextField(new GUIContent("Mesh", "The mesh that will be used for this item"), item.Mesh);
                    item.MeshSecondary = DrawTextField(new GUIContent("Mesh Secondary", "Optional secondary mesh for two-part items (e.g. a box lid). Leave blank if the item has a single mesh."), item.MeshSecondary);
                    item.IsTallItem = DrawToggle(new GUIContent("Is Tall Item"), item.IsTallItem);
                    item.InBoxOffsetY = DrawFloatField(new GUIContent("In Box Y Offset"), item.InBoxOffsetY);
                    item.InBoxOffsetScale = DrawFloatField(new GUIContent("In Box Scale"), item.InBoxOffsetScale);
                    item.CollidorPosOffset = EditorGUILayout.Vector3Field("Collider Offset", item.CollidorPosOffset);
                    item.ColliderScale = EditorGUILayout.Vector3Field("Collider Scale", item.ColliderScale);
                    item.ItemDeminsion = EditorGUILayout.Vector3Field("Item Dimensions", item.ItemDeminsion);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();

                if (item.IsCardBox)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Booster Box Configuration", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical("box");
                    string[] boosterPackNames = bundleData.Values.SelectMany(b => b.Items).Where(it => it.IsCardPack).Select(it => it.ItemType).ToArray();
                    int currentPackIndex = Array.IndexOf(boosterPackNames, item.SpawnsPackType);
                    if (currentPackIndex < 0)
                    {
                        currentPackIndex = 0;
                    }

                    if (boosterPackNames.Length > 0)
                    {
                        currentPackIndex = DrawPopup(new GUIContent("Spawns Packs", "The Item Type of the pack this box spawns"), currentPackIndex, boosterPackNames);
                        item.SpawnsPackType = boosterPackNames[currentPackIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Booster Packs found", MessageType.Warning);
                        item.SpawnsPackType = DrawTextField(new GUIContent("Spawns Packs"), item.SpawnsPackType);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }
                else { item.SpawnsPackType = ""; }

                if (item.IsCardPack)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Booster Pack Configuration", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical("box");
                    var allExpansionEntries = bundleData.Values.SelectMany(b => b.CardExpansions)
                        .Where(cs => !string.IsNullOrEmpty(cs.Name) || !string.IsNullOrEmpty(cs.CardExpansion))
                        .ToArray();
                    string[] cardExpansionDisplayNames = allExpansionEntries
                        .Select(cs => string.IsNullOrEmpty(cs.Name) ? cs.CardExpansion : cs.Name)
                        .ToArray();
                    string[] cardExpansionValues = allExpansionEntries
                        .Select(cs => string.IsNullOrEmpty(cs.CardExpansion) ? cs.Name : cs.CardExpansion)
                        .ToArray();
                    int currentCardExpansionIndex = Array.IndexOf(cardExpansionValues, item.CardExpansion);
                    if (currentCardExpansionIndex < 0)
                    {
                        currentCardExpansionIndex = 0;
                    }
                    if (allExpansionEntries.Length > 0)
                    {
                        currentCardExpansionIndex = DrawPopup(new GUIContent("Card Expansion", "The expansion this pack spawns cards from"), currentCardExpansionIndex, cardExpansionDisplayNames);
                        item.CardExpansion = cardExpansionValues[currentCardExpansionIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Card Expansions found in bundles", MessageType.Warning);
                        item.CardExpansion = DrawTextField(new GUIContent("Card Expansion Name"), item.CardExpansion);
                    }

                    item.ApiManaged = DrawToggle(new GUIContent("API Managed", "Pack generation is provided by a companion API mod via SetPackGenerator. Strategy, weights, and pack type are not required."), item.ApiManaged);
                    if (!item.ApiManaged)
                    {
                        item.CanHaveDuplicates = DrawToggle(new GUIContent("Can Have Duplicates"), item.CanHaveDuplicates);
                        item.CanHaveGodPacksSuppliedByApi = DrawToggle(new GUIContent("God Packs Supplied by API", "Whether this pack can trigger god packs is supplied by a companion API mod via IItemService.SetCanHaveGodPacks."), item.CanHaveGodPacksSuppliedByApi);
                        if (!item.CanHaveGodPacksSuppliedByApi)
                        {
                            item.CanHaveGodPacks = DrawToggle(new GUIContent("Can Have God Packs"), item.CanHaveGodPacks);
                        }
                        if (item.CanHaveGodPacks || item.CanHaveGodPacksSuppliedByApi)
                        {
                            item.GodPackPercentageSuppliedByApi = DrawToggle(new GUIContent("God Pack Chance Supplied by API", "The god pack chance is supplied by a companion API mod via IItemService.SetGodPackPercentage."), item.GodPackPercentageSuppliedByApi);
                            if (!item.GodPackPercentageSuppliedByApi)
                            {
                                item.GodPackPercentage = DrawFloatField(new GUIContent("God Pack Chance", "To get an 'N in X' chance, the value should be 'N / X'. For example, a 1 in 5000 chance is 1 / 5000 = 0.0002."), item.GodPackPercentage);
                            }
                        }
                        item.PackGenerationStrategy = (PackGenerationStrategy)DrawEnumPopup(new GUIContent("Pack Generation Strategy", "How the pack generates cards"), item.PackGenerationStrategy);
                        item.PackType = DrawTextField(new GUIContent("Pack Type", "HAS TO BE GLOBALLY UNIQUE. Doesn't currently effect anything important, but is required."), item.PackType);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space();

                    if (item.ApiManaged)
                    {
                        EditorGUILayout.HelpBox("Pack generation is API managed. The companion mod must assign a generator via IItemService.SetPackGenerator once bundle loading completes.", MessageType.Info);
                    }
                    else if (item.PackGenerationStrategy == PackGenerationStrategy.AllRandom)
                    {
                        EditorGUILayout.HelpBox("AllRandom packs select cards completely at random. No weights configuration is needed.", MessageType.Info);
                    }
                    else
                    {
                        CardExpansionEntry selectedCardExpansion = string.IsNullOrEmpty(item.CardExpansion) ? null
                            : bundleData.Values.SelectMany(b => b.CardExpansions).FirstOrDefault(cs => cs.CardExpansion == item.CardExpansion || cs.Name == item.CardExpansion);
                        var expansionRarities = selectedCardExpansion?.Rarities.Where(r => !string.IsNullOrEmpty(r)).ToList() ?? new List<string>();

                        if (expansionRarities.Count == 0)
                        {
                            EditorGUILayout.HelpBox("Add rarities to the selected Card Expansion to configure generation weights.", MessageType.Warning);
                        }
                        else
                        {
                            if (item.PackGenerationStrategy == PackGenerationStrategy.AllRandomWeighted)
                            {
                                item.NormalWeightsSuppliedByApi = DrawToggle(new GUIContent("Normal Weights Supplied by API", "Normal weights are supplied by a companion API mod via IItemService.SetNormalGenerationWeight."), item.NormalWeightsSuppliedByApi);
                                if (!item.NormalWeightsSuppliedByApi)
                                {
                                    SyncRarityWeightList(item.NormalWeights, expansionRarities);

                                    EditorGUILayout.LabelField(new GUIContent("Normal Weights", "Rarity distribution for all cards drawn from this pack."), EditorStyles.boldLabel);
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.BeginVertical("box");
                                    DrawRarityWeightTable(item.NormalWeights, expansionRarities);
                                    EditorGUILayout.EndVertical();
                                    EditorGUI.indentLevel--;
                                }
                            }
                            else if (item.PackGenerationStrategy == PackGenerationStrategy.Guaranteed)
                            {
                                item.SlotWeightsSuppliedByApi = DrawToggle(new GUIContent("Slot Weights Supplied by API", "Slot weights are supplied by a companion API mod via IItemService.SetSlotWeight."), item.SlotWeightsSuppliedByApi);
                                if (!item.SlotWeightsSuppliedByApi)
                                {
                                    // Ensure all 7 slots are always present in order
                                    var allSlots = new[] { Slots.Slot1, Slots.Slot2, Slots.Slot3, Slots.Slot4, Slots.Slot5, Slots.Slot6, Slots.Slot7 };
                                    foreach (var slot in allSlots)
                                    {
                                        if (!item.SlotWeights.Any(sw => sw.Slot == slot))
                                        {
                                            item.SlotWeights.Add(new SlotWeightEntry { Slot = slot, Weights = new List<RarityWeightEntry>() });
                                        }
                                    }
                                    item.SlotWeights.Sort((a, b) => Array.IndexOf(allSlots, a.Slot).CompareTo(Array.IndexOf(allSlots, b.Slot)));
                                    foreach (var sw in item.SlotWeights)
                                    {
                                        SyncRarityWeightList(sw.Weights, expansionRarities);
                                    }

                                    EditorGUILayout.LabelField(new GUIContent("Slot Weights", "Each slot draws one card using its own rarity weight table."), EditorStyles.boldLabel);
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.BeginVertical("box");

                                    const float C_SLOT_RARITY_WIDTH = 150f;

                                    // Header row
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUI.indentLevel--;
                                    EditorGUILayout.LabelField("Rarity", EditorStyles.boldLabel, GUILayout.Width(C_SLOT_RARITY_WIDTH));
                                    foreach (var sw in item.SlotWeights)
                                    {
                                        EditorGUILayout.LabelField(sw.Slot.ToString(), EditorStyles.boldLabel, GUILayout.Width(70));
                                    }
                                    EditorGUI.indentLevel++;
                                    GUILayout.FlexibleSpace();
                                    EditorGUILayout.EndHorizontal();

                                    // One row per rarity
                                    foreach (var rarity in expansionRarities)
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUI.indentLevel--;
                                        EditorGUILayout.LabelField(rarity, GUILayout.Width(C_SLOT_RARITY_WIDTH));
                                        foreach (var sw in item.SlotWeights)
                                        {
                                            var entry = sw.Weights.FirstOrDefault(w => w.Rarity == rarity);
                                            if (entry is not null)
                                            {
                                                entry.Weight = EditorGUILayout.FloatField(entry.Weight, GUILayout.Width(70));
                                            }
                                        }
                                        EditorGUI.indentLevel++;
                                        GUILayout.FlexibleSpace();
                                        EditorGUILayout.EndHorizontal();
                                    }

                                    EditorGUILayout.EndVertical();
                                    EditorGUI.indentLevel--;
                                }
                            }

                            EditorGUILayout.Space();

                            if (item.CanHaveGodPacks || item.CanHaveGodPacksSuppliedByApi)
                            {
                                item.GodWeightsSuppliedByApi = DrawToggle(new GUIContent("God Weights Supplied by API", "God pack weights are supplied by a companion API mod via IItemService.SetGodGenerationWeight."), item.GodWeightsSuppliedByApi);
                                if (!item.GodWeightsSuppliedByApi)
                                {
                                    SyncRarityWeightList(item.GodWeights, expansionRarities);
                                    EditorGUILayout.LabelField(new GUIContent("God Pack Weights", "Rarity distribution used when a God Pack is triggered. All cards in a God Pack are foil."), EditorStyles.boldLabel);
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.BeginVertical("box");
                                    DrawRarityWeightTable(item.GodWeights, expansionRarities);
                                    EditorGUILayout.EndVertical();
                                    EditorGUI.indentLevel--;
                                }
                            }
                        }
                    }

                    EditorGUILayout.Space();
                }
                else
                {
                    if (!item.IsBulkBox) { item.CardExpansion = ""; }
                    item.CanHaveDuplicates = false; item.CanHaveGodPacks = false; item.PackGenerationStrategy = PackGenerationStrategy.Guaranteed; item.PackType = "";
                    item.NormalWeightsSuppliedByApi = false; item.GodWeightsSuppliedByApi = false; item.SlotWeightsSuppliedByApi = false; item.CanHaveGodPacksSuppliedByApi = false; item.GodPackPercentageSuppliedByApi = false;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUI.indentLevel--;
        }

        private string DrawTextField(GUIContent label, string value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.TextField(fieldRect, value);
        }

        private bool DrawToggle(GUIContent label, bool value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.Toggle(fieldRect, value);
        }

        private Enum DrawEnumPopup(GUIContent label, Enum value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.EnumPopup(fieldRect, value);
        }

        private int DrawPopup(GUIContent label, int index, string[] options)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.Popup(fieldRect, index, options);
        }

        private float DrawFloatField(GUIContent label, float value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.FloatField(fieldRect, value);
        }

        private int DrawIntField(GUIContent label, int value)
        {
            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            Rect fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.IntField(fieldRect, value);
        }
    }

    private class PrefabEntryDrawer
    {
        private Dictionary<string, bool> expandedPrefabs = new Dictionary<string, bool>();

        public void Draw(List<PrefabEntry> prefabs)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < prefabs.Count; i++)
            {
                string prefabKey = $"prefab_{i}";
                if (!expandedPrefabs.ContainsKey(prefabKey))
                {
                    expandedPrefabs[prefabKey] = false;
                }

                EditorGUILayout.BeginHorizontal();
                expandedPrefabs[prefabKey] = EditorGUILayout.Foldout(expandedPrefabs[prefabKey], prefabs[i].Name, true);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    prefabs.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (!expandedPrefabs[prefabKey])
                {
                    continue;
                }

                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;

                var prefab = prefabs[i];

                prefab.Name = EditorGUILayout.TextField("Name", prefab.Name);
                prefab.PrefabName = EditorGUILayout.TextField(new GUIContent("Prefab Name", "Name of the Prefab Object. Will not be displayed in game"), prefab.PrefabName);

                prefab.SpriteName = EditorGUILayout.TextField(new GUIContent("Sprite", "The name of the sprite that will appear in the store, box, and price tags. Sprite names do need to be unique across the bundle."), prefab.SpriteName);
                prefab.Price = EditorGUILayout.FloatField(new GUIContent("Price", "The price it will cost to buy this item in game"), prefab.Price);
                prefab.LevelRequirementSuppliedByApi = EditorGUILayout.Toggle(new GUIContent("Level Req. Supplied by API", "The level requirement is supplied by a companion API mod via IDecoService/IFurnitureService.SetLevelRequirement."), prefab.LevelRequirementSuppliedByApi);
                if (!prefab.LevelRequirementSuppliedByApi)
                {
                    prefab.LevelRequirement = EditorGUILayout.IntField(new GUIContent("Level Requirement", "Level it requires to buy this item in game"), prefab.LevelRequirement);
                }

                EditorGUILayout.Space();

                bool isObjectGroup = prefab.IsDecoObject || prefab.AddItemToFurnitureShop;
                bool isTextureGroup = prefab.IsDecoWallTexture || prefab.IsDecoFloorTexture || prefab.IsDecoCeilingTexture;

                // Show Object toggles only if Texture group is not active
                if (!isTextureGroup)
                {
                    prefab.IsDecoObject = EditorGUILayout.Toggle(new GUIContent("Is Deco Object", "Is this a deco statue or sign?"), prefab.IsDecoObject);
                    prefab.AddItemToFurnitureShop = EditorGUILayout.Toggle(new GUIContent("Is Furniture", "Does this item belong in the DIY Shop?"), prefab.AddItemToFurnitureShop);
                }

                // Show Texture toggles only if Object group is not active
                if (!isObjectGroup)
                {
                    prefab.IsDecoWallTexture = EditorGUILayout.Toggle(new GUIContent("Is Wall Texture", "Is this going in the Deco store as a wall texture?"), prefab.IsDecoWallTexture);
                    prefab.IsDecoFloorTexture = EditorGUILayout.Toggle(new GUIContent("Is Floor Texture", "Is this going in the Deco store as a floor texture?"), prefab.IsDecoFloorTexture);
                    prefab.IsDecoCeilingTexture = EditorGUILayout.Toggle(new GUIContent("Is Ceiling Texture", "Is this going in the Deco store as a ceiling texture?"), prefab.IsDecoCeilingTexture);
                }

                // Recalculate bools after drawing toggles to ensure we use the latest state
                isObjectGroup = prefab.IsDecoObject || prefab.AddItemToFurnitureShop;
                isTextureGroup = prefab.IsDecoWallTexture || prefab.IsDecoFloorTexture || prefab.IsDecoCeilingTexture;

                EditorGUILayout.Space();

                // Object group takes priority
                if (isObjectGroup)
                {
                    prefab.DecoBonus = EditorGUILayout.FloatField("Deco Bonus", prefab.DecoBonus);
                    if (prefab.IsDecoObject)
                    {
                        EditorGUILayout.LabelField("Deco Object Configuration", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical("box");

                        prefab.DecoObject = EditorGUILayout.TextField(new GUIContent("Deco Object", "HAS TO BE GLOBALLY UNIQUE. Does not appear in game ui."), prefab.DecoObject);
                        if (prefab.DecoTypeEnumType is null)
                        {
                            prefab.DecoTypeEnumType = Type.GetType("EDecoType, Assembly-CSharp");
                        }

                        if (prefab.DecoTypeEnumType is not null && prefab.DecoTypeEnumType.IsEnum)
                        {
                            Enum currentDecoType = null;
                            try { currentDecoType = (Enum)Enum.Parse(prefab.DecoTypeEnumType, prefab.DecoType ?? ""); } catch { }

                            if (currentDecoType is null)
                            {
                                currentDecoType = (Enum)Enum.GetValues(prefab.DecoTypeEnumType).GetValue(0);
                            }

                            Enum newDecoType = EditorGUILayout.EnumPopup("Deco Type", currentDecoType);
                            prefab.DecoType = newDecoType.ToString();
                        }
                        else
                        {
                            prefab.DecoType = EditorGUILayout.TextField("Deco Type", prefab.DecoType);
                            EditorGUILayout.HelpBox("Could not find EDecoType enum", MessageType.Warning);
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }

                    if (prefab.AddItemToFurnitureShop)
                    {
                        EditorGUILayout.LabelField("Furniture Configuration", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical("box");

                        prefab.Description = EditorGUILayout.TextField("Description", prefab.Description);
                        prefab.ObjectType = EditorGUILayout.TextField(new GUIContent("Object Type", "HAS TO BE GLOBALLY UNIQUE, Does not appear in game UI ever."), prefab.ObjectType);

                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Video Clips", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    for (int j = 0; j < prefab.VideoClips.Count; j++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.BeginVertical();
                        prefab.VideoClips[j].Name = EditorGUILayout.TextField("Name", prefab.VideoClips[j].Name);
                        prefab.VideoClips[j].VideoPlayerName = EditorGUILayout.TextField("Player", prefab.VideoClips[j].VideoPlayerName);
                        EditorGUILayout.EndVertical();
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                        {
                            prefab.VideoClips.RemoveAt(j);
                            j--;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("Add Video Clip"))
                    {
                        prefab.VideoClips.Add(new VideoClipEntry());
                    }
                    EditorGUI.indentLevel--;

                    // Reset texture data
                    prefab.Texture = "";
                    prefab.RoughnessMap = "";
                    prefab.NormalMap = "";
                    prefab.Smoothness = 0.5f;
                    prefab.Color = Color.white;
                }
                else if (isTextureGroup)
                {
                    EditorGUILayout.LabelField("Texture Maps Configuration", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.Space();
                    prefab.Texture = EditorGUILayout.TextField(new GUIContent("Texture", "Main texture for a wall, ceiling, or floor deco texture"), prefab.Texture);
                    prefab.RoughnessMap = EditorGUILayout.TextField(new GUIContent("Roughness Map", "Roughness texture for a wall, ceiling, or floor deco texture"), prefab.RoughnessMap);
                    prefab.NormalMap = EditorGUILayout.TextField(new GUIContent("Normal Map", "Normal map for a wall, ceiling, or floor deco texture"), prefab.NormalMap);
                    prefab.Smoothness = EditorGUILayout.FloatField(new GUIContent("Smoothness", "The smoothing of the textures."), prefab.Smoothness);
                    prefab.Color = EditorGUILayout.ColorField("Color", prefab.Color);

                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;

                    // Reset object data
                    prefab.DecoObject = "";
                    prefab.DecoType = "";
                    prefab.Description = "";
                    prefab.ObjectType = "";
                    prefab.VideoClips = new List<VideoClipEntry>();
                }
                else
                {
                    // Nothing is selected, reset everything
                    prefab.DecoObject = "";
                    prefab.DecoType = "";
                    prefab.Description = "";
                    prefab.ObjectType = "";
                    prefab.VideoClips = new List<VideoClipEntry>();
                    prefab.Texture = "";
                    prefab.RoughnessMap = "";
                    prefab.NormalMap = "";
                    prefab.Smoothness = 0.5f;
                    prefab.Color = Color.white;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUI.indentLevel--;
        }
    }

    void DisplayCardSets(List<CardExpansionEntry> cardExpansions)
    {
        const float C_LABEL_WIDTH = 170f;

        EditorGUI.indentLevel++;

        for (int i = 0; i < cardExpansions.Count; i++)
        {
            string cardSetKey = $"cardset_{i}";
            if (!expandedCards.ContainsKey(cardSetKey))
            {
                expandedCards[cardSetKey] = false;
            }

            EditorGUILayout.BeginHorizontal();
            expandedCards[cardSetKey] = EditorGUILayout.Foldout(expandedCards[cardSetKey], cardExpansions[i].Name, true);
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                cardExpansions.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                continue;
            }
            EditorGUILayout.EndHorizontal();

            if (!expandedCards[cardSetKey])
            {
                continue;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            var cardExpansion = cardExpansions[i];
            
            Rect lineRect;
            Rect labelRect;
            Rect fieldRect;

            // Card Expansion Name
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Card Expansion Name", "Name of the expansion. This is what is used to display in game"));
            cardExpansion.Name = EditorGUI.TextField(fieldRect, cardExpansion.Name);

            // Expansion Type
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Expansion Type", "HAS TO BE GLOBALLY UNIQUE. Should not appear in game UI"));
            cardExpansion.CardExpansion = EditorGUI.TextField(fieldRect, cardExpansion.CardExpansion);

            // Menu Category
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Menu Category", "This expansion will be nested under this category in the selection screens."));
            cardExpansion.MenuCategory = EditorGUI.TextField(fieldRect, cardExpansion.MenuCategory);

            // Trade Unlock Level
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Trade Unlock Level", "Player level at which trades for this expansion become unlocked. Set to 0 to unlock immediately."));
            cardExpansion.TradeUnlockLevel = Mathf.Max(0, EditorGUI.IntField(fieldRect, cardExpansion.TradeUnlockLevel));

            // Has Foils
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Has Foils", "If true, ALL cards in the set will have a foil and non-foil versions."));
            cardExpansion.HasRandomFoils = EditorGUI.Toggle(fieldRect, cardExpansion.HasRandomFoils);

            if (cardExpansion.HasRandomFoils)
            {
                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Foil Chance", "Chance (0–1) that any given card drawn from this expansion will be foil. Leave at 0 to use the default of 0.025 (1-in-40)."));
                cardExpansion.FoilChance = EditorGUI.FloatField(fieldRect, cardExpansion.FoilChance);
            }
            else
            {
                cardExpansion.FoilChance = 0f;
            }

            // Card Price Strategy
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Prices Supplied by API", "Card price generation is supplied by a companion API mod via ICardExpansionService.SetCardPriceGenerator."));
            cardExpansion.CardPriceStrategySuppliedByApi = EditorGUI.Toggle(fieldRect, cardExpansion.CardPriceStrategySuppliedByApi);

            if (cardExpansion.CardPriceStrategySuppliedByApi)
            {
                EditorGUILayout.HelpBox("Card prices are supplied by the API. The companion mod must assign a generator via ICardExpansionService.SetCardPriceGenerator once bundle loading completes; the '" + cardExpansion.CardPriceStrategy + "' strategy is used as a fallback until it does.", MessageType.Info);
            }
            else
            {
                var strategies = new[] { "Default", "RarityDriven" };
                int currentStrategyIndex = Array.IndexOf(strategies, cardExpansion.CardPriceStrategy);
                if (currentStrategyIndex < 0)
                {
                    currentStrategyIndex = 0;
                }
                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Card Price Strategy", "How card market prices are calculated for this expansion. 'Default' uses border type as the primary driver with rarity as a modifier. 'RarityDriven' uses rarity as the base with border and foil as light multipliers."));
                currentStrategyIndex = EditorGUI.Popup(fieldRect, currentStrategyIndex, strategies);
                cardExpansion.CardPriceStrategy = strategies[currentStrategyIndex];
            }

            // Price Multipliers (conditional on strategy)
            if (!cardExpansion.CardPriceStrategySuppliedByApi && cardExpansion.CardPriceStrategy == "Default")
            {
                EditorGUI.indentLevel++;

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Border Multiplier Base", "Price is multiplied by this value raised to the power of the border type index. Higher values create bigger price gaps between border types."));
                cardExpansion.DefaultBorderMultiplierBase = EditorGUI.FloatField(fieldRect, cardExpansion.DefaultBorderMultiplierBase);

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Foil Multiplier", "Foil cards have their price multiplied by this value."));
                cardExpansion.DefaultFoilMultiplier = EditorGUI.FloatField(fieldRect, cardExpansion.DefaultFoilMultiplier);

                EditorGUI.indentLevel--;
            }
            else if (!cardExpansion.CardPriceStrategySuppliedByApi && cardExpansion.CardPriceStrategy == "RarityDriven")
            {
                EditorGUI.indentLevel++;

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Floor Price", "The minimum base price before any rarity stepping is applied."));
                cardExpansion.RarityDrivenFloor = EditorGUI.FloatField(fieldRect, cardExpansion.RarityDrivenFloor);

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Step Size", "Price increase per rarity tier. Each rarity index adds this amount to the base price."));
                cardExpansion.RarityDrivenStepSize = EditorGUI.FloatField(fieldRect, cardExpansion.RarityDrivenStepSize);

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Border Multiplier Base", "Price is multiplied by this value raised to the power of the border type index."));
                cardExpansion.RarityDrivenBorderMultiplierBase = EditorGUI.FloatField(fieldRect, cardExpansion.RarityDrivenBorderMultiplierBase);

                lineRect = EditorGUILayout.GetControlRect();
                labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                EditorGUI.LabelField(labelRect, new GUIContent("Foil Multiplier", "Foil cards have their price multiplied by this value."));
                cardExpansion.RarityDrivenFoilMultiplier = EditorGUI.FloatField(fieldRect, cardExpansion.RarityDrivenFoilMultiplier);

                EditorGUI.indentLevel--;
            }

            // Card Back Sprite
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Card Back Texture", "Texture name for the texture to be used for the card back. Texture names need to be unique across the bundle"));
            cardExpansion.CardbackSprite = EditorGUI.TextField(fieldRect, cardExpansion.CardbackSprite);
            
            // Foil Mask
            lineRect = EditorGUILayout.GetControlRect();
            labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
            fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent("Foil Mask", "Foil mask to use for the card set. This determines where the foil shows on the card"));
            cardExpansion.FoilMask = EditorGUI.TextField(fieldRect, cardExpansion.FoilMask);

            // Play Card Materials
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Play Card Materials", "Material asset names used as card texture sheets on play tables. Customers may use these when playing at tables."), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            for (int j = 0; j < cardExpansion.PlayCardMaterials.Count; j++)
            {
                Rect matLineRect = EditorGUILayout.GetControlRect();
                Rect matLabelRect = new Rect(matLineRect.x, matLineRect.y, 80, matLineRect.height);
                Rect matFieldRect = new Rect(matLineRect.x + 80, matLineRect.y, matLineRect.width - 80 - 85, matLineRect.height);
                Rect matRemoveRect = new Rect(matFieldRect.xMax + 5, matLineRect.y, 80, matLineRect.height);

                EditorGUI.LabelField(matLabelRect, new GUIContent($"Material {j + 1}"));
                cardExpansion.PlayCardMaterials[j] = EditorGUI.TextField(matFieldRect, cardExpansion.PlayCardMaterials[j]);
                if (GUI.Button(matRemoveRect, "Remove"))
                {
                    cardExpansion.PlayCardMaterials.RemoveAt(j);
                    j--;
                }
            }
            if (GUILayout.Button("Add Material"))
            {
                cardExpansion.PlayCardMaterials.Add("");
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Rarities", "Rarities in the set"), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            for (int j = 0; j < cardExpansion.Rarities.Count; j++)
            {
                Rect rarityLineRect = EditorGUILayout.GetControlRect();
                Rect rarityLabelRect = new Rect(rarityLineRect.x, rarityLineRect.y, 80, rarityLineRect.height);
                Rect rarityFieldRect = new Rect(rarityLineRect.x + 80, rarityLineRect.y, rarityLineRect.width - 80 - 85, rarityLineRect.height);
                Rect removeButtonRect = new Rect(rarityFieldRect.xMax + 5, rarityLineRect.y, 80, rarityLineRect.height);

                EditorGUI.LabelField(rarityLabelRect, new GUIContent($"Rarity {j + 1}"));
                cardExpansion.Rarities[j] = EditorGUI.TextField(rarityFieldRect, cardExpansion.Rarities[j]);
                if (GUI.Button(removeButtonRect, "Remove"))
                {
                    cardExpansion.Rarities.RemoveAt(j);
                    j--;
                }
            }
            if (GUILayout.Button("Add Rarity"))
            {
                cardExpansion.Rarities.Add("");
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            // Cards Section Foldout
            if (!showCardsInSection.ContainsKey(cardSetKey))
            {
                showCardsInSection[cardSetKey] = true;
            }

            showCardsInSection[cardSetKey] = EditorGUILayout.Foldout(showCardsInSection[cardSetKey], $"Cards ({cardExpansion.Cards.Count})", true);

            if (showCardsInSection[cardSetKey])
            {
                EditorGUI.indentLevel++;

                for (int j = 0; j < cardExpansion.Cards.Count; j++)
                {
                    string cardKey = $"card_{i}_{j}";
                    if (!expandedCards.ContainsKey(cardKey))
                    {
                        expandedCards[cardKey] = false;
                    }

                    EditorGUILayout.BeginHorizontal();
                    expandedCards[cardKey] = EditorGUILayout.Foldout(expandedCards[cardKey], cardExpansion.Cards[j].Name, true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        cardExpansion.Cards.RemoveAt(j);
                        EditorGUILayout.EndHorizontal();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (!expandedCards[cardKey])
                    {
                        continue;
                    }

                    EditorGUILayout.BeginVertical("box");
                    EditorGUI.indentLevel++;

                    var card = cardExpansion.Cards[j];

                    // Card Name
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Name", "Name of the card"));
                    card.Name = EditorGUI.TextField(fieldRect, card.Name);

                    // Element Type
                    if (card.ElementTypeEnumType is null)
                    {
                        card.ElementTypeEnumType = Type.GetType("EElementIndex, Assembly-CSharp");
                    }
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Element Type", "Element type of the card. This only effects the day to day price fluctuations"));
                    if (card.ElementTypeEnumType is not null && card.ElementTypeEnumType.IsEnum)
                    {
                        Enum currentElement = null;
                        try { currentElement = (Enum)Enum.Parse(card.ElementTypeEnumType, card.ElementType ?? ""); } catch { }
                        if (currentElement is null) { currentElement = (Enum)Enum.GetValues(card.ElementTypeEnumType).GetValue(0); }
                        card.ElementType = EditorGUI.EnumPopup(fieldRect, currentElement).ToString();
                    }
                    else
                    {
                        card.ElementType = EditorGUI.TextField(fieldRect, card.ElementType);
                        EditorGUILayout.HelpBox("Could not find EElementIndex enum", MessageType.Warning);
                    }

                    // Border Type
                    if (card.BorderTypeEnumType is null)
                    {
                        card.BorderTypeEnumType = Type.GetType("ECardBorderType, Assembly-CSharp");
                    }
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Border Type", "Border type of the card. This is only used in intial price generation where base border will generate with the least price. Full art will generate the highest prices"));
                    if (card.BorderTypeEnumType is not null && card.BorderTypeEnumType.IsEnum)
                    {
                        Enum currentBorder = null;
                        try { currentBorder = (Enum)Enum.Parse(card.BorderTypeEnumType, card.BorderType ?? ""); } catch { }
                        if (currentBorder is null) { currentBorder = (Enum)Enum.GetValues(card.BorderTypeEnumType).GetValue(0); }
                        card.BorderType = EditorGUI.EnumPopup(fieldRect, currentBorder).ToString();
                    }
                    else
                    {
                        card.BorderType = EditorGUI.TextField(fieldRect, card.BorderType);
                        EditorGUILayout.HelpBox("Could not find ECardBorderType enum", MessageType.Warning);
                    }
                    
                    // Rarity
                    if (cardExpansion.Rarities.Count > 0)
                    {
                        int currentRarityIndex = cardExpansion.Rarities.IndexOf(card.Rarity);
                        if (currentRarityIndex < 0) { currentRarityIndex = 0; }
                        
                        lineRect = EditorGUILayout.GetControlRect();
                        labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                        fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                        EditorGUI.LabelField(labelRect, new GUIContent("Rarity", "Rarity of the card"));
                        currentRarityIndex = EditorGUI.Popup(fieldRect, currentRarityIndex, cardExpansion.Rarities.ToArray());
                        card.Rarity = cardExpansion.Rarities[currentRarityIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Add rarities to the Card Expansion to view the dropdown", MessageType.Info);
                    }

                    // Sprite
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Sprite", "Sprite that is to be used as the card image. Sprite names need to be unique across the bundle"));
                    card.Sprite = EditorGUI.TextField(fieldRect, card.Sprite);

                    // Is Guaranteed Foil
                    if (!cardExpansion.HasRandomFoils)
                    {
                        lineRect = EditorGUILayout.GetControlRect();
                        labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                        fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                        EditorGUI.LabelField(labelRect, new GUIContent("Is Guaranteed Foil", "Is this card always foil? Can not be true if the set can have random foils"));
                        card.IsFoil = EditorGUI.Toggle(fieldRect, card.IsFoil);
                    }
                    else
                    {
                        card.IsFoil = false;
                    }
                    
                    // Foil Mask
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Foil Mask", "This cards foil mask if it needs a different foil mask. Will fallback to the sets foilmask if not set"));
                    card.FoilMask = EditorGUI.TextField(fieldRect, card.FoilMask);

                    // Card Back
                    lineRect = EditorGUILayout.GetControlRect();
                    labelRect = new Rect(lineRect.x, lineRect.y, C_LABEL_WIDTH, lineRect.height);
                    fieldRect = new Rect(lineRect.x + C_LABEL_WIDTH, lineRect.y, lineRect.width - C_LABEL_WIDTH, lineRect.height);
                    EditorGUI.LabelField(labelRect, new GUIContent("Card Back", "This card will use this cardback. If this is blank it will continue using the expansion cardback"));
                    card.CardBack = EditorGUI.TextField(fieldRect, card.CardBack);
                    
                    card.CardNumber = cardExpansion.Cards.IndexOf(card);

                    DisplayCardPrice(card, cardExpansion);

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }

                if (GUILayout.Button("Add New Card"))
                {
                    cardExpansion.Cards.Add(new CardEntry
                    {
                        Name = "New Card",
                        ElementType = "",
                        Rarity = "",
                        CardNumber = -1,
                        Sprite = "",
                        BorderType = "",
                        CardBack = ""
                    });
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUI.indentLevel--;
    }
    private void DisplayCardPrice(CardEntry card, CardExpansionEntry cardExpansion)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Calculated Pricing", EditorStyles.boldLabel);

        if (cardExpansion.CardPriceStrategySuppliedByApi)
        {
            EditorGUILayout.LabelField("Price preview unavailable — card prices are supplied by the API.");
            return;
        }

        int rarityIndex = Mathf.Max(0, cardExpansion.Rarities.IndexOf(card.Rarity));
        int rarityCount = cardExpansion.Rarities.Count;
        ECardBorderType borderType = ECardBorderType.Base;

        if (!string.IsNullOrEmpty(card.BorderType))
        {
            if (!Enum.TryParse<ECardBorderType>(card.BorderType, out borderType))
            {
                Debug.LogWarning($"Failed to parse border type '{card.BorderType}'. Defaulting to 'Base'.");
                borderType = ECardBorderType.Base;
            }
        }

        EditorGUI.BeginDisabledGroup(true);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        bool isRarityDriven = cardExpansion.CardPriceStrategy == "RarityDriven";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Price Range:", GUILayout.Width(80));

        if (cardExpansion.HasRandomFoils)
        {
            float minPriceNormal = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMin(rarityIndex, borderType, false, cardExpansion) : GenerateCardMarketPriceMin(rarityIndex, rarityCount, borderType, false, cardExpansion);
            float maxPriceNormal = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMax(rarityIndex, borderType, false, cardExpansion) : GenerateCardMarketPriceMax(rarityIndex, rarityCount, borderType, false, cardExpansion);
            EditorGUILayout.LabelField("Normal:", GUILayout.Width(50));
            EditorGUILayout.SelectableLabel(minPriceNormal.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("-", GUILayout.Width(10));
            EditorGUILayout.SelectableLabel(maxPriceNormal.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            GUILayout.Space(10);

            float minPriceFoil = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMin(rarityIndex, borderType, true, cardExpansion) : GenerateCardMarketPriceMin(rarityIndex, rarityCount, borderType, true, cardExpansion);
            float maxPriceFoil = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMax(rarityIndex, borderType, true, cardExpansion) : GenerateCardMarketPriceMax(rarityIndex, rarityCount, borderType, true, cardExpansion);
            EditorGUILayout.LabelField("Foil:", GUILayout.Width(30));
            EditorGUILayout.SelectableLabel(minPriceFoil.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("-", GUILayout.Width(10));
            EditorGUILayout.SelectableLabel(maxPriceFoil.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
        else
        {
            float minPrice = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMin(rarityIndex, borderType, card.IsFoil, cardExpansion) : GenerateCardMarketPriceMin(rarityIndex, rarityCount, borderType, card.IsFoil, cardExpansion);
            float maxPrice = isRarityDriven ? GenerateCardMarketPriceRarityDrivenMax(rarityIndex, borderType, card.IsFoil, cardExpansion) : GenerateCardMarketPriceMax(rarityIndex, rarityCount, borderType, card.IsFoil, cardExpansion);
            EditorGUILayout.LabelField(card.IsFoil ? "Foil:" : "Normal:", GUILayout.Width(50));
            EditorGUILayout.SelectableLabel(minPrice.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("-", GUILayout.Width(10));
            EditorGUILayout.SelectableLabel(maxPrice.ToString("F2"), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel = indent;

        EditorGUI.EndDisabledGroup();
    }

    private float GenerateCardMarketPriceMin(int rarityIndex, int rarityCount, ECardBorderType borderType, bool isFoil, CardExpansionEntry expansion)
    {
        var lowEndMin = Mathf.Lerp(0.2f, 0.85f, (int)borderType / 5f);
        return GenerateCardMarketPrice(rarityIndex, rarityCount, borderType, isFoil, lowEndMin, expansion);
    }

    private float GenerateCardMarketPriceMax(int rarityIndex, int rarityCount, ECardBorderType borderType, bool isFoil, CardExpansionEntry expansion)
    {
        return GenerateCardMarketPrice(rarityIndex, rarityCount, borderType, isFoil, 1.15f, expansion);
    }

    private float GenerateCardMarketPrice(int rarityIndex, int rarityCount, ECardBorderType borderType, bool isFoil, float randMultiplier, CardExpansionEntry expansion)
    {
        var rarityPercent = rarityCount > 1 ? rarityIndex / (float)(rarityCount - 1) : 0.5f;
        var borderMultiplier = Mathf.Pow(expansion.DefaultBorderMultiplierBase, (int)borderType);
        var rarityMultiplier = 1f + (Mathf.Pow(rarityPercent, 1.6f) * 4.0f);
        var baseMultiplier = 1.0f + (rarityPercent * 1.0f);
        var foilMultiplier = isFoil ? expansion.DefaultFoilMultiplier : 1f;
        var basePrice = baseMultiplier * borderMultiplier * rarityMultiplier * foilMultiplier * randMultiplier;
        return basePrice;
    }

    private float GenerateCardMarketPriceRarityDrivenMin(int rarityIndex, ECardBorderType borderType, bool isFoil, CardExpansionEntry expansion)
    {
        var rarityBase = expansion.RarityDrivenFloor + (rarityIndex * expansion.RarityDrivenStepSize);
        var borderMultiplier = Mathf.Pow(expansion.RarityDrivenBorderMultiplierBase, (int)borderType);
        var foilMultiplier = isFoil ? expansion.RarityDrivenFoilMultiplier : 1f;
        var lowEnd = Mathf.Lerp(0.05f, 0.7f, rarityIndex / 20f);
        return rarityBase * borderMultiplier * foilMultiplier * lowEnd;
    }

    private float GenerateCardMarketPriceRarityDrivenMax(int rarityIndex, ECardBorderType borderType, bool isFoil, CardExpansionEntry expansion)
    {
        var rarityBase = expansion.RarityDrivenFloor + (rarityIndex * expansion.RarityDrivenStepSize);
        var borderMultiplier = Mathf.Pow(expansion.RarityDrivenBorderMultiplierBase, (int)borderType);
        var foilMultiplier = isFoil ? expansion.RarityDrivenFoilMultiplier : 1f;
        return rarityBase * borderMultiplier * foilMultiplier * 1.3f;
    }

    public static int GetValueFromBulkBoxType(BulkBoxType boxType)
    {
        switch (boxType)
        {
            case BulkBoxType.WhiteBox: return 0;
            case BulkBoxType.YellowBox: return 100;
            case BulkBoxType.PinkBox: return 200;
            case BulkBoxType.RedBox: return 400;
            case BulkBoxType.GreenBox: return 1200;
            default: return 0;
        }
    }

    public static BulkBoxType GetBulkBoxTypeFromValue(int value)
    {
        if (value == 100)
        {
            return BulkBoxType.YellowBox;
        }
        if (value == 200)
        {
            return BulkBoxType.PinkBox;
        }
        if (value == 400)
        {
            return BulkBoxType.RedBox;
        }
        if (value == 1200)
        {
            return BulkBoxType.GreenBox;
        }
        return BulkBoxType.WhiteBox;
    }

    static void ConfigureTexturesForBundle(string bundleName)
    {
        var bundle = bundleData[bundleName];
        if (!bundle.AutoCompressTextures)
        {
            return;
        }

        HashSet<string> cardSpriteNames = new HashSet<string>();
        foreach (var expansion in bundle.CardExpansions)
        {
            if (!string.IsNullOrEmpty(expansion.CardbackSprite))
            {
                cardSpriteNames.Add(expansion.CardbackSprite);
            }

            foreach (var card in expansion.Cards)
            {
                if (!string.IsNullOrEmpty(card.Sprite))
                {
                    cardSpriteNames.Add(card.Sprite);
                }
                if (!string.IsNullOrEmpty(card.CardBack))
                {
                    cardSpriteNames.Add(card.CardBack);
                }
            }
        }

        var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        
        foreach(var assetPath in assetPaths)
        {
            if(!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && 
               !assetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
               !assetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
               !assetPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if(!importer)
            {
                continue;
            }
            
            var platformSettings = importer.GetPlatformTextureSettings("Standalone");

            if (importer.textureType == TextureImporterType.NormalMap)
            {
                bool normalChanged = false;
                if (importer.maxTextureSize != bundle.MaxTextureSize)
                {
                    importer.maxTextureSize = bundle.MaxTextureSize;
                    normalChanged = true;
                }

                if (importer.isReadable)
                {
                    importer.isReadable = false;
                    normalChanged = true;
                }

                if (!platformSettings.overridden ||
                    platformSettings.format != TextureImporterFormat.DXT5 ||
                    platformSettings.maxTextureSize != bundle.MaxTextureSize)
                {
                    platformSettings.overridden = true;
                    platformSettings.format = TextureImporterFormat.DXT5;
                    platformSettings.maxTextureSize = bundle.MaxTextureSize;
                    platformSettings.textureCompression = TextureImporterCompression.Compressed;
                    importer.SetPlatformTextureSettings(platformSettings);
                    normalChanged = true;
                }

                if (normalChanged)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"Configured normal map texture for: {assetPath}");
                }
                continue;
            }
            
            bool changed = false;
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isCardAsset = cardSpriteNames.Contains(fileName);

            // 2. MipMap Logic — cards get Kaiser mipmaps when the toggle is on; otherwise card/sprite textures have mipmaps stripped.
            // Non-card Default-type textures (3D mesh textures) are left alone — mipmaps are desirable there.
            if (bundle.EnableMipMaps && isCardAsset)
            {
                if (!importer.mipmapEnabled || importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter)
                {
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    changed = true;
                    Debug.Log($"Applied Kaiser MipMaps to Card Asset: {fileName}");
                }
            }
            else if ((isCardAsset || importer.textureType == TextureImporterType.Sprite) && importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
                Debug.Log($"Stripped MipMaps from: {fileName}");
            }

            if(importer.maxTextureSize != bundle.MaxTextureSize)
            {
                    importer.maxTextureSize = bundle.MaxTextureSize;
                    changed = true;
            }

            if  (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    changed = true;
             }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            TextureImporterFormat newFormat;
            switch (bundle.TextureCompression)
            {
                case "BC7":
                    newFormat = TextureImporterFormat.BC7;
                    break;
                default:
                    // DXT5's extra alpha block is wasted on opaque textures — DXT1 is half the VRAM.
                    bool hasAlpha = importer.alphaSource != TextureImporterAlphaSource.None && importer.DoesSourceTextureHaveAlpha();
                    newFormat = hasAlpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
                    break;
            }

            if(!platformSettings.overridden || 
               platformSettings.format != newFormat ||
               platformSettings.maxTextureSize != bundle.MaxTextureSize)
            {
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = bundle.MaxTextureSize;
                platformSettings.format = newFormat;
                platformSettings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(platformSettings);
                changed = true;
            }
            
            if(changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"Configured texture compression for: {assetPath}");
            }
        }
    }

    static void BuildAllAssetBundles()
    {
        string assetBundleRoot = "AssetBundles";

        if(!Directory.Exists(assetBundleRoot))
        {
            Directory.CreateDirectory(assetBundleRoot);
        }

        var buildOptions = BuildAssetBundleOptions.ChunkBasedCompression;
        var buildTarget = EditorUserBuildSettings.activeBuildTarget;

        var window = GetWindow<BundleVerificationWindow>();
        foreach(var bundleName in bundleNames)
        {
            if(window.buildToggles.ContainsKey(bundleName) && window.buildToggles[bundleName])
            {
                BuildSingleBundle(bundleName, assetBundleRoot, buildOptions, buildTarget);
            }
        }

        AssetDatabase.Refresh();
    }

    static void SaveBundleMetadata(string bundleName, string rootPath)
    {
        var bundleInfo = bundleData[bundleName];

        AssetBundleCatalog catalog = new AssetBundleCatalog
        {
            Name = bundleInfo.BundleName,
            BundleId = bundleInfo.BundleId,
            AutoCompressTextures = bundleInfo.AutoCompressTextures,
            MaxTextureSize = bundleInfo.MaxTextureSize,
            TextureCompression = bundleInfo.TextureCompression,
            EnableMipMaps = bundleInfo.EnableMipMaps,
            ItemPricesSuppliedByApi = bundleInfo.ItemPricesSuppliedByApi,
            Assemblies = bundleInfo.Assemblies,
            Items = bundleInfo.Items,
            Prefabs = bundleInfo.Prefabs,
            CardExpansions = bundleInfo.CardExpansions,
            CustomShops = bundleInfo.CustomShops,
        };

        string bundleOutputPath = Path.Combine(rootPath, Path.GetDirectoryName(bundleName));
        string bundleFileName = Path.GetFileName(bundleName);

        if(!Directory.Exists(bundleOutputPath))
        {
            Directory.CreateDirectory(bundleOutputPath);
        }

        string jsonPath = Path.Combine(bundleOutputPath, $"{bundleFileName}.json");
        string catalogJsonPath = Path.Combine(bundleOutputPath, $"{bundleFileName}.catalog.json");

        // Editor working file — list-based weights, roundtripped via JsonUtility
        File.WriteAllText(catalogJsonPath, JsonUtility.ToJson(catalog, true));
        // Bundle descriptor for the game — dict-format weights required by Core
        File.WriteAllText(jsonPath, BuildBundleDescriptorJson(catalog));

        Debug.Log($"Saved bundle metadata for {bundleFileName} at {jsonPath}");
    }

    static void BuildSingleBundle(string bundleName, string rootPath, BuildAssetBundleOptions options, BuildTarget target)
    {
        var bundleDir = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName)
            .Select(p => Path.GetDirectoryName(p))
            .FirstOrDefault();

        if(string.IsNullOrEmpty(bundleDir))
        {
            return;
        }

        SaveBundleMetadata(bundleName, rootPath);

        string bundleOutputPath = Path.Combine(rootPath, Path.GetDirectoryName(bundleName));
        string bundleFileName = Path.GetFileName(bundleName);

        ConfigureTexturesForBundle(bundleName);

        var assetBundleBuild = new AssetBundleBuild
        {
            assetBundleName = bundleFileName,
            assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName)
        };

        BuildPipeline.BuildAssetBundles(bundleOutputPath,
            new AssetBundleBuild[] { assetBundleBuild },
            options,
            target);

        Debug.Log($"Built bundle {bundleFileName} at {bundleOutputPath}");
    }

    // ── Weight UI helpers ──────────────────────────────────────────────────────

    // Non-destructive: entries whose rarity no longer matches are kept (renames happen one
    // keystroke at a time in the rarity text fields — removing here would discard tuned
    // weights mid-edit). Unmatched entries are hidden by the draw filter and stripped at
    // export time.
    private static void SyncRarityWeightList(List<RarityWeightEntry> weights, List<string> expansionRarities)
    {
        var existing = new HashSet<string>(weights.Select(w => w.Rarity));
        foreach (var rarity in expansionRarities)
        {
            if (!existing.Contains(rarity))
            {
                weights.Add(new RarityWeightEntry { Rarity = rarity, Weight = 0f });
            }
        }
        weights.Sort((a, b) =>
        {
            int indexA = expansionRarities.IndexOf(a.Rarity);
            int indexB = expansionRarities.IndexOf(b.Rarity);
            if (indexA < 0)
            {
                indexA = int.MaxValue;
            }
            if (indexB < 0)
            {
                indexB = int.MaxValue;
            }
            return indexA.CompareTo(indexB);
        });
    }

    private static void DrawRarityWeightTable(List<RarityWeightEntry> weights, List<string> expansionRarities)
    {
        var visibleWeights = weights.Where(w => expansionRarities.Contains(w.Rarity)).ToList();
        if (visibleWeights.Count == 0)
        {
            return;
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(120));
        EditorGUILayout.LabelField("Rarity", EditorStyles.boldLabel);
        foreach (var w in visibleWeights)
        {
            EditorGUILayout.LabelField(w.Rarity);
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUI.indentLevel--;
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Weight", EditorStyles.boldLabel);
        foreach (var w in visibleWeights)
        {
            w.Weight = EditorGUILayout.FloatField(w.Weight, GUILayout.Width(70));
        }
        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel++;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ── Old-format migration ───────────────────────────────────────────────────

    private static void InferApiManagedPacks(List<ItemEntry> items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.IsCardPack && !item.ApiManaged && string.IsNullOrEmpty(item.PackType))
            {
                item.ApiManaged = true;
            }
        }
    }

    private static void MigrateItemsFromOldFormat(List<ItemEntry> items)
    {
        foreach (var item in items)
        {
            if (item.GenerationWeights is null || item.GenerationWeights.Count == 0)
            {
                continue;
            }
            if (item.NormalWeights is null)
            {
                item.NormalWeights = new List<RarityWeightEntry>();
            }
            if (item.GodWeights is null)
            {
                item.GodWeights = new List<RarityWeightEntry>();
            }
            if (item.SlotWeights is null)
            {
                item.SlotWeights = new List<SlotWeightEntry>();
            }
            if (item.NormalWeights.Count == 0)
            {
                item.NormalWeights = item.GenerationWeights
                    .Select(gw => new RarityWeightEntry { Rarity = gw.Rarity, Weight = gw.NormalWeight })
                    .ToList();
            }
            if (item.GodWeights.Count == 0)
            {
                item.GodWeights = item.GenerationWeights
                    .Select(gw => new RarityWeightEntry { Rarity = gw.Rarity, Weight = gw.GodPackWeight })
                    .ToList();
            }
            if (item.SlotWeights.Count == 0)
            {
                var allSlots = new[] { Slots.Slot1, Slots.Slot2, Slots.Slot3, Slots.Slot4, Slots.Slot5, Slots.Slot6, Slots.Slot7 };
                foreach (var slot in allSlots)
                {
                    var slotEntry = new SlotWeightEntry { Slot = slot, Weights = new List<RarityWeightEntry>() };
                    foreach (var gw in item.GenerationWeights)
                    {
                        if ((int)gw.GuaranteedSlots <= 0)
                        {
                            continue;
                        }
                        if ((gw.GuaranteedSlots & slot) != 0)
                        {
                            slotEntry.Weights.Add(new RarityWeightEntry { Rarity = gw.Rarity, Weight = gw.NormalWeight });
                        }
                    }
                    item.SlotWeights.Add(slotEntry);
                }
            }
        }
    }

    // ── Bundle descriptor JSON builder ────────────────────────────────────────
    // Produces Core-compatible JSON with dict-format weight fields.
    // JsonUtility cannot serialize Dictionary<K,V>, so this custom builder is used
    // instead of JsonUtility.ToJson for the game-facing bundle descriptor.

    private static string BuildBundleDescriptorJson(AssetBundleCatalog catalog)
    {
        var temp = new AssetBundleCatalog
        {
            Name = catalog.Name,
            BundleId = catalog.BundleId,
            AutoCompressTextures = catalog.AutoCompressTextures,
            MaxTextureSize = catalog.MaxTextureSize,
            TextureCompression = catalog.TextureCompression,
            EnableMipMaps = catalog.EnableMipMaps,
            ItemPricesSuppliedByApi = catalog.ItemPricesSuppliedByApi,
            Assemblies = catalog.Assemblies,
            Items = new List<ItemEntry>(),
            Prefabs = catalog.Prefabs,
            CardExpansions = catalog.CardExpansions,
            CustomShops = catalog.CustomShops,
        };
        string baseJson = JsonUtility.ToJson(temp, true);
        string itemsJson = BuildItemsDescriptorJson(catalog.Items, catalog.ItemPricesSuppliedByApi);
        // Locate the empty Items array with the same string-state/bracket-depth scanner the
        // loader uses — a formatting change in JsonUtility's output can't silently miss it.
        var (start, length) = FindItemsArraySpan(baseJson);
        return baseJson.Remove(start, length).Insert(start, itemsJson);
    }

    private static string BuildItemsDescriptorJson(List<ItemEntry> items, bool bundleItemPricesApi)
    {
        if (items is null || items.Count == 0)
        {
            return "[]";
        }
        var sb = new StringBuilder("[");
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(",");
            }
            sb.Append(BuildItemDescriptorJson(items[i], bundleItemPricesApi));
        }
        sb.Append("]");
        return sb.ToString();
    }

    private static string BuildItemDescriptorJson(ItemEntry item, bool bundleItemPricesApi)
    {
        // Hidden weight tables are preserved in working data so strategy toggles and rarity
        // renames never discard tuned values — only the tables the item's current
        // configuration actually uses (filtered to the linked expansion's rarities) export.
        var expansionRarities = FindExpansionRarities(item.CardExpansion);
        List<RarityWeightEntry> FilterToExpansion(List<RarityWeightEntry> weights) =>
            expansionRarities is null || weights is null ? weights : weights.Where(w => expansionRarities.Contains(w.Rarity)).ToList();

        var exportNormalWeights = item.IsCardPack && !item.ApiManaged && !item.NormalWeightsSuppliedByApi && item.PackGenerationStrategy == PackGenerationStrategy.AllRandomWeighted
            ? FilterToExpansion(item.NormalWeights) : null;
        var exportGodWeights = item.IsCardPack && !item.ApiManaged && !item.GodWeightsSuppliedByApi && item.CanHaveGodPacks
            ? FilterToExpansion(item.GodWeights) : null;
        var exportSlotWeights = item.IsCardPack && !item.ApiManaged && !item.SlotWeightsSuppliedByApi && item.PackGenerationStrategy == PackGenerationStrategy.Guaranteed
            ? item.SlotWeights?.Select(sw => new SlotWeightEntry { Slot = sw.Slot, Weights = FilterToExpansion(sw.Weights) ?? new List<RarityWeightEntry>() }).ToList() : null;

        var sb = new StringBuilder("{");
        JStr(sb, "Name", item.Name);
        JStr(sb, "ItemCategory", item.ItemCategory);
        JStr(sb, "SpriteName", item.SpriteName);
        JFlt(sb, "BaseCost", item.BaseCost);
        JBool(sb, "AddItemAsAccessory", item.AddItemAsAccessory);
        JBool(sb, "AddItemAsFigurine", item.AddItemAsFigurine);
        JBool(sb, "AddItemAsBoardGame", item.AddItemAsBoardGame);
        JBool(sb, "IsSprayCan", item.IsSprayCan);
        JStr(sb, "PhoneAppId", item.PhoneAppId);
        JStr(sb, "ItemType", item.ItemType);
        JBool(sb, "UsesBaseGameMesh", item.UsesBaseGameMesh);
        JStr(sb, "MeshToUse", item.MeshToUse);
        JStr(sb, "Mesh", item.Mesh);
        JStr(sb, "MeshSecondary", item.MeshSecondary);
        JStr(sb, "Material", item.Material);
        JStr(sb, "MaterialSecondary", item.MaterialSecondary);
        JStrList(sb, "MaterialList", item.MaterialList);
        JBool(sb, "IsBigBox", item.HasBigBox);
        JBool(sb, "HideTillUnlocked", item.BigBoxHideTillUnlocked);
        JFlt(sb, "LicensePrice", item.BigBoxLicensePrice);
        JInt(sb, "LicenseLevelRequirement", item.BigBoxLicenseLevelRequirement);
        JBool(sb, "IgnoreDoubleImage", item.BigBoxIgnoreDoubleImage);
        JBool(sb, "HasSmallBox", item.HasSmallBox);
        JBool(sb, "SmallBoxHideTillUnlocked", item.SmallBoxHideTillUnlocked);
        JFlt(sb, "SmallBoxLicensePrice", item.SmallBoxLicensePrice);
        JInt(sb, "SmallBoxLicenseLevelRequirement", item.SmallBoxLicenseLevelRequirement);
        JBool(sb, "SmallBoxIgnoreDoubleImage", item.SmallBoxIgnoreDoubleImage);
        JBool(sb, "IsBulkBox", item.IsBulkBox);
        JFlt(sb, "MinMarketPricePercent", item.MinMarketPricePercent);
        JFlt(sb, "MaxMarketPricePercent", item.MaxMarketPricePercent);
        JStr(sb, "FollowItemPrice", bundleItemPricesApi || item.FollowItemPriceSuppliedByApi ? "None" : item.FollowItemPrice);
        JBool(sb, "AutoSetBoxPrice", !bundleItemPricesApi && !item.AutoBoxPriceSuppliedByApi && item.AutoSetBoxPrice);
        JBool(sb, "MarketPricePercentSuppliedByApi", item.MarketPricePercentSuppliedByApi);
        JBool(sb, "FollowItemPriceSuppliedByApi", item.FollowItemPriceSuppliedByApi);
        JBool(sb, "AutoBoxPriceSuppliedByApi", item.AutoBoxPriceSuppliedByApi);
        JBool(sb, "LicensePriceSuppliedByApi", item.LicensePriceSuppliedByApi);
        JBool(sb, "LicenseLevelSuppliedByApi", item.LicenseLevelSuppliedByApi);
        JBool(sb, "IsTallItem", item.IsTallItem);
        JFlt(sb, "InBoxOffsetY", item.InBoxOffsetY);
        JFlt(sb, "InBoxOffsetScale", item.InBoxOffsetScale);
        JVec3(sb, "ItemDeminsion", item.ItemDeminsion);
        JVec3(sb, "CollidorPosOffset", item.CollidorPosOffset);
        JVec3(sb, "ColliderScale", item.ColliderScale);
        JStrList(sb, "PriceAffectedBy", item.PriceAffectedBy);
        JBool(sb, "IsCardPack", item.IsCardPack);
        JBool(sb, "ApiManaged", item.ApiManaged);
        JBool(sb, "IsCardBox", item.IsCardBox);
        JInt(sb, "MinValue", item.MinValue);
        JStr(sb, "CardExpansion", item.CardExpansion);
        JBool(sb, "CanHaveDuplicates", item.CanHaveDuplicates);
        JBool(sb, "CanHaveGodPacks", !item.ApiManaged && item.CanHaveGodPacks);
        JFlt(sb, "GodPackPercentage", item.GodPackPercentage);
        JStr(sb, "PackGenerationStrategy", item.ApiManaged ? "" : item.PackGenerationStrategy.ToString());
        JStr(sb, "SpawnsPackType", item.SpawnsPackType);
        JStr(sb, "PackType", item.ApiManaged ? "" : item.PackType);
        JBool(sb, "NormalWeightsSuppliedByApi", item.NormalWeightsSuppliedByApi);
        JBool(sb, "GodWeightsSuppliedByApi", item.GodWeightsSuppliedByApi);
        JBool(sb, "SlotWeightsSuppliedByApi", item.SlotWeightsSuppliedByApi);
        JBool(sb, "CanHaveGodPacksSuppliedByApi", item.CanHaveGodPacksSuppliedByApi);
        JBool(sb, "GodPackPercentageSuppliedByApi", item.GodPackPercentageSuppliedByApi);
        JRarityWeightDict(sb, "NormalWeights", exportNormalWeights);
        JRarityWeightDict(sb, "GodWeights", exportGodWeights);
        JSlotWeightDict(sb, "SlotWeights", exportSlotWeights);
        if (sb[sb.Length - 1] == ',')
        {
            sb.Length--;
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static List<string> FindExpansionRarities(string cardExpansion)
    {
        if (string.IsNullOrEmpty(cardExpansion))
        {
            return null;
        }
        var expansion = bundleData.Values.SelectMany(b => b.CardExpansions)
            .FirstOrDefault(cs => cs.CardExpansion == cardExpansion || cs.Name == cardExpansion);
        return expansion?.Rarities.Where(r => !string.IsNullOrEmpty(r)).ToList();
    }

    private static void JStr(StringBuilder sb, string key, string value)
        => sb.Append($"\"{key}\":\"{JEsc(value)}\",");

    private static void JFlt(StringBuilder sb, string key, float value)
        => sb.Append($"\"{key}\":{value.ToString("G9", CultureInfo.InvariantCulture)},");

    private static void JInt(StringBuilder sb, string key, int value)
        => sb.Append($"\"{key}\":{value},");

    private static void JBool(StringBuilder sb, string key, bool value)
        => sb.Append($"\"{key}\":{(value ? "true" : "false")},");

    private static void JVec3(StringBuilder sb, string key, Vector3 v)
        => sb.Append($"\"{key}\":{{\"x\":{v.x.ToString("G9", CultureInfo.InvariantCulture)},\"y\":{v.y.ToString("G9", CultureInfo.InvariantCulture)},\"z\":{v.z.ToString("G9", CultureInfo.InvariantCulture)}}},");

    private static void JStrList(StringBuilder sb, string key, List<string> values)
    {
        sb.Append($"\"{key}\":[");
        if (values is not null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append($"\"{JEsc(values[i])}\"");
            }
        }
        sb.Append("],");
    }

    private static void JRarityWeightDict(StringBuilder sb, string key, List<RarityWeightEntry> entries)
    {
        sb.Append($"\"{key}\":{{");
        if (entries is not null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append($"\"{JEsc(entries[i].Rarity)}\":{entries[i].Weight.ToString("G9", CultureInfo.InvariantCulture)}");
            }
        }
        sb.Append("},");
    }

    private static void JSlotWeightDict(StringBuilder sb, string key, List<SlotWeightEntry> entries)
    {
        sb.Append($"\"{key}\":{{");
        if (entries is not null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append($"\"{entries[i].Slot}\":{{");
                var weights = entries[i].Weights;
                for (int j = 0; j < weights.Count; j++)
                {
                    if (j > 0)
                    {
                        sb.Append(",");
                    }
                    sb.Append($"\"{JEsc(weights[j].Rarity)}\":{weights[j].Weight.ToString("G9", CultureInfo.InvariantCulture)}");
                }
                sb.Append("}");
            }
        }
        sb.Append("},");
    }

    private static string JEsc(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                    {
                        sb.Append($"\\u{(int)c:x4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    // ── Dict-format descriptor fallback loader ────────────────────────────────
    // The game-facing .json stores item weights as dictionaries, which JsonUtility
    // cannot read. When the .catalog.json working file is missing, this path splices
    // the Items array out for manual parsing and reads the rest with JsonUtility, so
    // the full descriptor (including BundleId) survives the roundtrip.

    private static bool IsDictFormatDescriptorJson(string json)
        => Regex.IsMatch(json, "\"(NormalWeights|GodWeights|SlotWeights)\"\\s*:\\s*\\{");

    private static AssetBundleCatalog LoadFromDictFormatDescriptorJson(string json)
    {
        var (start, length) = FindItemsArraySpan(json);
        string itemsJson = json.Substring(start, length);
        string jsonWithoutItems = json.Remove(start, length).Insert(start, "[]");
        var catalog = JsonUtility.FromJson<AssetBundleCatalog>(jsonWithoutItems);
        catalog.Items = ParseDictFormatItems(itemsJson);
        return catalog;
    }

    private static (int start, int length) FindItemsArraySpan(string json)
    {
        int depth = 0;
        int i = 0;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                string key = ReadJsonString(json, ref i);
                int afterString = SkipJsonWhitespace(json, i);
                if (depth == 1 && key == "Items" && afterString < json.Length && json[afterString] == ':')
                {
                    int valueStart = SkipJsonWhitespace(json, afterString + 1);
                    if (valueStart < json.Length && json[valueStart] == '[')
                    {
                        int valueEnd = FindMatchingJsonBracket(json, valueStart);
                        return (valueStart, valueEnd - valueStart + 1);
                    }
                }
                continue;
            }
            if (c == '{' || c == '[')
            {
                depth++;
            }
            else if (c == '}' || c == ']')
            {
                depth--;
            }
            i++;
        }
        throw new FormatException("Descriptor JSON has no root-level \"Items\" array.");
    }

    private static int FindMatchingJsonBracket(string json, int openIndex)
    {
        int depth = 0;
        int i = openIndex;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                ReadJsonString(json, ref i);
                continue;
            }
            if (c == '{' || c == '[')
            {
                depth++;
            }
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
            i++;
        }
        throw new FormatException("Unterminated array in descriptor JSON.");
    }

    private static List<ItemEntry> ParseDictFormatItems(string itemsJson)
    {
        var items = new List<ItemEntry>();
        int i = 0;
        if (ParseJsonValue(itemsJson, ref i) is List<object> parsed)
        {
            foreach (var element in parsed)
            {
                if (element is Dictionary<string, object> dict)
                {
                    items.Add(MapDictFormatItem(dict));
                }
            }
        }
        return items;
    }

    private static ItemEntry MapDictFormatItem(Dictionary<string, object> d)
    {
        var item = new ItemEntry
        {
            Name = DStr(d, "Name"),
            ItemCategory = DStr(d, "ItemCategory"),
            SpriteName = DStr(d, "SpriteName"),
            BaseCost = DFlt(d, "BaseCost"),
            AddItemAsAccessory = DBool(d, "AddItemAsAccessory"),
            AddItemAsFigurine = DBool(d, "AddItemAsFigurine"),
            AddItemAsBoardGame = DBool(d, "AddItemAsBoardGame"),
            IsSprayCan = DBool(d, "IsSprayCan"),
            PhoneAppId = DStr(d, "PhoneAppId"),
            ItemType = DStr(d, "ItemType"),
            UsesBaseGameMesh = DBool(d, "UsesBaseGameMesh"),
            MeshToUse = DStr(d, "MeshToUse"),
            Mesh = DStr(d, "Mesh"),
            MeshSecondary = DStr(d, "MeshSecondary"),
            Material = DStr(d, "Material"),
            MaterialSecondary = DStr(d, "MaterialSecondary"),
            MaterialList = DStrList(d, "MaterialList"),
            HasBigBox = DBool(d, "IsBigBox"),
            BigBoxHideTillUnlocked = DBool(d, "HideTillUnlocked") || DBool(d, "BigBoxHideTillUnlocked"),
            BigBoxLicensePrice = DFlt(d, "LicensePrice"),
            BigBoxLicenseLevelRequirement = DInt(d, "LicenseLevelRequirement"),
            BigBoxIgnoreDoubleImage = DBool(d, "IgnoreDoubleImage"),
            HasSmallBox = DBool(d, "HasSmallBox"),
            SmallBoxHideTillUnlocked = DBool(d, "SmallBoxHideTillUnlocked"),
            SmallBoxLicensePrice = DFlt(d, "SmallBoxLicensePrice"),
            SmallBoxLicenseLevelRequirement = DInt(d, "SmallBoxLicenseLevelRequirement"),
            SmallBoxIgnoreDoubleImage = DBool(d, "SmallBoxIgnoreDoubleImage"),
            IsBulkBox = DBool(d, "IsBulkBox"),
            MinMarketPricePercent = DFlt(d, "MinMarketPricePercent"),
            MaxMarketPricePercent = DFlt(d, "MaxMarketPricePercent"),
            FollowItemPrice = DStr(d, "FollowItemPrice"),
            AutoSetBoxPrice = DBool(d, "AutoSetBoxPrice"),
            IsTallItem = DBool(d, "IsTallItem"),
            InBoxOffsetY = DFlt(d, "InBoxOffsetY"),
            InBoxOffsetScale = DFlt(d, "InBoxOffsetScale"),
            ItemDeminsion = DVec3(d, "ItemDeminsion"),
            CollidorPosOffset = DVec3(d, "CollidorPosOffset"),
            ColliderScale = DVec3(d, "ColliderScale"),
            PriceAffectedBy = DStrList(d, "PriceAffectedBy"),
            IsCardPack = DBool(d, "IsCardPack"),
            ApiManaged = DBool(d, "ApiManaged"),
            IsCardBox = DBool(d, "IsCardBox"),
            MinValue = DInt(d, "MinValue"),
            CardExpansion = DStr(d, "CardExpansion"),
            CanHaveDuplicates = DBool(d, "CanHaveDuplicates"),
            CanHaveGodPacks = DBool(d, "CanHaveGodPacks"),
            GodPackPercentage = DFlt(d, "GodPackPercentage"),
            SpawnsPackType = DStr(d, "SpawnsPackType"),
            PackType = DStr(d, "PackType"),
            NormalWeightsSuppliedByApi = DBool(d, "NormalWeightsSuppliedByApi"),
            GodWeightsSuppliedByApi = DBool(d, "GodWeightsSuppliedByApi"),
            SlotWeightsSuppliedByApi = DBool(d, "SlotWeightsSuppliedByApi"),
            CanHaveGodPacksSuppliedByApi = DBool(d, "CanHaveGodPacksSuppliedByApi"),
            GodPackPercentageSuppliedByApi = DBool(d, "GodPackPercentageSuppliedByApi"),
            MarketPricePercentSuppliedByApi = DBool(d, "MarketPricePercentSuppliedByApi"),
            FollowItemPriceSuppliedByApi = DBool(d, "FollowItemPriceSuppliedByApi"),
            AutoBoxPriceSuppliedByApi = DBool(d, "AutoBoxPriceSuppliedByApi"),
            LicensePriceSuppliedByApi = DBool(d, "LicensePriceSuppliedByApi"),
            LicenseLevelSuppliedByApi = DBool(d, "LicenseLevelSuppliedByApi"),
            NormalWeights = DRarityWeights(d, "NormalWeights"),
            GodWeights = DRarityWeights(d, "GodWeights"),
            SlotWeights = DSlotWeights(d, "SlotWeights"),
        };
        if (Enum.TryParse(DStr(d, "PackGenerationStrategy"), out PackGenerationStrategy strategy))
        {
            item.PackGenerationStrategy = strategy;
        }
        item.UseCustomMinValue = item.IsBulkBox && GetValueFromBulkBoxType(GetBulkBoxTypeFromValue(item.MinValue)) != item.MinValue;
        return item;
    }

    private static string DStr(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v is string s ? s : string.Empty;

    private static float DFlt(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v is double n ? (float)n : 0f;

    private static int DInt(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v is double n ? (int)n : 0;

    private static bool DBool(Dictionary<string, object> d, string key)
        => d.TryGetValue(key, out var v) && v is bool b && b;

    private static Vector3 DVec3(Dictionary<string, object> d, string key)
    {
        if (d.TryGetValue(key, out var v) && v is Dictionary<string, object> vec)
        {
            return new Vector3(DFlt(vec, "x"), DFlt(vec, "y"), DFlt(vec, "z"));
        }
        return Vector3.zero;
    }

    private static List<string> DStrList(Dictionary<string, object> d, string key)
    {
        var result = new List<string>();
        if (d.TryGetValue(key, out var v) && v is List<object> list)
        {
            foreach (var element in list)
            {
                if (element is string s)
                {
                    result.Add(s);
                }
            }
        }
        return result;
    }

    private static List<RarityWeightEntry> DRarityWeights(Dictionary<string, object> d, string key)
    {
        var result = new List<RarityWeightEntry>();
        if (d.TryGetValue(key, out var v) && v is Dictionary<string, object> weights)
        {
            foreach (var pair in weights)
            {
                result.Add(new RarityWeightEntry { Rarity = pair.Key, Weight = pair.Value is double w ? (float)w : 0f });
            }
        }
        return result;
    }

    private static List<SlotWeightEntry> DSlotWeights(Dictionary<string, object> d, string key)
    {
        var result = new List<SlotWeightEntry>();
        if (d.TryGetValue(key, out var v) && v is Dictionary<string, object> slots)
        {
            foreach (var pair in slots)
            {
                if (!Enum.TryParse(pair.Key, out Slots slot) || !(pair.Value is Dictionary<string, object> weights))
                {
                    continue;
                }
                var entry = new SlotWeightEntry { Slot = slot, Weights = new List<RarityWeightEntry>() };
                foreach (var weight in weights)
                {
                    entry.Weights.Add(new RarityWeightEntry { Rarity = weight.Key, Weight = weight.Value is double w ? (float)w : 0f });
                }
                result.Add(entry);
            }
        }
        return result;
    }

    // ── Minimal JSON reader (exporter has no Newtonsoft reference) ─────────────

    private static object ParseJsonValue(string json, ref int i)
    {
        i = SkipJsonWhitespace(json, i);
        char c = json[i];
        if (c == '{')
        {
            return ParseJsonObject(json, ref i);
        }
        if (c == '[')
        {
            return ParseJsonArray(json, ref i);
        }
        if (c == '"')
        {
            return ReadJsonString(json, ref i);
        }
        if (c == 't') { i += 4; return true; }
        if (c == 'f') { i += 5; return false; }
        if (c == 'n') { i += 4; return null; }
        return ParseJsonNumber(json, ref i);
    }

    private static Dictionary<string, object> ParseJsonObject(string json, ref int i)
    {
        var result = new Dictionary<string, object>();
        i++;
        i = SkipJsonWhitespace(json, i);
        if (json[i] == '}')
        {
            i++;
            return result;
        }
        while (true)
        {
            i = SkipJsonWhitespace(json, i);
            string key = ReadJsonString(json, ref i);
            i = SkipJsonWhitespace(json, i);
            i++;
            result[key] = ParseJsonValue(json, ref i);
            i = SkipJsonWhitespace(json, i);
            if (json[i++] == '}')
            {
                return result;
            }
        }
    }

    private static List<object> ParseJsonArray(string json, ref int i)
    {
        var result = new List<object>();
        i++;
        i = SkipJsonWhitespace(json, i);
        if (json[i] == ']')
        {
            i++;
            return result;
        }
        while (true)
        {
            result.Add(ParseJsonValue(json, ref i));
            i = SkipJsonWhitespace(json, i);
            if (json[i++] == ']')
            {
                return result;
            }
        }
    }

    private static string ReadJsonString(string json, ref int i)
    {
        var sb = new StringBuilder();
        i++;
        while (json[i] != '"')
        {
            if (json[i] == '\\')
            {
                i++;
                char e = json[i];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(json.Substring(i + 1, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            else
            {
                sb.Append(json[i]);
            }
            i++;
        }
        i++;
        return sb.ToString();
    }

    private static object ParseJsonNumber(string json, ref int i)
    {
        int start = i;
        while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '+' || json[i] == '.' || json[i] == 'e' || json[i] == 'E'))
        {
            i++;
        }
        return double.Parse(json.Substring(start, i - start), CultureInfo.InvariantCulture);
    }

    private static int SkipJsonWhitespace(string json, int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i]))
        {
            i++;
        }
        return i;
    }
}
