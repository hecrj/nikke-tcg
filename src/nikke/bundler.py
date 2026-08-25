"""Build Unity AssetBundles without Unity.

The pipeline used to drive the Unity editor (an ExpansionBuilder project) to import each
PNG as a BC7 Texture2D (+ Sprite), create a Standard-shader Material for every `*_Material`
texture, and pack them into a per-set AssetBundle. This reproduces that output with UnityPy
so the pipeline no longer needs Unity (and its licensing, Windows-only, DLL and caching
problems).

The bundles are consumed by TCG Card Shop Simulator (Unity 2021.3.38f1). We can't author
a byte-identical file, but the game resolves assets by name/container path, so a
structurally faithful bundle loads the same. UnityPy also can't author a SerializedFile
from nothing, so we build on top of `seed.bundle` -- a ~47 KB distillation of a real
Unity-built bundle holding the container scaffolding, the embedded built-in Standard
shader, and one prototype of each object type. We clone those prototypes (so Unity's
field defaults come along for free) and override only what matters: name, image, size,
and the PPtr wiring between them.
"""

from __future__ import annotations

import dataclasses
import importlib.resources
import pathlib
import struct
import sys

import UnityPy
from PIL import Image
from UnityPy.enums import TextureFormat
from UnityPy.files.ObjectReader import ObjectReader
from UnityPy.files.SerializedFile import SerializedFile

# UnityPy cannot author a SerializedFile from nothing, so we ship a ~47 KB seed bundle:
# the irreducible container scaffolding plus one prototype of each object we build
# (Texture2D/Sprite/Material) and the built-in Standard shader materials bind to. It was
# distilled once from a real Unity-built bundle; see scripts/distill_seed.py.
SEED = importlib.resources.files(__package__) / "seed.bundle"

# GPU format Builder.cs bakes with (TextureImporterFormat.BC7, best quality). BC7 is
# 1 byte/px; UnityPy encodes it via etcpak.compress_bc7.
TEXTURE_FORMAT = TextureFormat.BC7

# Sprite import default Unity used (pixels-per-unit 100); centre pivot / extrude come
# from the prototype unchanged.
SPRITE_PIXELS_TO_UNITS = 100.0

# Builder.GenerateMaterials created a Standard material for every `*_Material` texture.
# Those become Texture2D + Material; every other PNG becomes Texture2D + Sprite.
MATERIAL_SUFFIX = "_Material"

# Static card art comes from an already-encoded source texture that `bundle` only relocates.
# To avoid a lossy decode->PNG->re-encode round-trip, `extract` stashes the source texture
# (format + dimensions + raw bytes) verbatim in this private PNG chunk, and `build` embeds it
# untouched. Private/ancillary/safe-to-copy PNG chunk name, so it survives a byte copy.
RAW_TEXTURE_CHUNK = b"nkTx"


def pack_raw_texture(
    texture_format: int, width: int, height: int, data: bytes
) -> bytes:
    return struct.pack("<iii", int(texture_format), width, height) + data


def read_raw_texture(png: pathlib.Path):
    """Return (format, width, height, data) from a PNG's raw-texture chunk, or None."""
    blob = png.read_bytes()
    if blob[:8] != b"\x89PNG\r\n\x1a\n":
        return None

    offset = 8
    while offset + 12 <= len(blob):
        (length,) = struct.unpack(">I", blob[offset : offset + 4])
        if blob[offset + 4 : offset + 8] == RAW_TEXTURE_CHUNK:
            payload = blob[offset + 8 : offset + 8 + length]
            texture_format, width, height = struct.unpack("<iii", payload[:12])
            return texture_format, width, height, payload[12:]
        offset += 12 + length
    return None


@dataclasses.dataclass
class Prototypes:
    """Known-good objects lifted from the reference bundle, used as clone templates."""

    texture: ObjectReader
    sprite: ObjectReader
    material: ObjectReader  # a Standard-shader material (`defaultMat`)
    shader_path_id: int  # the embedded built-in Standard shader


class Bundle:
    """A single AssetBundle under construction, templated from the reference bundle."""

    def __init__(self, name: str):
        self._env = UnityPy.load(SEED.read_bytes())
        self._sf: SerializedFile = next(
            f for f in self._env.file.files.values() if isinstance(f, SerializedFile)
        )
        self._protos = self._extract_prototypes()
        self._manifest = self._find(lambda o: o.type.name == "AssetBundle")

        # Assign PathIDs that can't collide with the objects we keep (shader + manifest).
        self._next_path_id = max(self._sf.objects) + 1

        # Start from a clean slate: keep only the manifest and the Standard shader,
        # drop every Pokémon-specific object. We re-add our own below.
        keep = {self._manifest.path_id, self._protos.shader_path_id}
        self._sf.objects = {
            path_id: obj for path_id, obj in self._sf.objects.items() if path_id in keep
        }

        # Drop the reference's `.resS` resource stream (~all its texture pixel data lives
        # there via m_StreamData). Our textures store their data inline via set_image, so
        # nothing references the stream and carrying it would bloat the bundle to ~240 MB.
        self._env.file.files = {
            name: f
            for name, f in self._env.file.files.items()
            if isinstance(f, SerializedFile)
        }

        self._name = name
        self._container: list[tuple[str, ObjectReader, list[ObjectReader]]] = []

    def _find(self, predicate):
        return next(o for o in self._sf.objects.values() if predicate(o))

    def _extract_prototypes(self) -> Prototypes:
        texture = self._find(lambda o: o.type.name == "Texture2D")
        sprite = self._find(lambda o: o.type.name == "Sprite")
        # `defaultMat` is the one reference material bound to the built-in Standard shader
        # (the others use a third-party shader Nikke doesn't use).
        material = self._find(
            lambda o: o.type.name == "Material" and o.read().m_Name == "defaultMat"
        )
        shader_path_id = material.read().m_Shader.m_PathID
        return Prototypes(texture, sprite, material, shader_path_id)

    def _clone(self, proto: ObjectReader) -> ObjectReader:
        path_id = self._next_path_id
        self._next_path_id += 1

        clone = ObjectReader(
            assets_file=self._sf,
            reader=proto.reader,
            path_id=path_id,
            type_id=proto.type_id,
            serialized_type=proto.serialized_type,
            class_id=proto.class_id,
            type=proto.type,
            byte_start=proto.byte_start,
            byte_size=proto.byte_size,
            is_destroyed=proto.is_destroyed,
            is_stripped=proto.is_stripped,
        )
        clone.set_raw_data(proto.get_raw_data())
        self._sf.objects[path_id] = clone
        return clone

    def _pptr(self, obj: ObjectReader):
        """A same-file PPtr (m_FileID = 0) pointing at one of our objects."""
        from UnityPy.classes import PPtr

        return PPtr(m_FileID=0, m_PathID=obj.path_id, assetsfile=self._sf)

    def add_texture(self, name: str, image: Image.Image) -> ObjectReader:
        obj = self._clone(self._protos.texture)
        data = obj.read()
        data.m_Name = name
        # BC7 (like all block formats) needs both dimensions to be multiples of 4. Unity
        # falls back to uncompressed RGBA32 otherwise -- as the source assets confirm -- so
        # match that: forcing BC7 would pad the block grid and store a texture the game's
        # runtime never sees Unity produce (and lose quality, since RGBA32 is lossless).
        width, height = image.size
        block_compressible = width % 4 == 0 and height % 4 == 0
        fmt = TEXTURE_FORMAT if block_compressible else TextureFormat.RGBA32
        # set_image re-encodes and fixes m_Width/Height/CompleteImageSize/format.
        data.set_image(image, target_format=fmt)
        data.m_MipCount = 1
        data.m_IsReadable = False
        data.m_TextureSettings.m_FilterMode = 1  # Bilinear
        data.save()
        return obj

    def add_raw_texture(
        self, name: str, texture_format: int, width: int, height: int, data_bytes: bytes
    ) -> ObjectReader:
        """Embed an already-encoded texture verbatim (no decode/re-encode, so lossless)."""
        obj = self._clone(self._protos.texture)
        data = obj.read()
        data.m_Name = name
        data.m_Width = width
        data.m_Height = height
        data.image_data = data_bytes
        data.m_CompleteImageSize = len(data_bytes)
        data.m_TextureFormat = TextureFormat(texture_format)
        data.m_MipCount = 1
        data.m_IsReadable = False
        data.m_TextureSettings.m_FilterMode = 1  # Bilinear
        if data.m_StreamData is not None:
            data.m_StreamData.path = ""
            data.m_StreamData.offset = 0
            data.m_StreamData.size = 0
        data.save()
        return obj

    def add_sprite(self, name: str, texture: ObjectReader, size: tuple[int, int]):
        obj = self._clone(self._protos.sprite)
        data = obj.read()
        width, height = size
        data.m_Name = name
        data.m_Rect.width = data.m_RD.textureRect.width = float(width)
        data.m_Rect.height = data.m_RD.textureRect.height = float(height)
        data.m_PixelsToUnits = SPRITE_PIXELS_TO_UNITS
        data.m_RD.texture = self._pptr(texture)
        data.save()
        return obj

    def add_material(self, name: str, texture: ObjectReader):
        obj = self._clone(self._protos.material)
        data = obj.read()
        data.m_Name = name
        for key, tex_env in data.m_SavedProperties.m_TexEnvs:
            if key == "_MainTex":
                tex_env.m_Texture = self._pptr(texture)
        data.save()
        return obj

    @property
    def shader(self) -> ObjectReader:
        """The embedded Standard shader materials bind to (kept from the seed)."""
        return self._sf.objects[self._protos.shader_path_id]

    def register(self, container_path: str, primary: ObjectReader, preload: list):
        """Expose `primary` at `container_path`, preloading it and its dependencies."""
        self._container.append((container_path.lower(), primary, preload))

    def save(self, output: pathlib.Path):
        from UnityPy.classes import AssetInfo, PPtr

        manifest = self._manifest.read()
        manifest.m_Name = self._name
        manifest.m_AssetBundleName = self._name
        manifest.m_Dependencies = []

        preload_table: list = []
        container: list = []
        for path, primary, preload in self._container:
            index = len(preload_table)
            preload_table.extend(
                PPtr(m_FileID=0, m_PathID=o.path_id, assetsfile=self._sf)
                for o in preload
            )
            info = AssetInfo(
                preloadIndex=index,
                preloadSize=len(preload),
                asset=PPtr(m_FileID=0, m_PathID=primary.path_id, assetsfile=self._sf),
            )
            container.append((path, info))

        manifest.m_PreloadTable = preload_table
        manifest.m_Container = container
        manifest.save()
        self._sf.mark_changed()

        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(self._env.file.save(packer="lz4"))


def progress(items: list, label: str):
    """Yield each of `items`, reporting `label: done/total (pct%)` as it goes.

    In a terminal the line updates in place via a carriage return; CI logs don't collapse
    those, so there it prints once per percent instead of once per item.
    """
    total = len(items)
    if not total:
        return

    live = sys.stdout.isatty()
    percent = -1

    for done, item in enumerate(items, start=1):
        yield item

        pct = done * 100 // total
        if live:
            print(f"\r  {label}: {done}/{total} ({pct}%)", end="", flush=True)
        elif pct != percent:
            percent = pct
            print(f"  {label}: {done}/{total} ({pct}%)")

    if live:
        print()


def build(
    assets_dir: pathlib.Path, name: str, output: pathlib.Path, root: pathlib.Path
) -> None:
    """Build one AssetBundle named `name` from the PNGs under `assets_dir`.

    Mirrors Unity's Builder.Run: `*_Material` PNGs become Texture2D + Standard Material,
    every other PNG becomes Texture2D + Sprite. Container paths are `assets/` + the path
    relative to `root` (the Assets-equivalent), lowercased -- exactly what Unity emitted
    when `root` was copied into the project's Assets/ -- so the game resolves assets the same.
    """
    bundle = Bundle(name)
    assets = sorted(p for p in assets_dir.rglob("*") if p.is_file())
    textures = [p for p in assets if p.suffix == ".png"]
    meshes = [p for p in assets if p.suffix == ".obj"]

    for asset in progress(textures, name):
        container = "assets/" + asset.relative_to(root).as_posix().lower()

        raw = read_raw_texture(asset)
        if raw is not None:
            texture_format, width, height, data_bytes = raw
            texture = bundle.add_raw_texture(
                asset.stem, texture_format, width, height, data_bytes
            )
            size = (width, height)
        else:
            image = Image.open(asset).convert("RGBA")
            texture = bundle.add_texture(asset.stem, image)
            size = image.size
        bundle.register(container, texture, [texture])

        if asset.stem.endswith(MATERIAL_SUFFIX):
            material = bundle.add_material(asset.stem, texture)
            mat_path = container.removesuffix(".png") + ".mat"
            bundle.register(mat_path, material, [material, texture, bundle.shader])
        else:
            sprite = bundle.add_sprite(asset.stem, texture, size)
            bundle.register(container, sprite, [sprite, texture])

    if meshes:
        # Figurine .obj meshes need Mesh/GameObject/MeshRenderer serialization (phase 2).
        skipped = ", ".join(m.name for m in meshes)
        print(f"  {name}: skipped {len(meshes)} unsupported mesh(es): {skipped}")

    bundle.save(output)
    print(f"  {name}: {len(textures)} textures, {output.stat().st_size / 1e6:.1f} MB")
