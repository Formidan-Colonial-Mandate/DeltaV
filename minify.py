import re
import sys

def minify_script(code):
    # Remove multiline comments /* ... */
    code = re.sub(r'/\*.*?\*/', '', code, flags=re.DOTALL)

    # Split into string and non-string parts
    parts = re.split(r'(".*?(?<!\\\\)")', code)

    # Process only non-string parts
    for i in range(0, len(parts), 2):
        # Remove single-line comments
        parts[i] = re.sub(r'//.*', '', parts[i])
        # Collapse multiple spaces but preserve spacing around operators
        parts[i] = re.sub(r'\s+', ' ', parts[i])

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
