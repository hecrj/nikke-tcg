"""Regenerate src/nikke/seed.bundle from a full Unity-built reference bundle.

UnityPy cannot author a SerializedFile from scratch, so nikke.bundler builds every bundle
on top of a tiny seed: the container scaffolding, one prototype of each object type we
emit (Texture2D/Sprite/Material) to clone, and the built-in Standard shader that
booster-pack materials bind to. This distills that seed from a real bundle by throwing
everything else away and shrinking the prototypes to 4x4.

You only need to run this to refresh the seed (e.g. for a new Unity version). It expects a
reference bundle at reference/pokemon; that 239 MB file is NOT committed (see .gitignore).

    uv run python scripts/distill_seed.py
"""

import pathlib

import UnityPy
from PIL import Image
from UnityPy.enums import TextureFormat
from UnityPy.files.SerializedFile import SerializedFile

REFERENCE = pathlib.Path("reference/pokemon")
SEED = pathlib.Path("src/nikke/seed.bundle")


def main() -> None:
    env = UnityPy.load(str(REFERENCE))
    sf = next(f for f in env.file.files.values() if isinstance(f, SerializedFile))

    def one(type_name):
        return next(o for o in sf.objects.values() if o.type.name == type_name)

    texture = one("Texture2D")
    sprite = one("Sprite")
    # defaultMat is the reference material bound to the built-in Standard shader.
    material = next(
        o
        for o in sf.objects.values()
        if o.type.name == "Material" and o.read().m_Name == "defaultMat"
    )
    shader_path_id = material.read().m_Shader.m_PathID
    manifest = one("AssetBundle")

    keep = {texture.path_id, sprite.path_id, material.path_id, shader_path_id, manifest.path_id}
    sf.objects = {path_id: o for path_id, o in sf.objects.items() if path_id in keep}
    # Drop the resource stream (.resS); our prototypes store data inline.
    env.file.files = {n: f for n, f in env.file.files.items() if isinstance(f, SerializedFile)}

    # Shrink the prototypes so the seed stays tiny (the shader dominates at ~47 KB).
    tex = texture.read()
    tex.set_image(Image.new("RGBA", (4, 4)), target_format=TextureFormat.BC7)
    tex.m_Name = "__proto_tex"
    tex.save()

    spr = sprite.read()
    spr.m_Name = "__proto_sprite"
    spr.m_Rect.width = spr.m_Rect.height = 4.0
    spr.m_RD.textureRect.width = spr.m_RD.textureRect.height = 4.0
    spr.save()

    ab = manifest.read()
    ab.m_PreloadTable = []
    ab.m_Container = []
    ab.m_Name = "seed"
    ab.save()

    sf.mark_changed()
    SEED.write_bytes(env.file.save(packer="lz4"))
    print(f"wrote {SEED} ({SEED.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
