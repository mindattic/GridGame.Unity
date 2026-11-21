"""The one asset file-naming rule for the tools (GG-T561); the game's copy is GridGame.Core.Content.AssetNames.

A file name is a stem (lowercase words joined by '-') plus a two-digit variant from -00, then the extension:
acolyte-female-00.png, hag-02.png, tithe-moors-00.jpg. Variant 00 is the default art; 01, 02, ... are alternates
of the same thing (a costume, another backdrop of the same region). Ids never change: only files are named this way.

Portraits (GG-T557): a class's art is Portraits/<Portrait>-<NN>.png, its male art Portraits/<MalePortrait>-<NN>.png,
with Portrait / MalePortrait / PortraitVariant read from classes.json (see portrait_file below).
"""
import re

DEFAULT_VARIANT = 0
_PARSE = re.compile(r"^(?P<stem>[a-z0-9]+(?:-[a-z0-9]+)*)-(?P<n>\d\d)$")


def stem(*words):
    """Lowercase words joined by '-'. Spaces, underscores and hyphens separate words, as does a lower-to-upper case
    change (PandaKeeper -> panda-keeper); other punctuation is dropped (Morrow's Shadow -> morrows-shadow); digits stay
    with their word (plus3). Same as AssetNames.Stem."""
    out = []
    pending = False
    for w in words:
        if not w:
            continue
        pending = True
        for i, c in enumerate(w):
            if c in " _-\t":
                pending = True
                continue
            if not c.isalnum():
                continue
            camel = c.isupper() and i > 0 and (w[i - 1].islower() or w[i - 1].isdigit()
                                               or (w[i - 1].isupper() and i + 1 < len(w) and w[i + 1].islower()))
            if (pending or camel) and out:
                out.append("-")
            pending = False
            out.append(c.lower())
    return "".join(out)


def file(stem_, variant=DEFAULT_VARIANT):
    """stem-00 (no extension)."""
    if not 0 <= variant <= 99:
        raise ValueError(f"asset variants run 00..99, got {variant}")
    return f"{stem_}-{variant:02d}" if stem_ else None


def of(id_, variant=DEFAULT_VARIANT):
    """file(stem(id)): an asset keyed by an id (aegisguard_hands_of_mercy -> aegisguard-hands-of-mercy-00)."""
    return file(stem(id_), variant) if id_ else None


def parse(name):
    """(stem, variant) of a file name without extension, or None when it doesn't follow the rule."""
    m = _PARSE.match(name or "")
    return (m.group("stem"), int(m.group("n"))) if m else None


def is_valid(name):
    return parse(name) is not None


def portrait_file(cls, male=False, variant=None):
    """The Portraits/ file (no extension) a classes.json entry draws: its MalePortrait stem when male and it has one,
    else its Portrait stem, at its PortraitVariant (or the given variant). Same as ClassDef.PortraitFile."""
    s = cls.get("MalePortrait") if male and cls.get("MalePortrait") else cls.get("Portrait") or stem(cls["Id"])
    return file(s, cls.get("PortraitVariant", DEFAULT_VARIANT) if variant is None else variant)


def portrait_users(classes):
    """{portrait file: [(classId, "Figure" | "MaleFigure")]}: every class that draws each Portraits/ file, and which of
    its figure boxes frames it (a shared file, like the Wild Child's that the Monster Tamer draws, has several)."""
    users = {}
    for c in classes:
        users.setdefault(portrait_file(c), []).append((c["Id"], "Figure"))
        if c.get("MalePortrait"):
            users.setdefault(portrait_file(c, male=True), []).append((c["Id"], "MaleFigure"))
    return users


def portrait_files_for(names, classes):
    """Portrait file names for a CLI list that may mix files (acolyte-female-00) and class ids (sister)."""
    by_id = {c["Id"]: c for c in classes}
    return [portrait_file(by_id[n]) if n in by_id else n for n in names]


if __name__ == "__main__":
    assert stem("PandaKeeper") == "panda-keeper"
    assert stem("Panda Keeper", "female") == "panda-keeper-female"
    assert stem("aegisguard_hands_of_mercy") == "aegisguard-hands-of-mercy"
    assert stem("Morrow's Shadow") == "morrows-shadow"
    assert stem("eq_lantern_seraph_plus1") == "eq-lantern-seraph-plus1"
    assert stem("HPUp") == "hp-up"
    assert file("hag", 2) == "hag-02"
    assert parse("tithe-moors-00") == ("tithe-moors", 0) and parse("Sister") is None
    print("ok")
