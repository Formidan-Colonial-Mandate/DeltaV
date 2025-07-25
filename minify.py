import re
import sys

def minify_script(code):
    # Remove multiline comments
    code = re.sub(r'/\\*.*?\\*/', '', code, flags=re.DOTALL)
    # Remove single-line comments
    code = re.sub(r'//.*', '', code)
    # Preserve string literals
    parts = re.split(r'(\".*?(?<!\\\\)\")', code)
    for i in range(0, len(parts), 2):  # Only strip non-string segments
        parts[i] = re.sub(r'\\s+', ' ', parts[i])
    return ''.join(parts)

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python minify.py input.cs output.cs")
        sys.exit(1)
    with open(sys.argv[1], 'r') as infile:
        original = infile.read()
    minified = minify_script(original)
    with open(sys.argv[2], 'w') as outfile:
        outfile.write(minified)
