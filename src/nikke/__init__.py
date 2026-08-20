import configparser
import json
import math
import pathlib
import shutil
import subprocess

from PIL import Image


WORKING_DIR = pathlib.Path.cwd()
ORIGINAL_DIR = WORKING_DIR.joinpath("original/BepInEx")
TEXTURE_DIR = ORIGINAL_DIR.joinpath("plugins/TextureReplacer/objects_textures/nikke")
ORIGINAL_CARDS_DIR = ORIGINAL_DIR.joinpath("plugins/CardConfigurator/Configs")
ORIGINAL_PACKS_DIR = TEXTURE_DIR.joinpath("packs")
ART_STATIC_DIR = WORKING_DIR.joinpath("cards/Texture2D/assets/cardart/default")
ART_ANIMATED_DIR = WORKING_DIR.joinpath("cards/Texture2D/assets/animated/default/ghost")
EXTERNAL_DIR = WORKING_DIR.joinpath("external")
OUTPUT_DIR = WORKING_DIR.joinpath("output")
ANIMATED_OUTPUT_DIR = OUTPUT_DIR.joinpath("animated")

CARDBACK = TEXTURE_DIR.joinpath("cards/T_CardBackMesh.png")
LOGO = TEXTURE_DIR.joinpath("misc/GameTitle.png")

RARITY_MULTIPLIER = {
    "Base": 1,
    "FirstEdition": 2,
    "Silver": 3,
    "Gold": 4,
    "EX": 5,
    "FullArt": 6,
    "FullArtAnimated": 7,
}

RARITY_BORDER = {
    "Silver": "Base",
    "Gold": "Base",
    "EX": "Silver",
}

SETS = {
    "Tetramon": "Basic",
    "Destiny": "Destiny",
}

BUNDLE = {
    "Name": "",
    "BundleId": "",
    "AutoCompressTextures": True,
    "MaxTextureSize": 2048,
    "TextureCompression": "BC7",
    "EnableMipMaps": False,
    "Assemblies": [],
    "Items": [],
    "CardExpansions": [],
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

PACK = {
    "Name": "",
    "CardExpansion": "",
    "PackType": "",
    "ItemType": "",
    "BaseCost": 2,
    "ItemCategory": "TCG",
    "Material": "_BoosterPack",
    "SpriteName": "_BoosterPackIcon",
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
    "SlotWeights": SLOT_WEIGHTS,
}

EXPANSION = {
    "Name": "",
    "CardExpansion": "",
    "MenuCategory": "Nikke",
    "CardbackSprite": "_Cardback",
    "FoilMask": "_Foilmask",
    "Rarities": list(RARITY_MULTIPLIER.keys()),
    "Cards": [],
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

CARD = {
    "Name": "",
    "Rarity": "",
    "CardNumber": 0,
    "Sprite": "",
    "IsFoil": False,
    "BorderType": "Base",
    "ElementType": "None",
    "FoilMask": "",
    "CardBack": "",
}


def box(set, kind) -> dict:
    material = f"_BoosterBox_{kind}"
    sprite = f"{material}_Icon"

    prefix = "" if set == "Basic" else "Destiny_"
    original_kind = "Legend" if kind == "Legendary" else kind
    original_kind = "" if kind == "Common" else original_kind

    TEXTURE_DIR.joinpath("boxes").joinpath(f"{prefix}{original_kind}Cardbox.png").copy(
        OUTPUT_DIR.joinpath(f"Nikke_{set}/{sprite}.png")
    )

    return {
        "Name": f"{set} {kind} Box",
        "Material": material,
        "SpriteName": sprite,
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


def extract() -> None:
    shutil.rmtree(OUTPUT_DIR, ignore_errors=True)
    OUTPUT_DIR.mkdir()

    for set in ART_STATIC_DIR.iterdir():
        set_name = SETS.get(set.name)

        if set_name is None:
            continue

        total_cards = len(next(set.joinpath("base").walk())[2])
        total_digits = math.ceil(math.log10(total_cards))
        total_animated = 0

        output_set = OUTPUT_DIR.joinpath(f"Nikke_{set_name}")
        output_set.mkdir()

        cards = []

        for expansion in set.iterdir():
            if not expansion.is_dir():
                continue

            for card in expansion.iterdir():
                metadata = (
                    ORIGINAL_CARDS_DIR.joinpath(set.name)
                    .joinpath("default")
                    .joinpath(f"{card.stem}.ini")
                )

                if not metadata.is_file():
                    print(
                        f"[{expansion.name} - {card.name}] Config not found. Skipping..."
                    )
                    continue

                config = configparser.ConfigParser()
                config.read(metadata)

                multiplier = RARITY_MULTIPLIER.get(expansion.name, 1)

                name = config[card.stem]["Name"]
                number = int(config[card.stem]["Number"]) + total_cards * (
                    multiplier - 1
                )

                filename = "".join(
                    c for c in name if c.isalnum() or c.isspace()
                ).replace(" ", "_")
                filename = f"{str(number).rjust(total_digits, '0')}_{filename}_{expansion.name}"

                card.copy(output_set.joinpath(f"{filename}.png"))

                card = {
                    "Name": name,
                    "Rarity": expansion.name,
                    "BorderType": RARITY_BORDER.get(expansion.name, expansion.name),
                    "CardNumber": number,
                    "Sprite": filename,
                    "IsFoil": expansion.name == "FullArt" or expansion.name == "EX",
                }

                cards.append(dict(CARD, **card))

                if expansion.name == "FullArt":
                    selector = 1 if set_name == "Basic" else 0

                    if number % 2 == selector:
                        continue

                    frames_dir = ART_ANIMATED_DIR.joinpath(metadata.stem.lower())

                    if not frames_dir.exists():
                        continue

                    multiplier = RARITY_MULTIPLIER.get("FullArtAnimated", 1)

                    name = card["Name"]
                    number = total_animated + total_cards * (multiplier - 1)

                    filename = "".join(
                        c for c in name if c.isalnum() or c.isspace()
                    ).replace(" ", "_")
                    filename = f"{str(number).rjust(total_digits, '0')}_{filename}_FullArtAnimated"

                    card = {
                        "Name": name,
                        "Rarity": "FullArtAnimated",
                        "BorderType": "FullArt",
                        "CardNumber": number,
                        "Sprite": filename,
                        "IsFoil": True,
                    }

                    cards.append(dict(CARD, **card))

                    output_frames_dir = ANIMATED_OUTPUT_DIR.joinpath(
                        f"Nikke_{set_name}"
                    ).joinpath(filename)

                    output_frames_dir.mkdir(parents=True)

                    total_padding = math.ceil(
                        math.log10(len(next(frames_dir.walk())[2]))
                    )

                    for frame in frames_dir.iterdir():
                        img = Image.open(frame).convert("RGBA")
                        background = Image.new("RGBA", img.size, (0, 0, 0, 255))
                        result = Image.alpha_composite(background, img)

                        result.save(
                            output_frames_dir.joinpath(
                                f"{frame.stem.rjust(total_padding, '0')}.png"
                            )
                        )

                    total_animated += 1

        cards.sort(key=lambda card: card["CardNumber"])

        expansion = {
            "Name": f"{set_name} Set",
            "CardExpansion": f"Nikke_{set_name}",
            "Cards": cards,
        }

        items = []

        PACKS = ["Common", "Rare", "Epic", "Legendary"]

        for kind in PACKS:
            material = f"_BoosterPack_{kind}"
            sprite = f"{material}_Icon"

            pack = {
                "Name": f"{set_name} {kind} Packs",
                "CardExpansion": expansion["CardExpansion"],
                "PackType": f"Nikke_{set_name}_{kind}_Pack",
                "ItemType": f"Nikke_{set_name}_{kind}_Pack_Item",
                "Material": material,
                "SpriteName": sprite,
            }

            ORIGINAL_PACKS_DIR.joinpath(
                f"T_CardPack{'' if set.name == 'Tetramon' else set.name}{kind}.png"
            ).copy(output_set.joinpath(f"{material}.png"))

            ORIGINAL_PACKS_DIR.joinpath(
                f"Pack{'' if set.name == 'Tetramon' else set.name}{kind}.png"
            ).copy(output_set.joinpath(f"{sprite}.png"))

            items.append(dict(PACK, **pack))
            items.append(box(set_name, kind))

        bundle = {
            "Name": f"Nikke_{set_name}",
            "BundleId": f"nikke_{set_name.lower()}",
            "Items": items,
            "CardExpansions": [dict(EXPANSION, **expansion)],
        }

        CARDBACK.copy(output_set.joinpath("_Cardback.png"))
        LOGO.copy(output_set.joinpath("_ShopLogo.png"))

        for entry in EXTERNAL_DIR.iterdir():
            if entry.is_file():
                entry.copy(output_set.joinpath(entry.name))
                continue

            if entry.name == set_name:
                for file in entry.iterdir():
                    file.copy(output_set.joinpath(file.name))

        json.dump(
            dict(BUNDLE, **bundle),
            OUTPUT_DIR.joinpath(f"nikke_{set_name.lower()}.json").open("w"),
            indent=2,
        )


def package():
    UNITY_EXE = pathlib.Path(
        r"C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe"
    )
    UNITY_DIR = WORKING_DIR.joinpath("unity")
    EXPANSION_BUILDER = UNITY_DIR.joinpath("ExpansionBuilder")

    shutil.copytree(
        OUTPUT_DIR,
        EXPANSION_BUILDER.joinpath("Assets"),
        dirs_exist_ok=True,
    )

    subprocess.run(
        [
            str(UNITY_EXE),
            "-batchmode",
            "-quit",
            "-projectPath",
            str(EXPANSION_BUILDER),
            "-executeMethod",
            "Builder.Run",
            "-logFile",
            "-",
        ],
        check=True,
        text=True,
    )
