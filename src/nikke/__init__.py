import configparser
import hashlib
import json
import math
import pathlib
import shutil
import subprocess
import urllib.request
import zipfile

from PIL import Image

from nikke import metadata

WORKING_DIR = pathlib.Path.cwd()
ORIGINAL_DIR = WORKING_DIR / "original" / "BepInEx"
TEXTURE_REPLACER_DIR = ORIGINAL_DIR / "plugins" / "TextureReplacer"
TEXTURE_DIR = TEXTURE_REPLACER_DIR / "objects_textures" / "nikke"
AKALIE_DIR = TEXTURE_REPLACER_DIR / "objects_textures" / "from Akalie"
DATA_DIR = TEXTURE_REPLACER_DIR / "objects_data"
MESH_DIR = TEXTURE_REPLACER_DIR / "objects_meshes"
ORIGINAL_CARDS_DIR = ORIGINAL_DIR / "plugins" / "CardConfigurator" / "Configs"
ORIGINAL_PACKS_DIR = TEXTURE_DIR / "packs"
CARDS_DIR = WORKING_DIR / "cards"
ART_STATIC_DIR = WORKING_DIR / "cards" / "assets" / "cardart" / "default"
ART_ANIMATED_DIR = WORKING_DIR / "cards" / "assets" / "animated" / "default" / "ghost"
EXTERNAL_DIR = WORKING_DIR / "external"
TOOLS_DIR = WORKING_DIR / "tools"
OUTPUT_DIR = WORKING_DIR / "output"
ACCESSORIES_DIR = OUTPUT_DIR / "Nikke_Accessories"
FIGURINES_DIR = OUTPUT_DIR / "Nikke_Figurines"
ANIMATED_OUTPUT_DIR = OUTPUT_DIR / "animated"
UNITY_DIR = WORKING_DIR / "unity"
EXPANSION_BUILDER = UNITY_DIR / "ExpansionBuilder"
BUILD_DIR = WORKING_DIR / "build"

CARDBACK = TEXTURE_DIR / "cards" / "T_CardBackMesh.png"
LOGO = TEXTURE_DIR / "misc" / "GameTitle.png"


def extract() -> None:
    zips = list(EXTERNAL_DIR.glob("*.zip"))

    if len(zips) != 1:
        raise SystemExit(
            f"Expected exactly one zip in {EXTERNAL_DIR}, found: {[z.name for z in zips]}"
        )

    zip_file = zips[0]

    with zipfile.ZipFile(zip_file) as archive:
        required = [
            "BepInEx/plugins/ArtExpander/cardart.assets",
            "BepInEx/plugins/ArtExpander/animated.assets",
        ]

        if any(entry not in archive.namelist() for entry in required):
            raise SystemExit(f"{zip_file.name} is missing the ArtExpander assets")

    for directory in (ORIGINAL_DIR.parent, CARDS_DIR):
        shutil.rmtree(directory, ignore_errors=True)

    print(
        f"Extracting {zip_file.name} -> {ORIGINAL_DIR.parent.relative_to(WORKING_DIR)}/"
    )

    with zipfile.ZipFile(zip_file) as archive:
        archive.extractall(ORIGINAL_DIR.parent)

    cli = asset_studio_cli()

    art_expander_dir = ORIGINAL_DIR / "plugins" / "ArtExpander"

    for name in ("cardart", "animated"):
        print(f"Exporting {name}.assets -> {CARDS_DIR.relative_to(WORKING_DIR)}/")

        subprocess.run(
            [
                str(cli),
                "export",
                str(art_expander_dir / f"{name}.assets"),
                "-o",
                str(CARDS_DIR),
                "--types",
                "Texture2D",
                "--group-by",
                "container",
            ],
            check=True,
        )


def asset_studio_cli():
    # AssetStudioCLI 0.17.0, .NET Framework 4.7.2 build (ships with Windows)
    VERSION = "0.17.0"
    ZIP = "AssetStudioCLI.net472.zip"
    FOLDER = "AssetStudioCLI.net472"
    BINARY = "AssetStudioCLI.exe"
    SHA256 = "17834cc9bddf791f7b2c76cbdcc3f2a6a35408b2c466c927d085a5b58da85ff7"
    URL = f"https://github.com/hecrj/AssetStudio/releases/download/{VERSION}/{ZIP}"

    executable_path = TOOLS_DIR / FOLDER / BINARY

    if executable_path.is_file():
        return executable_path

    TOOLS_DIR.mkdir(parents=True, exist_ok=True)

    zip_path = TOOLS_DIR / ZIP

    print(f"Downloading {URL}")

    with urllib.request.urlopen(URL) as response, zip_path.open("wb") as file:
        shutil.copyfileobj(response, file)

    digest = hashlib.sha256(zip_path.read_bytes()).hexdigest()

    if digest != SHA256:
        zip_path.unlink()
        raise RuntimeError(f"SHA256 mismatch for {ZIP}: {digest}")

    with zipfile.ZipFile(zip_path) as archive:
        archive.extractall(TOOLS_DIR)

    zip_path.unlink()

    return executable_path


def generate() -> None:
    SETS = {
        "all_expansions": "Core",
        "destiny": "Destiny",
    }

    CONFIG_SETS = {
        "all_expansions": "Tetramon",
        "destiny": "Destiny",
    }

    RARITIES = {
        "base": "Standard",
        "silver": "Silver",
        "gold": "Gold",
        "firstedition": "FirstEdition",
        "ex": "EX",
        "fullart": "FullArt",
    }

    # Card art names that differ from their config names
    NAME_OVERRIDES = {
        "CrystalA": "EmeraldA",
        "CrystalB": "EmeraldB",
        "CrystalC": "EmeraldC",
        "Mummy": "MummyMan",
    }

    # Legendary pack textures don't follow the regular naming pattern
    PACK_TEXTURES_LEGENDARY = {
        "Core": ("T_CardPackLegnd.png", "PackLegend.png"),
        "Destiny": (
            "T_CardPackDestinyLegend.png",
            "PackDestinyLegendary.png",
        ),
    }

    # Accessories
    accessories = []

    for set_name in SETS.values():
        suffix = "" if set_name == "Core" else "Destiny"

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

    ## Sleeves
    for kind in metadata.SLEEVES:
        name = (
            (DATA_DIR / "accessories" / "sleeves" / f"Card Sleeves ({kind})_NAME.txt")
            .read_text()
            .strip()
        )

        sleeve = metadata.sleeve(name, kind)
        accessories.append(sleeve)

        copy(
            TEXTURE_DIR / "sleeves" / f"T_CardSleeve{kind}.png",
            ACCESSORIES_DIR / f"{sleeve['Material']}.png",
        )

        copy(
            TEXTURE_DIR / "sleeves" / f"Icon_CardSleeve{kind}.png",
            ACCESSORIES_DIR / f"{sleeve['SpriteName']}.png",
        )

    ## Playmats
    names = list((DATA_DIR / "accessories" / "playmats").iterdir())

    for i in range(18):
        name = (
            next(name for name in names if f"_{i + 1}_" in name.stem)
            .read_text()
            .strip()
        )

        playmat = metadata.playmat(name, i)
        accessories.append(playmat)

        copy(
            TEXTURE_DIR / "playmats" / f"T_PlayMat{i + 1}.png",
            ACCESSORIES_DIR / f"{playmat['Material']}.png",
        )

        copy(
            TEXTURE_DIR / "playmats" / f"Icon_Playmat{i + 1}.png",
            ACCESSORIES_DIR / f"{playmat['SpriteName']}.png",
        )

    ## Manga
    for i in range(12):
        name = (
            (DATA_DIR / "accessories" / "manga" / f"Comic Vol {i + 1}_NAME.txt")
            .read_text()
            .strip()
        )
        manga = metadata.manga(name, i)
        accessories.append(manga)

        copy(
            TEXTURE_DIR / "manga" / f"T_Manga_{i + 1}.png",
            ACCESSORIES_DIR / f"{manga['Material']}.png",
        )

        copy(
            TEXTURE_DIR / "manga" / f"Icon_Manga{i + 1}.png",
            ACCESSORIES_DIR / f"{manga['SpriteName']}.png",
        )

    ## Board Games
    BOARD_GAME_ASSETS = {
        "Claim!": ("Claim", "Claim_Mafia"),
        "Mafia Works": ("Mafia", "Claim_Mafia"),
        "Necromansters": ("Necro", "Necromansters"),
        "System Gate 1": ("SystemGate1", "SystemGate"),
        "System Gate 2": ("SystemGate2", "SystemGate"),
    }

    for original in (DATA_DIR / "accessories" / "boardgames").iterdir():
        name = original.read_text().strip()
        board_game = metadata.board_game(name)
        accessories.append(board_game)

        (icon, material) = BOARD_GAME_ASSETS[original.stem.removesuffix("_NAME")]

        copy(
            AKALIE_DIR / f"Icon_Boardgame_Speedrobo_{icon}.png",
            ACCESSORIES_DIR / f"{board_game['SpriteName']}.png",
        )

        copy(
            AKALIE_DIR / f"T_{material}.png",
            ACCESSORIES_DIR / f"{board_game['Material']}.png",
        )

    accessories.sort(key=lambda accessory: accessory["LicenseLevelRequirement"])

    write_json(
        metadata.bundle("Accessories", accessories),
        OUTPUT_DIR / "nikke_accessories.json",
    )

    # Figurines
    figurines = []

    for mesh in (MESH_DIR / "figures").iterdir():
        name = (
            mesh.stem.removeprefix("Figurine_")
            .removesuffix("_Mesh")
            .removesuffix("_Plushie")
        )
        figurine = metadata.figurine(name)
        figurines.append(figurine)

        copy(
            mesh,
            FIGURINES_DIR / f"{figurine['Mesh']}.obj",
        )

        for prefix in ["", "Toy_"]:
            for suffix in ["", "Plushie"]:
                icon = (
                    TEXTURE_DIR
                    / "figures"
                    / f"Icon_{prefix}{name.replace('PigB', 'PiggyB')}{suffix}.png"
                )

                if icon.exists():
                    break
            else:
                continue

            break

        copy(
            icon,
            FIGURINES_DIR / f"{figurine['SpriteName']}.png",
        )

        for suffix in ["", "Plushie"]:
            material = (
                TEXTURE_DIR
                / "figures"
                / f"T_{name.replace('PigB', 'PiggyB')}{suffix}.png"
            )

            if material.exists():
                break

        copy(
            material,
            FIGURINES_DIR / f"{figurine['Material']}.png",
        )

    figurines.sort(key=lambda figurine: figurine["LicenseLevelRequirement"])

    write_json(
        metadata.bundle("Figurines", figurines),
        OUTPUT_DIR / "nikke_figurines.json",
    )

    # Expansions
    for set in ART_STATIC_DIR.iterdir():
        set_name = SETS.get(set.name)

        if set_name is None:
            continue

        config_set = CONFIG_SETS[set.name]
        total_cards = len(list((set / "base").glob("*.png")))
        total_animated = 0
        animated_selector = 1 if set_name == "Core" else 0
        ghost_suffix = "" if set_name == "Core" else "Destiny"

        output_set = OUTPUT_DIR / f"Nikke_{set_name}"
        cards = []

        for expansion in set.iterdir():
            if not expansion.is_dir():
                continue

            rarity = RARITIES.get(expansion.name)

            if rarity is None:
                continue

            for card_art in expansion.iterdir():
                config_name = NAME_OVERRIDES.get(card_art.stem, card_art.stem)
                ini = ORIGINAL_CARDS_DIR / config_set / "Default" / f"{config_name}.ini"

                if not ini.is_file():
                    print(f"[{rarity} - {card_art.name}] Config not found. Skipping...")
                    continue

                config = configparser.ConfigParser()
                config.read(ini)

                kind = config[config_name]["Rarity"]
                number = int(
                    config[config_name]["Number"]
                ) + total_cards * metadata.RARITIES.index(rarity)

                ini_override = ORIGINAL_CARDS_DIR / config_set / rarity / ini.name

                if ini_override.is_file():
                    config.read(ini_override)

                name = config[config_name]["Name"]
                card = metadata.card(name, number, rarity, kind)
                cards.append(card)

                copy(card_art, output_set / f"{card['Sprite']}.png")

                if rarity == "FullArt":
                    if number % 2 == animated_selector:
                        continue

                    frames_dir = ART_ANIMATED_DIR / card_art.stem.lower()

                    if not frames_dir.exists():
                        continue

                    total_animated += 1

                    ini_override = (
                        ORIGINAL_CARDS_DIR / f"Ghost{ghost_suffix}" / ini.name
                    )

                    if ini_override.is_file():
                        config.read(ini_override)

                    name = config[config_name]["Name"]
                    number = total_animated + total_cards * (len(metadata.RARITIES) - 1)
                    card = metadata.card(name, number, "FullArtAnimated", kind)
                    cards.append(card)

                    frames = sorted(frames_dir.iterdir())
                    copy(frames[0], output_set / f"{card['Sprite']}.png")

                    output_frames_dir = (
                        ANIMATED_OUTPUT_DIR / f"Nikke_{set_name}" / card["Sprite"]
                    )

                    if output_frames_dir.exists():
                        continue

                    print(output_frames_dir.relative_to(WORKING_DIR))
                    output_frames_dir.mkdir(parents=True)

                    total_padding = math.ceil(math.log10(len(frames)))

                    for frame in frames:
                        img = Image.open(frame).convert("RGBA")
                        background = Image.new("RGBA", img.size, (0, 0, 0, 255))
                        result = Image.alpha_composite(background, img)

                        result.save(
                            output_frames_dir
                            / f"{frame.stem.rjust(total_padding, '0')}.png"
                        )

        cards.sort(key=lambda card: card["CardNumber"])

        items = []

        TIERS = ["Common", "Rare", "Epic", "Legendary"]

        for tier in TIERS:
            pack = metadata.pack(set_name, tier)
            items.append(pack)

            box = metadata.box(set_name, tier)
            items.append(box)

            pack_suffix = "" if set_name == "Core" else "Destiny"
            box_prefix = "" if set_name == "Core" else "Destiny_"
            box_suffix = "" if set_name == "Core" else "_Destiny"
            box_tier = "Legend" if tier == "Legendary" else tier
            box_tier = "" if tier == "Common" else box_tier

            if tier == "Legendary":
                pack_material, pack_sprite = PACK_TEXTURES_LEGENDARY[set_name]
            else:
                pack_material = f"T_CardPack{pack_suffix}{tier}.png"
                pack_sprite = f"Pack{pack_suffix}{tier}.png"

            copy(
                ORIGINAL_PACKS_DIR / pack_material,
                output_set / f"{pack['Material']}.png",
            )

            copy(
                ORIGINAL_PACKS_DIR / pack_sprite,
                output_set / f"{pack['SpriteName']}.png",
            )

            copy(
                TEXTURE_DIR / "boxes" / f"T_CardBox{box_suffix}.png",
                output_set / f"{box['Material']}.png",
            )

            copy(
                TEXTURE_DIR / "boxes" / f"{box_prefix}{box_tier}CardBox.png",
                output_set / f"{box['SpriteName']}.png",
            )

        copy(CARDBACK, output_set / "_Cardback.png")

        for entry in (EXTERNAL_DIR / "Expansion").iterdir():
            if entry.is_file():
                copy(entry, output_set / entry.name)

        expansion = metadata.expansion(set_name, cards)

        for i, material in enumerate(expansion["PlayCardMaterials"]):
            n = i if set_name == "Core" else i + 4
            n = 5 if i + 1 == len(expansion["PlayCardMaterials"]) else n
            n = "" if n == 0 else str(n + 1)

            copy(
                TEXTURE_DIR / "cards" / f"T_3dCardModel{n}.png",
                output_set / f"{material}.png",
            )

        write_json(
            metadata.bundle(set_name, items, [expansion]),
            OUTPUT_DIR / f"nikke_{set_name.lower()}.json",
        )

    for bundle in OUTPUT_DIR.iterdir():
        if not bundle.is_dir() or "Nikke_" not in bundle.name:
            continue

        copy(LOGO, bundle / "_ShopLogo.png")

        for entry in (EXTERNAL_DIR / "Bundle").iterdir():
            if entry.is_file():
                copy(entry, bundle / entry.name)


def bundle():
    UNITY_EXE = pathlib.Path(
        r"C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe"
    )
    ASSETS_DIR = EXPANSION_BUILDER / "Assets"

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

            print(file.relative_to(ASSETS_DIR))
            file.unlink()

    shutil.copytree(
        OUTPUT_DIR,
        ASSETS_DIR,
        dirs_exist_ok=True,
    )

    bundle = subprocess.Popen(
        [
            str(UNITY_EXE),
            "-batchmode",
            "-nographics",
            "-quit",
            "-projectPath",
            str(EXPANSION_BUILDER),
            "-executeMethod",
            "Builder.Run",
            "-logFile",
            "-",
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )

    for line in bundle.stdout or []:
        print(line, end="", flush=True)

    bundle.wait()

    if bundle.returncode != 0:
        raise subprocess.CalledProcessError(bundle.returncode, bundle.args)


def package():
    SHOP_ICONS = EXTERNAL_DIR / "Shop"

    shutil.rmtree(BUILD_DIR, ignore_errors=True)

    def mod(name):
        class Mod:
            def __init__(self, name):
                self.name = name

            def __enter__(self):
                return (
                    BUILD_DIR
                    / f"Nikke - {self.name.capitalize()}"
                    / "BepInEx"
                    / "plugins"
                )

            def __exit__(self, _exc_type, _exc_value, _traceback):
                pass

        return Mod(name)

    for entry in OUTPUT_DIR.iterdir():
        if entry.suffix != ".json":
            continue

        with mod(entry.stem.removeprefix("nikke_")) as plugins:
            copy(
                SHOP_ICONS,
                plugins / "Phone - Overhaul" / "App Images" / "Nikke",
            )

            plugin = plugins / "Nikke" / f"{entry.stem}_prefabloader"
            bundle = EXPANSION_BUILDER / "AssetBundles" / entry.stem
            bundle_animated = bundle.with_name(bundle.name + "_animated")

            copy(bundle, plugin / entry.stem)
            copy(entry, plugin / entry.name)

            epl_animator_dir = plugins / "EPLCardAnimator" / "animated"

            if bundle_animated.exists():
                copy(
                    bundle_animated,
                    (epl_animator_dir / bundle_animated.name).with_suffix(".assets"),
                )

    with mod("posters") as plugins:
        copy(
            TEXTURE_DIR / "posters",
            plugins
            / "TextureReplacer"
            / "objects_textures"
            / "Nikke_Posters"
            / "posters",
        )

    with mod("theme") as plugins:
        output_dir = plugins / "TextureReplacer" / "objects_textures" / "Nikke_Theme"

        TEXTURES = [
            "misc",
            "furniture/machines",
            "shop/T_PaperBagAlbedoClosed.png",
            "shop/T_PaperBagAlbedoOpen.png",
        ]

        for texture in TEXTURES:
            copy(TEXTURE_DIR / texture, output_dir / texture)

        for texture in (TEXTURE_DIR / "cards").iterdir():
            if "CardModel" not in texture.name:
                continue

            copy(texture, output_dir / "cards" / texture.name)

        for texture in (EXTERNAL_DIR / "Theme").iterdir():
            copy(texture, output_dir / texture.name, overwrite=True)

        copy(
            EXTERNAL_DIR / "Billboards" / "Nero.png",
            output_dir / "misc" / "Billboard.png",
            overwrite=True,
        )


def install():
    GAME_DIR = pathlib.Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\TCG Card Shop Simulator"
    )

    for mod in BUILD_DIR.iterdir():
        copy(mod / "BepInEx", GAME_DIR / "BepInEx", overwrite=True)


def write_json(data: dict, file: pathlib.Path):
    print(file.relative_to(WORKING_DIR))

    with file.open("w", newline="\n", encoding="utf-8") as json_file:
        json.dump(data, json_file, indent=2)


def copy(source: pathlib.Path, dest: pathlib.Path, overwrite=False):
    if dest.exists():
        if not overwrite:
            return

        if source.is_dir():
            for entry in source.iterdir():
                copy(entry, dest / entry.name, overwrite=True)

            return

    if dest.is_relative_to(WORKING_DIR):
        print(dest.relative_to(WORKING_DIR))
    else:
        print(dest)

    dest.parent.mkdir(parents=True, exist_ok=True)
    source.copy(dest)
