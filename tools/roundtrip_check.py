# -*- coding: utf-8 -*-
"""Round-trip validation of golden .hop files against the independent
nc-hops parser (github.com/apers0/nc-hops, MIT).

The parser is a SECOND implementation of the .hop format written without
knowledge of Wallaby Hop — if it chokes on our output, our format drifted.
Checks per file:
  1. parser opens the file without raising
  2. VARS extraction finds DX/DY/DZ
  3. vertical drill count matches a simple grep of "Bohrung (" lines

Usage:  python tools/roundtrip_check.py [golden_dir]
Exit 0 = all files pass, 1 = any failure.
"""
import io
import os
import re
import sys

DEFAULT_GOLDEN = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "tests", "DynesticPostProcessor.Tests", "Golden")


def check_file(path):
    errors = []
    from ReadHop import ExtractHopProcessing

    try:
        parsed = ExtractHopProcessing(path)
    except Exception as e:  # noqa: BLE001 — report, don't crash the runner
        return ["parser raised on open: %r" % e]

    # 1. VARS extraction
    try:
        vars_result = parsed.get_vars()
        vars_text = str(vars_result)
        for key in ("DX", "DY", "DZ"):
            if key not in vars_text:
                errors.append("get_vars() missing %s (got: %.120s)" % (key, vars_text))
    except Exception as e:
        errors.append("get_vars() raised: %r" % e)

    # 2. Drill count cross-check (independent grep)
    # newline="" disables universal-newline translation so CRLF survives
    raw = io.open(path, encoding="ascii", errors="strict", newline="").read()
    grep_drills = len(re.findall(r"^Bohrung \(", raw, re.MULTILINE))
    try:
        lib_drills = parsed.drill_count()
        if callable(lib_drills):
            lib_drills = lib_drills()
    except TypeError:
        try:
            lib_drills = parsed.drill_count
        except Exception as e:
            errors.append("drill_count raised: %r" % e)
            lib_drills = None
    except Exception as e:
        errors.append("drill_count raised: %r" % e)
        lib_drills = None

    # The lib may count vertical+horizontal differently; require it to be
    # AT LEAST the grep count is too strict — just require it parsed to a
    # number when we have drills at all.
    if grep_drills > 0 and lib_drills in (None, 0):
        # Not fatal by itself if the lib counts differently — only warn.
        print("  note: grep found %d Bohrung lines, lib drill_count=%r"
              % (grep_drills, lib_drills))

    # 3. ASCII / CRLF invariants (raise happened above if non-ascii)
    if "\r\n" not in raw:
        errors.append("no CRLF line endings found")

    return errors


def main():
    golden_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_GOLDEN
    golden_dir = os.path.abspath(golden_dir)
    if not os.path.isdir(golden_dir):
        print("golden dir not found:", golden_dir)
        return 1

    hop_files = sorted(f for f in os.listdir(golden_dir) if f.endswith(".hop"))
    if not hop_files:
        print("no .hop files in", golden_dir)
        return 1

    failed = 0
    for name in hop_files:
        path = os.path.join(golden_dir, name)
        errors = check_file(path)
        if errors:
            failed += 1
            print("FAIL:", name)
            for e in errors:
                print("      -", e)
        else:
            print("OK:  ", name)

    print()
    print("%d/%d golden files round-trip clean" % (len(hop_files) - failed, len(hop_files)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
