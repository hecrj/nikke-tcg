import configparser
import json
import math
import pathlib
import shutil
import subprocess

from PIL import Image
from nikke import metadata


WORKING_DIR = pathlib.Path.cwd()
ORIGINAL_DIR = WORKING_DIR.joinpath("original/BepInEx")
TEXTURE_DIR = ORIGINAL_DIR.joinpath("plugins/TextureReplacer/objects_textures/nikke")
ORIGINAL_CARDS_DIR = ORIGINAL_DIR.joinpath("plugins/CardConfigurator/Configs")
ORIGINAL_PACKS_DIR = TEXTURE_DIR.joinpath("packs")
ART_STATIC_DIR = WORKING_DIR.joinpath("cards/Texture2D/assets/cardart/default")
ART_ANIMATED_DIR = WORKING_DIR.joinpath("cards/Texture2D/assets/animated/default/ghost")
EXTERNAL_DIR = WORKING_DIR.joinpath("external")
OUTPUT_DIR = WORKING_DIR.joinpath("output")
ACCESSORIES_DIR = OUTPUT_DIR / "Nikke_Accessories"
ANIMATED_OUTPUT_DIR = OUTPUT_DIR.joinpath("animated")
UNITY_DIR = WORKING_DIR.joinpath("unity")
EXPANSION_BUILDER = UNITY_DIR.joinpath("ExpansionBuilder")

CARDBACK = TEXTURE_DIR.joinpath("cards/T_CardBackMesh.png")
LOGO = TEXTURE_DIR.joinpath("misc/GameTitle.png")


def generate() -> None:
    OUTPUT_DIR.mkdir(exist_ok=True)

    SETS = {
        "Tetramon": "Basic",
        "Destiny": "Destiny",
    }

    # Accessories
    ACCESSORIES_DIR.mkdir(exist_ok=True)
    accessories = []

    for set_name in SETS.values():
        suffix = "" if set_name == "Basic" else "Destiny"

        ## Battle Decks
        for element in metadata.ELEMENTS:
            deck = metadata.deck(set_name, element)
            accessories.append(deck)

            copy(
                TEXTURE_DIR / "boxes" / f"T_PreconDeck{element}{suffix}.png",
                ACCESSORIES_DIR / f"{deck['Material']}.png",
            )

            copy(
                TEXTURE_DIR / "boxes" / f"Icon_Precon{element}{suffix}.png",
                ACCESSORIES_DIR / f"{deck['SpriteName']}.png",
            )

    ## Deck Boxes
    for i in range(4):
        deck_box = metadata.deck_box(i)
        accessories.append(deck_box)

        suffix = "" if i == 0 else str(i + 1)

        copy(
            TEXTURE_DIR / "boxes" / f"T_DeckBox{suffix}.png",
            ACCESSORIES_DIR / f"{deck_box['Material']}.png",
        )

        copy(
            TEXTURE_DIR / "boxes" / f"Icon_DeckBox{i + 1}.png",
            ACCESSORIES_DIR / f"{deck_box['SpriteName']}.png",
        )

    write_json(
        metadata.bundle("Accessories", accessories),
        OUTPUT_DIR / "nikke_accessories.json",
    )

    # Expansions
    for set in ART_STATIC_DIR.iterdir():
        set_name = SETS.get(set.name)

        if set_name is None:
            continue

        total_cards = len(next(set.joinpath("base").walk())[2])
        total_animated = 0

        output_set = OUTPUT_DIR.joinpath(f"Nikke_{set_name}")
        output_set.mkdir(exist_ok=True)

        cards = []

        for expansion in set.iterdir():
            if not expansion.is_dir():
                continue

            for card_art in expansion.iterdir():
                ini = (
                    ORIGINAL_CARDS_DIR.joinpath(set.name)
                    .joinpath("default")
                    .joinpath(f"{card_art.stem}.ini")
                )

                if not ini.is_file():
                    print(
                        f"[{expansion.name} - {card_art.name}] Config not found. Skipping..."
                    )
                    continue

                config = configparser.ConfigParser()
                config.read(ini)

                name = config[card_art.stem]["Name"]
                number = int(
                    config[card_art.stem]["Number"]
                ) + total_cards * metadata.RARITIES.index(expansion.name)

                card = metadata.card(name, number, expansion.name)
                cards.append(card)

                copy(card_art, output_set.joinpath(f"{card['Sprite']}.png"))

                if expansion.name == "FullArt":
                    selector = 1 if set_name == "Basic" else 0

                    if number % 2 == selector:
                        continue

                    frames_dir = ART_ANIMATED_DIR.joinpath(card_art.stem.lower())

                    if not frames_dir.exists():
                        continue

                    total_animated += 1

                    number = total_animated + total_cards * (len(metadata.RARITIES) - 1)
                    card = metadata.card(name, number, "FullArtAnimated")
                    cards.append(card)

                    output_frames_dir = ANIMATED_OUTPUT_DIR.joinpath(
                        f"Nikke_{set_name}"
                    ).joinpath(card["Sprite"])

                    if output_frames_dir.exists():
                        continue

                    print(output_frames_dir.relative_to(OUTPUT_DIR))
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

        cards.sort(key=lambda card: card["CardNumber"])

        items = []

        TIERS = ["Common", "Rare", "Epic", "Legendary"]

        for tier in TIERS:
            pack = metadata.pack(set_name, tier)
            items.append(pack)

            box = metadata.box(set_name, tier)
            items.append(box)

            prefix = "" if set_name == "Basic" else "Destiny_"
            original_kind = "Legend" if tier == "Legendary" else tier
            original_kind = "" if tier == "Common" else original_kind

            copy(
                ORIGINAL_PACKS_DIR.joinpath(
                    f"T_CardPack{'' if set.name == 'Tetramon' else set.name}{tier}.png"
                ),
                output_set.joinpath(f"{pack['Material']}.png"),
            )

            copy(
                ORIGINAL_PACKS_DIR.joinpath(
                    f"Pack{'' if set.name == 'Tetramon' else set.name}{tier}.png"
                ),
                output_set.joinpath(f"{pack['SpriteName']}.png"),
            )

            copy(
                EXTERNAL_DIR.joinpath(set_name).joinpath(f"{box['Material']}.png"),
                output_set.joinpath(f"{box['Material']}.png"),
            )

            copy(
                TEXTURE_DIR.joinpath("boxes").joinpath(
                    f"{prefix}{original_kind}Cardbox.png"
                ),
                output_set.joinpath(f"{box['SpriteName']}.png"),
            )

        copy(CARDBACK, output_set.joinpath("_Cardback.png"))
        copy(LOGO, output_set.joinpath("_ShopLogo.png"))

        for entry in EXTERNAL_DIR.iterdir():
            if entry.is_file():
                copy(entry, output_set.joinpath(entry.name))
                continue

        expansion = metadata.expansion(set_name, cards)
        write_json(
            metadata.bundle(set_name, items, [expansion]),
            OUTPUT_DIR.joinpath(f"nikke_{set_name.lower()}.json"),
        )


def bundle():
    UNITY_EXE = pathlib.Path(
        r"C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe"
    )
    ASSETS_DIR = EXPANSION_BUILDER.joinpath("Assets")

    for path, dirs, files in ASSETS_DIR.walk(top_down=False):
        if "Nikke" not in str(path):
            continue

        for dir in dirs:
            dir = path / dir

            if not any(dir.iterdir()):
                dir.rmdir()

        for file in files:
            file = path / file
            output_file = OUTPUT_DIR / file.with_suffix("").relative_to(ASSETS_DIR)

            if file.suffix == ".meta" and output_file.exists():
                continue

            file.unlink()

    shutil.copytree(
        OUTPUT_DIR,
        ASSETS_DIR,
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


def package():
    BUILD_DIR = WORKING_DIR / "build"
    PLUGINS_DIR = BUILD_DIR / "BepInEx" / "plugins"
    SHOP_DIR = PLUGINS_DIR / "Phone - Overhaul" / "App Images" / "Nikke"
    SHOP_ICONS = EXTERNAL_DIR / "Shop"
    EPL_ANIMATOR_DIR = PLUGINS_DIR / "EPLCardAnimator" / "animated"

    shutil.rmtree(BUILD_DIR, ignore_errors=True)

    SHOP_DIR.parent.mkdir(parents=True)
    SHOP_ICONS.copy(SHOP_DIR)

    for entry in OUTPUT_DIR.iterdir():
        if entry.suffix != ".json":
            continue

        print(entry.stem)

        plugin = PLUGINS_DIR / "Nikke" / f"{entry.stem}_prefabloader"
        bundle = EXPANSION_BUILDER / "AssetBundles" / entry.stem
        bundle_animated = bundle.with_name(bundle.name + "_animated")

        plugin.mkdir(parents=True)
        bundle.copy(plugin / entry.stem)
        entry.copy(plugin / entry.name)

        if bundle_animated.exists():
            if not EPL_ANIMATOR_DIR.exists():
                EPL_ANIMATOR_DIR.mkdir(parents=True)

            bundle_animated.copy(
                (EPL_ANIMATOR_DIR / bundle_animated.name).with_suffix(".assets")
            )


def write_json(data: dict, file: pathlib.Path):
    print(file.relative_to(OUTPUT_DIR))

    json.dump(
        data,
        file.open("w"),
        indent=2,
    )


def copy(source: pathlib.Path, dest: pathlib.Path):
    if not dest.exists():
        print(dest.relative_to(OUTPUT_DIR))
        source.copy(dest)
