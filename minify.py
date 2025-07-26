import re
import sys

def minify_script(code):
    # Remove multiline comments
    code = re.sub(r'/\*.*?\*/', '', code, flags=re.DOTALL)

    # Remove single-line comments, but keep newline
    code = re.sub(r'//.*', '', code)

    # Collapse multiple spaces to one (per line)
    lines = []
    for line in code.splitlines():
        if '"' in line:  # crude check for strings, don't collapse those
            lines.append(line)
        else:
            lines.append(re.sub(r'\s+', ' ', line).rstrip())

    return '\n'.join(lines)

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python minify.py input.cs output.cs")
        sys.exit(1)

    with open(sys.argv[1], 'r') as infile:
        original = infile.read()

    minified = minify_script(original)

    with open(sys.argv[2], 'w') as outfile:
        outfile.write(minified)
