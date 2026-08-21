import math

RARITIES = [
    "Base",
    "FirstEdition",
    "Silver",
    "Gold",
    "EX",
    "FullArt",
    "FullArtAnimated",
]

LEVELS = {
    "Basic": {
        "Common": 1,
        "Rare": 5,
        "Epic": 12,
        "Legendary": 20,
    },
    "Destiny": {
        "Common": 25,
        "Rare": 30,
        "Epic": 40,
        "Legendary": 50,
    },
}

ELEMENTS = ["Fire", "Earth", "Water", "Wind"]
SLEEVES = ["Clear"] + ELEMENTS + ["Tetramon"]


def bundle(name, items, expansions=None) -> dict:
    return {
        "Name": f"Nikke_{name}",
        "BundleId": f"nikke_{name.lower()}",
        "AutoCompressTextures": True,
        "MaxTextureSize": 2048,
        "TextureCompression": "BC7",
        "EnableMipMaps": False,
        "Assemblies": [],
        "Items": items,
        "CardExpansions": expansions or [],
        "CustomShops": [
            {
                "PhoneAppId": "Nikke",
                "AppDisplayName": "Nikke TCG",
                "AppIcon": "Icon",
                "AppInnerBGImage": "IBG",
                "AppOuterBGImage": "OBG",
                "SiteLogo": "_ShopLogo",
                "SiteHeader": "_ShopBanner",
                "SiteBackground": "_ShopWallpaper",
            }
        ],
    }


def expansion(set, cards) -> dict:
    return {
        "Name": f"{set} Set",
        "CardExpansion": f"Nikke_{set}",
        "Cards": cards,
        "MenuCategory": "Nikke",
        "CardbackSprite": "_Cardback",
        "FoilMask": "_Foilmask",
        "Rarities": RARITIES,
        "HasRandomFoils": False,
        "FoilChance": 0,
        "CardPriceStrategy": "RarityDriven",
        "DefaultBorderMultiplierBase": 3.15,
        "DefaultFoilMultiplier": 10.0,
        "RarityDrivenFloor": 0.1,
        "RarityDrivenStepSize": 1.28,
        "RarityDrivenBorderMultiplierBase": 1.75,
        "RarityDrivenFoilMultiplier": 10.0,
        "PlayCardMaterials": [],
    }


def pack(set, kind) -> dict:
    t = list(LEVELS[set].keys()).index(kind)

    common = t / (len(LEVELS[set]) - 1)
    premium = common**1.5

    # -------------------------
    # Slots 1-4: Common
    # -------------------------
    first_edition = 1 + 9 * common

    slot_common = {
        "Base": 100 - first_edition,
        "FirstEdition": first_edition,
        "Silver": 0,
        "Gold": 0,
        "EX": 0,
        "FullArt": 0,
        "FullArtAnimated": 0,
    }

    # -------------------------
    # Slots 5-6: Uncommon
    # -------------------------
    ex = 2 + 28 * premium
    gold = 28 + 22 * common
    silver = 100 - gold - ex

    slot_uncommon = {
        "Base": 0,
        "FirstEdition": 0,
        "Silver": silver,
        "Gold": gold,
        "EX": ex,
        "FullArt": 0,
        "FullArtAnimated": 0,
    }

    # -------------------------
    # Slot 7: Rare+
    # -------------------------
    animated = 0.1 + 0.9 * premium
    full_art = 0.5 + 4.5 * premium
    ex = 9.5 + 35.5 * premium
    gold = 100 - ex - full_art - animated

    slot_rare = {
        "Base": 0,
        "FirstEdition": 0,
        "Silver": 0,
        "Gold": gold,
        "EX": ex,
        "FullArt": full_art,
        "FullArtAnimated": animated,
    }

    slot_weights = {
        "Slot1": slot_common,
        "Slot2": slot_common,
        "Slot3": slot_common,
        "Slot4": slot_common,
        "Slot5": slot_uncommon,
        "Slot6": slot_uncommon,
        "Slot7": slot_rare,
    }

    pack_at = LEVELS[set][kind]
    next_pack_at = next_unlock(pack_at)

    level = pack_at
    level_big = pack_at + math.ceil((next_pack_at - pack_at) / 4)

    return {
        "Name": f"{set} {kind} Packs",
        "CardExpansion": f"Nikke_{set}",
        "PackType": f"Nikke_{set}_{kind}_Pack",
        "ItemType": f"Nikke_{set}_{kind}_Pack_Item",
        "Material": f"_BoosterPack_{kind}_Material",
        "SpriteName": f"_BoosterPack_{kind}_Icon",
        "BaseCost": 2 * (t + 1),
        "SlotWeights": slot_weights,
        "ItemCategory": "TCG",
        "AddItemAsAccessory": False,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "BasicCardPack",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 50 * level_big,
        "LicenseLevelRequirement": level_big,
        "IgnoreDoubleImage": False,
        "HasSmallBox": True,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 50 * level,
        "SmallBoxLicenseLevelRequirement": level,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": False,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": True,
        "IsCardBox": False,
        "MinValue": 0,
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.000199999995,
        "PackGenerationStrategy": "Guaranteed",
        "SpawnsPackType": "",
        "NormalWeights": {},
        "GodWeights": {},
    }


def box(set, kind) -> dict:
    pack_at = LEVELS[set][kind]
    next_pack_at = next_unlock(pack_at)
    level = pack_at + math.ceil((next_pack_at - pack_at) / 2)
    level_big = pack_at + math.ceil(3 * (next_pack_at - pack_at) / 4)

    return {
        "Name": f"{set} {kind} Box",
        "Material": f"_BoosterBox_{kind}_Material",
        "SpriteName": f"_BoosterBox_{kind}_Icon",
        "ItemType": f"Nikke_{set}_{kind}_Box_Item",
        "SpawnsPackType": f"Nikke_{set}_{kind}_Pack_Item",
        "ItemCategory": "TCG",
        "BaseCost": 0,
        "AddItemAsAccessory": False,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "BasicCardBox",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 50 * level_big,
        "LicenseLevelRequirement": level_big,
        "IgnoreDoubleImage": False,
        "HasSmallBox": True,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 50 * level,
        "SmallBoxLicenseLevelRequirement": level,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": True,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def card(name, number, rarity, padding=4) -> dict:
    RARITY_BORDER = {
        "Silver": "Base",
        "Gold": "Base",
        "EX": "Silver",
        "FullArtAnimated": "FullArt",
    }

    filename = "".join(c for c in name if c.isalnum() or c.isspace()).replace(" ", "_")
    filename = f"{str(number).rjust(padding, '0')}_{filename}_{rarity}"

    return {
        "Name": name,
        "Rarity": rarity,
        "BorderType": RARITY_BORDER.get(rarity, rarity),
        "CardNumber": number,
        "Sprite": filename,
        "IsFoil": rarity == "EX" or "FullArt" in rarity,
        "ElementType": "None",
        "FoilMask": "",
        "CardBack": "",
    }


def deck(set, element) -> dict:
    CHARACTERS = {
        "Basic": {
            "Fire": "Modernia",
            "Earth": "Raven",
            "Water": "Ludmilla",
            "Wind": "Scarlet",
        },
        "Destiny": {
            "Fire": "Alice",
            "Earth": "Red_Hood",
            "Water": "Dorothy",
            "Wind": "Siren",
        },
    }

    character = CHARACTERS[set][element]

    tier = [
        character
        for characters in CHARACTERS.values()
        for character in characters.values()
    ].index(character) + 1

    level = [level for levels in LEVELS.values() for level in levels.values()][tier - 1]
    level_next = next_unlock(level)
    level_requirement = level + (level_next - level) // 2

    return {
        "Name": f"{character.replace('_', ' ')} Deck",
        "Material": f"_BattleDeck_{character}_Material",
        "SpriteName": f"_BattleDeck_{character}_Icon",
        "ItemType": f"Nikke_{character}_Deck_Item",
        "SpawnsPackType": "",
        "ItemCategory": "TCG",
        "BaseCost": 15 * tier,
        "AddItemAsAccessory": True,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "PreconDeck_Fire",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 100 * level_requirement,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def deck_box(i) -> dict:
    CHARACTERS = [
        "D_Killer_Wife",
        "Siren",
        "Helm",
        "Red_Hood",
    ]

    character = CHARACTERS[i]
    tier = i + 1

    level = [level for levels in LEVELS.values() for level in levels.values()][tier - 1]
    level_next = next_unlock(level)
    level_requirement = level + math.ceil((level_next - level) / 4)

    return {
        "Name": f"{character.replace('_', ' ')} Deck Box",
        "Material": f"_DeckBox_{character}_Material",
        "SpriteName": f"_DeckBox_{character}_Icon",
        "ItemType": f"Nikke_{character}_DeckBox_Item",
        "SpawnsPackType": "",
        "ItemCategory": "Deckbox",
        "BaseCost": 20 * tier,
        "AddItemAsAccessory": True,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "DeckBox1",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 15 * level_requirement,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def manga(name, i):
    tier = i + 1
    level_requirement = 5 + math.ceil(1.2 * tier**1.6)

    return {
        "Name": name,
        "Material": f"_Manga_{tier}_Material",
        "SpriteName": f"_Manga_{tier}_Icon",
        "ItemType": f"Nikke_Manga_{tier}_Item",
        "SpawnsPackType": "",
        "ItemCategory": "Manga",
        "BaseCost": 5 * tier,
        "AddItemAsAccessory": True,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "Manga1",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 250 * level_requirement,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def playmat(name, i):
    tier = i + 1
    level_requirement = 3 + math.ceil(1.4 * tier**1.2)

    return {
        "Name": name,
        "Material": f"_Playmat_{tier}_Material",
        "SpriteName": f"_Playmat_{tier}_Icon",
        "ItemType": f"Nikke_Playmat_{tier}_Item",
        "SpawnsPackType": "",
        "ItemCategory": "Playmat",
        "BaseCost": 2 * tier,
        "AddItemAsAccessory": True,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "Playmat1",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 80 * level_requirement,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def sleeve(name, kind):
    tier = SLEEVES.index(kind) + 1
    level_requirement = 3 + math.ceil(1.1 * tier**1.8)

    return {
        "Name": name,
        "Material": f"_Sleeve_{tier}_Material",
        "SpriteName": f"_Sleeve_{tier}_Icon",
        "ItemType": f"Nikke_Sleeve_{tier}_Item",
        "SpawnsPackType": "",
        "ItemCategory": "Sleeve",
        "BaseCost": 1 * tier,
        "AddItemAsAccessory": True,
        "AddItemAsFigurine": False,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "UsesBaseGameMesh": True,
        "MeshToUse": "CardSleeve_Clear",
        "Mesh": "",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": 40 * level_requirement,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": False,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 1, "y": 1, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 1, "y": 1, "z": 1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def figurine(original):
    CHARACTERS = {
        "PiggyA": "Rapi",
        "GolemA": "Poli",
        "StarfishA": "Snow White",
        "BatB": "Grave",
        "BatD": "Cinderella",
        "BatA": "Red Hood",
        "Beetle": "Shifty",
        "GolemB": "Miranda",
        "GolemC": "Quiry",
        "GolemD": "D Killer Wife",
        "PigB": "Neon",
        "FoxB": "Red Hood (Nonsense Red)",
        "PiggyC": "Anis",
        "StarfishD": "Dorothy",
        "PiggyD": "Marian",
        "StarfishB": "Scarlet",
        "StarFishC": "Rapunzel",
        "ToonZ": "Liliweiss",
        "BatC": "Siren",
    }

    name = CHARACTERS[original]
    tier = list(CHARACTERS.values()).index(name) + 1
    filename = name.replace(" ", "_").replace("(", "").replace(")", "")
    level_requirement = 5 + math.ceil(1.3 * tier**1.35)

    return {
        "Name": name,
        "Material": f"_Figurine_{filename}_Material",
        "SpriteName": f"_Figurine_{filename}_Icon",
        "ItemType": f"Nikke_Figurine_{filename}_Item",
        "UsesBaseGameMesh": False,
        "MeshToUse": "",
        "Mesh": f"_Figurine_{filename}_Mesh",
        "SpawnsPackType": "",
        "ItemCategory": "Figurine",
        "BaseCost": 10 * tier,
        "AddItemAsAccessory": False,
        "AddItemAsFigurine": True,
        "AddItemAsBoardGame": False,
        "PhoneAppId": "Nikke",
        "MaterialList": [],
        "IsBigBox": True,
        "BigBoxHideTillUnlocked": False,
        "LicensePrice": math.ceil(5 * level_requirement**1.8 / 10) * 10,
        "LicenseLevelRequirement": level_requirement,
        "IgnoreDoubleImage": True,
        "HasSmallBox": False,
        "SmallBoxHideTillUnlocked": False,
        "SmallBoxLicensePrice": 0,
        "SmallBoxLicenseLevelRequirement": 0,
        "SmallBoxIgnoreDoubleImage": False,
        "IsBulkBox": False,
        "MinMarketPricePercent": 1,
        "MaxMarketPricePercent": 1.20000005,
        "FollowItemPrice": "None",
        "AutoSetBoxPrice": True,
        "IsTallItem": True,
        "InBoxOffsetY": 0,
        "InBoxOffsetScale": 0,
        "ItemDeminsion": {"x": 2, "y": 4, "z": 1},
        "CollidorPosOffset": {"x": 0, "y": 0, "z": 0},
        "ColliderScale": {"x": 0.1, "y": 0.2, "z": 0.1},
        "PriceAffectedBy": [],
        "IsCardPack": False,
        "IsCardBox": False,
        "MinValue": 0,
        "CardExpansion": "",
        "CanHaveDuplicates": False,
        "CanHaveGodPacks": False,
        "GodPackPercentage": 0.0,
        "PackGenerationStrategy": "Guaranteed",
        "PackType": "",
        "NormalWeights": {},
        "GodWeights": {},
        "SlotWeights": {},
    }


def next_unlock(level):
    for levels in LEVELS.values():
        for candidate in levels.values():
            if candidate > level:
                return candidate

    return 70  # Endgame
