"""Release gate for a built .aab (GG-T1516): every native library is arm64-v8a only with 16 KB aligned ELF LOAD
segments (Play's page-size rule for apps targeting Android 15+), and the merged manifest asks only for permissions
listed in docs/legal/play-data-safety.md (the backticked names in its Permissions row are the allowlist).
Usage: python tools/check_aab.py [Build/AndroidRelease/GridGame.aab] [--bundletool JAR] [--java EXE]
Without --bundletool/--java, uses the newest Unity Hub editor's AndroidPlayer tools."""
import argparse, glob, os, re, struct, subprocess, sys, zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOC = os.path.join(ROOT, "docs", "legal", "play-data-safety.md")
PAGE = 16384
ANDROID_NS = "{http://schemas.android.com/apk/res/android}"


def allowed_permissions(doc=DOC):
    with open(doc, encoding="utf-8") as f:
        row = next((l for l in f if l.startswith("| Permissions |")), None)
    if row is None:
        raise SystemExit(f"no '| Permissions |' row in {doc}")
    names = re.findall(r"`([A-Za-z0-9_.]+)`", row)
    return {n if "." in n else "android.permission." + n for n in names}


def load_segment_aligns(data):
    if data[:4] != b"\x7fELF":
        return None
    is64 = data[4] == 2
    le = "<" if data[5] == 1 else ">"
    if is64:
        phoff, = struct.unpack_from(le + "Q", data, 0x20)
        phentsize, phnum = struct.unpack_from(le + "HH", data, 0x36)
    else:
        phoff, = struct.unpack_from(le + "I", data, 0x1C)
        phentsize, phnum = struct.unpack_from(le + "HH", data, 0x2A)
    aligns = []
    for i in range(phnum):
        o = phoff + i * phentsize
        if struct.unpack_from(le + "I", data, o)[0] != 1:
            continue
        aligns.append(struct.unpack_from(le + "Q", data, o + 0x30)[0] if is64 else struct.unpack_from(le + "I", data, o + 0x1C)[0])
    return aligns


def check_libs(aab):
    problems, count = [], 0
    with zipfile.ZipFile(aab) as z:
        for name in z.namelist():
            m = re.match(r"[^/]+/lib/([^/]+)/(.+)$", name)
            if not m or name.endswith("/"):
                continue
            abi, lib = m.groups()
            count += 1
            if abi != "arm64-v8a":
                problems.append(f"{name}: ABI {abi}, only arm64-v8a is allowed")
                continue
            if not lib.endswith(".so"):
                continue
            aligns = load_segment_aligns(z.read(name))
            if aligns is None:
                problems.append(f"{name}: not an ELF file")
            elif not aligns or any(a < PAGE or a % PAGE for a in aligns):
                problems.append(f"{name}: LOAD segment alignment {[hex(a) for a in aligns]}, need a multiple of 0x{PAGE:x}")
    if count == 0:
        problems.append("no native libraries found under */lib/")
    return problems, count


def find_unity_tools():
    pat = r"C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer"
    for t in sorted(glob.glob(pat), reverse=True):
        jars = sorted(glob.glob(os.path.join(t, "Tools", "bundletool-all-*.jar")))
        java = os.path.join(t, "OpenJDK", "bin", "java.exe")
        if jars and os.path.exists(java):
            return java, jars[-1]
    return "java", None


def manifest_permissions(aab, java, jar):
    out = subprocess.run([java, "-jar", jar, "dump", "manifest", "--bundle=" + aab],
                         capture_output=True, text=True)
    if out.returncode:
        raise SystemExit("bundletool dump manifest failed:\n" + out.stderr)
    root = ET.fromstring(out.stdout)
    return sorted({e.get(ANDROID_NS + "name") for tag in ("uses-permission", "uses-permission-sdk-23")
                   for e in root.iter(tag)} - {None})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("aab", nargs="?", default=os.path.join(ROOT, "Build", "AndroidRelease", "GridGame.aab"))
    ap.add_argument("--bundletool")
    ap.add_argument("--java")
    a = ap.parse_args()
    if not os.path.exists(a.aab):
        raise SystemExit(f"no such bundle: {a.aab}")
    java, jar = find_unity_tools()
    java, jar = a.java or java, a.bundletool or jar
    if not jar:
        raise SystemExit("bundletool jar not found; pass --bundletool")
    problems, count = check_libs(a.aab)
    print(f"{count} native files checked (arm64-v8a only, {PAGE // 1024} KB aligned)")
    perms, allowed = manifest_permissions(a.aab, java, jar), allowed_permissions()
    for p in perms:
        print("permission", p, "" if p in allowed else "NOT ALLOWED")
    problems += [f"manifest permission {p} is not in {os.path.relpath(DOC, ROOT)}" for p in perms if p not in allowed]
    for p in problems:
        print("FAIL", p)
    print("check_aab: " + ("FAILED" if problems else "OK"))
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
