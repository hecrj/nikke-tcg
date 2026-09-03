import configparser
import json
import math
import pathlib
import shutil
import threading
import zipfile

import UnityPy
from PIL import Image, PngImagePlugin

from nikke import bundler, metadata

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
OUTPUT_DIR = WORKING_DIR / "output"
ACCESSORIES_DIR = OUTPUT_DIR / "Nikke_Accessories"
FIGURINES_DIR = OUTPUT_DIR / "Nikke_Figurines"
ANIMATED_OUTPUT_DIR = OUTPUT_DIR / "animated"
BUNDLES_DIR = OUTPUT_DIR / "AssetBundles"
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

    art_expander_dir = ORIGINAL_DIR / "plugins" / "ArtExpander"

    for name in ("cardart", "animated"):
        print(f"Exporting {name}.assets -> {CARDS_DIR.relative_to(WORKING_DIR)}/")
        # Static card art is bundled as-is, so keep the source texture verbatim (see
        # export_textures). Animated frames get a black background composited in later, so
        # there is nothing to preserve.
        export_textures(
            art_expander_dir / f"{name}.assets",
            preserve_raw=name == "cardart",
        )


def export_textures(assets: pathlib.Path, preserve_raw: bool = False) -> None:
    # Replaces AssetStudioCLI `export --types Texture2D --group-by container`: write every
    # Texture2D as a PNG under CARDS_DIR, in the folder given by its AssetBundle container
    # path and named after the asset (m_Name), e.g.
    #   assets/cardart/default/all_expansions/gold/shellyd.png + name "ShellyD"
    #     -> cards/assets/cardart/default/all_expansions/gold/ShellyD.png
    # UnityPy decodes any GPU format to a correctly-oriented image (Unity stores textures
    # bottom-up; .image already flips them).
    #
    # With preserve_raw, the source texture's encoded bytes are also stashed in a private PNG
    # chunk so `bundle` can embed them untouched instead of decoding and re-encoding (which
    # would compress an already-compressed texture a second time, losing quality).
    env = UnityPy.load(str(assets))

    textures = [o for o in env.objects if o.type.name == "Texture2D" and o.container]

    # Decoding (GPU format -> RGBA) and PNG encoding are C code that releases the GIL, so
    # they parallelize across threads. The pixels live in a shared .resS stream, though, and
    # every Texture2D reads it through one reader whose cursor is shared state -- so reading
    # (obj.read + get_image_data) must be serialized. We do that under `read_lock`, inline the
    # bytes so the later decode no longer touches the stream, then decode/encode unlocked.
    read_lock = threading.Lock()

    def export(obj):
        with read_lock:
            data = obj.read()
            raw = bytes(data.get_image_data())
            data.image_data = raw
            data.m_StreamData.path = ""
            data.m_StreamData.size = 0

        output = (CARDS_DIR / obj.container).with_name(f"{data.m_Name}.png")
        output.parent.mkdir(parents=True, exist_ok=True)

        info = None
        if preserve_raw:
            info = PngImagePlugin.PngInfo()
            info.add(
                bundler.RAW_TEXTURE_CHUNK,
                bundler.pack_raw_texture(
                    int(data.m_TextureFormat), data.m_Width, data.m_Height, raw
                ),
            )

        data.image.save(output, pnginfo=info)

    bundler.parallel(textures, export, assets.stem)


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
                name = name.replace("PigB", "PiggyB").replace("StarFish", "Starfish")
                icon = TEXTURE_DIR / "figures" / f"Icon_{prefix}{name}{suffix}.png"

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
    # Compositing every animation frame over a black background is independent per frame, so
    # we collect the jobs here and run them in one parallel pass once all cards are known.
    animated_jobs: list[tuple[pathlib.Path, pathlib.Path]] = []

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

            for card_art in sorted(expansion.iterdir()):
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
                        output = (
                            output_frames_dir
                            / f"{frame.stem.rjust(total_padding, '0')}.png"
                        )
                        animated_jobs.append((frame, output))

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

    def composite(job):
        frame, output = job
        image = Image.open(frame).convert("RGBA")
        background = Image.new("RGBA", image.size, (0, 0, 0, 255))
        Image.alpha_composite(background, image).save(output)

    bundler.parallel(animated_jobs, composite, "animated")

    for bundle in OUTPUT_DIR.iterdir():
        if not bundle.is_dir() or "Nikke_" not in bundle.name:
            continue

        copy(LOGO, bundle / "_ShopLogo.png")

        for entry in (EXTERNAL_DIR / "Bundle").iterdir():
            if entry.is_file():
                copy(entry, bundle / entry.name)


def bundle():
    # Build the per-set AssetBundles directly with UnityPy (see nikke.bundler) instead of
    # driving the Unity editor. Bundle names are the set-directory name lowercased (animated
    # sets get an `_animated` suffix), and the output lands where package() reads it from.
    shutil.rmtree(BUNDLES_DIR, ignore_errors=True)

    for set_dir in sorted(OUTPUT_DIR.iterdir()):
        if set_dir.is_dir() and "Nikke_" in set_dir.name:
            name = set_dir.name.lower()
            bundler.build(set_dir, name, BUNDLES_DIR / name, OUTPUT_DIR)

    if ANIMATED_OUTPUT_DIR.is_dir():
        for set_dir in sorted(ANIMATED_OUTPUT_DIR.iterdir()):
            if set_dir.is_dir():
                name = f"{set_dir.name.lower()}_animated"
                bundler.build(set_dir, name, BUNDLES_DIR / name, OUTPUT_DIR)


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
            bundle = BUNDLES_DIR / entry.stem
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
