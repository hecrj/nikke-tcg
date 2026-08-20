RARITIES = [
    "Base",
    "FirstEdition",
    "Silver",
    "Gold",
    "EX",
    "FullArt",
    "FullArtAnimated",
]


def bundle(set, items, expansions) -> dict:
    return {
        "Name": f"Nikke_{set}",
        "BundleId": f"nikke_{set.lower()}",
        "AutoCompressTextures": True,
        "MaxTextureSize": 2048,
        "TextureCompression": "BC7",
        "EnableMipMaps": False,
        "Assemblies": [],
        "Items": items,
        "CardExpansions": expansions,
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
    SLOT_COMMON = {
        "Base": 99,
        "FirstEdition": 1,
        "Silver": 0,
        "Gold": 0,
        "EX": 0,
        "FullArt": 0,
        "FullArtAnimated": 0,
    }

    SLOT_UNCOMMON = {
        "Base": 0,
        "FirstEdition": 0,
        "Silver": 64,
        "Gold": 16,
        "EX": 1,
        "FullArt": 0,
        "FullArtAnimated": 0,
    }

    SLOT_RARE = {
        "Base": 0,
        "FirstEdition": 0,
        "Silver": 0,
        "Gold": 0,
        "EX": 0,
        "FullArt": 0,
        "FullArtAnimated": 1,
    }

    SLOT_WEIGHTS = {
        "Slot1": SLOT_COMMON,
        "Slot2": SLOT_COMMON,
        "Slot3": SLOT_COMMON,
        "Slot4": SLOT_COMMON,
        "Slot5": SLOT_UNCOMMON,
        "Slot6": SLOT_UNCOMMON,
        "Slot7": SLOT_RARE,
    }

    return {
        "Name": f"{set} {kind} Packs",
        "CardExpansion": f"Nikke_{set}",
        "PackType": f"Nikke_{set}_{kind}_Pack",
        "ItemType": f"Nikke_{set}_{kind}_Pack_Item",
        "Material": f"_BoosterPack_{kind}",
        "SpriteName": f"_BoosterPack_{kind}_Icon",
        "BaseCost": 2,
        "SlotWeights": SLOT_WEIGHTS,
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
        "LicensePrice": 0,
        "LicenseLevelRequirement": 0,
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
    return {
        "Name": f"{set} {kind} Box",
        "Material": f"_BoosterBox_{kind}",
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
        "LicensePrice": 0,
        "LicenseLevelRequirement": 0,
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
        "IsFoil": rarity == "FullArt" or rarity == "EX",
        "ElementType": "None",
        "FoilMask": "",
        "CardBack": "",
    }
