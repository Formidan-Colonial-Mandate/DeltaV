import re

# Optional: variable renaming map (shorten internal symbols)
RENAME_MAP = {
    'includeRCS': 'i',
    'displayBurnTimes': 'b',
    'showAllDirections': 'd',
    'controller': 'c',
    'statusLog': 'l',
    'groupNames': 'g',
    'booting': 'u',
    'bootFrame': 'f',
    'bootMsg': 'm',
    'allThrusters': 't',
    'tanks': 'k',
    'allGroups': 'q',
    'HYDROGEN_DENSITY_KG_PER_L': 'H',
    'fuelUsageLpsBySubtypeId': 'F',
    'rcsSubtypes': 'R',
    'lcd': 'x',
}

def rename_vars(code):
    for k, v in RENAME_MAP.items():
        code = re.sub(r'\b' + re.escape(k) + r'\b', v, code)
    return code

def minify_script(code):
    code = re.sub(r'/\*.*?\*/', '', code, flags=re.DOTALL)  # multiline comments
    code = re.sub(r'//.*', '', code)                        # single line comments

    # preserve strings
    strings = {}
    def save_string(m):
        key = f"__STR{len(strings)}__"
        strings[key] = m.group(0)
        return key
    code = re.sub(r'\"(?:\\.|[^"\\])*\"', save_string, code)

    # smash whitespace
    code = re.sub(r'\s+', ' ', code)
    code = re.sub(r'\s*([=+\-*/<>!&|%^]+)\s*', r'\1', code)
    code = re.sub(r'\s*([{}()\[\],;])\s*', r'\1', code)
    code = re.sub(r';+', ';', code)

    # restore strings
    for k, v in strings.items():
        code = code.replace(k, v)

    code = rename_vars(code)
    return code.strip()

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 3:
        print("Usage: python minify.py input.cs output.cs")
        sys.exit(1)

    with open(sys.argv[1], 'r') as f:
        original = f.read()
    minified = minify_script(original)
    with open(sys.argv[2], 'w') as f:
        f.write(minified)
