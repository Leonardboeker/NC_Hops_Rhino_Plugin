#!/bin/bash
# validate_hop.sh -- Validate .hop file structure against expected format
# Usage: bash validate_hop.sh <path-to-hop-file>
# Exit code: 0 = all pass, 1 = any fail
# Works in Git Bash on Windows

set -u

HOP_FILE="$1"
PASS_COUNT=0
FAIL_COUNT=0
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

pass() {
  echo "  PASS: $1"
  PASS_COUNT=$((PASS_COUNT + 1))
}

fail() {
  echo "  FAIL: $1"
  FAIL_COUNT=$((FAIL_COUNT + 1))
}

echo "=== .hop File Validation ==="
echo "File: $HOP_FILE"
echo ""

# ------------------------------------------------------------------
# Check 1: File exists
# ------------------------------------------------------------------
echo "--- Check 1: File exists ---"
if [ -f "$HOP_FILE" ]; then
  pass "File exists"
else
  fail "File not found: $HOP_FILE"
  echo ""
  echo "=== RESULT: FAIL (file not found) ==="
  exit 1
fi

# ------------------------------------------------------------------
# Check 2: No BOM (first bytes must be ;M not EF BB BF)
# ------------------------------------------------------------------
echo "--- Check 2: No BOM ---"
FIRST_BYTES=$(xxd -l 3 -p "$HOP_FILE" 2>/dev/null)
if [ -z "$FIRST_BYTES" ]; then
  # xxd might not be available, try od
  FIRST_BYTES=$(od -A n -t x1 -N 3 "$HOP_FILE" 2>/dev/null | tr -d ' ')
fi
if [ "$FIRST_BYTES" = "efbbbf" ]; then
  fail "File starts with UTF-8 BOM (EF BB BF)"
else
  pass "No BOM detected (first bytes: $FIRST_BYTES)"
fi

# ------------------------------------------------------------------
# Check 3: CRLF line endings
# ------------------------------------------------------------------
echo "--- Check 3: CRLF line endings ---"
# Check if file contains at least one CR+LF sequence
if xxd -p "$HOP_FILE" 2>/dev/null | tr -d '\n' | grep -q "0d0a"; then
  pass "CRLF line endings detected"
else
  # Fallback: use od to check
  if od -A n -t x1 "$HOP_FILE" 2>/dev/null | tr -d ' \n' | grep -q "0d0a"; then
    pass "CRLF line endings detected"
  else
    fail "No CRLF (\\r\\n) line endings found"
  fi
fi

# ------------------------------------------------------------------
# Check 4: ;MAKROTYP=0 on first line
# ------------------------------------------------------------------
echo "--- Check 4: ;MAKROTYP=0 on first line ---"
FIRST_LINE=$(head -1 "$HOP_FILE" | tr -d '\r\n')
if [ "$FIRST_LINE" = ";MAKROTYP=0" ]; then
  pass ";MAKROTYP=0 is the first line"
else
  fail "First line is '$FIRST_LINE', expected ';MAKROTYP=0'"
fi

# ------------------------------------------------------------------
# Check 5: Required structural markers present
# ------------------------------------------------------------------
echo "--- Check 5: Required structural markers ---"

check_marker() {
  local MARKER="$1"
  if grep -q "$MARKER" "$HOP_FILE"; then
    pass "Found: $MARKER"
  else
    fail "Missing: $MARKER"
  fi
}

check_marker ";MAKROTYP=0"
check_marker ";MASCHINE=HOLZHER"
check_marker ";DIMENSION_UNIT=0"
check_marker "^VARS$"
check_marker "^START$"
check_marker "Fertigteil (DX,DY,DZ"
check_marker "CALL HH_Park"

# ------------------------------------------------------------------
# Check 6: VARS block format (3-space indent, ;*VAR* suffix, dot decimals)
# ------------------------------------------------------------------
echo "--- Check 6: VARS block format ---"

# Check 3-space indent for DX
if grep -q "^   DX := " "$HOP_FILE"; then
  pass "DX has 3-space indent"
else
  fail "DX missing or wrong indent (expected 3-space: '   DX := ')"
fi

# Check 3-space indent for DY
if grep -q "^   DY := " "$HOP_FILE"; then
  pass "DY has 3-space indent"
else
  fail "DY missing or wrong indent"
fi

# Check 3-space indent for DZ
if grep -q "^   DZ := " "$HOP_FILE"; then
  pass "DZ has 3-space indent"
else
  fail "DZ missing or wrong indent"
fi

# Check ;*VAR* suffix on VARS lines
if grep "^   DX := " "$HOP_FILE" | grep -q ";\*VAR\*"; then
  pass "DX line has ;*VAR* suffix"
else
  fail "DX line missing ;*VAR* suffix"
fi

# Check dot decimal separator (no comma in numeric value between := and ;)
DX_LINE=$(grep "^   DX := " "$HOP_FILE" | tr -d '\r')
if echo "$DX_LINE" | grep -Pq "^   DX := [0-9]+\.?[0-9]*;\*VAR\*"; then
  pass "DX uses dot decimal separator"
elif echo "$DX_LINE" | grep -q ","; then
  fail "DX appears to use comma decimal separator"
else
  pass "DX uses dot decimal separator (integer value)"
fi

# ------------------------------------------------------------------
# Check 7: Correct ordering (MAKROTYP before VARS before START before Fertigteil)
# ------------------------------------------------------------------
echo "--- Check 7: Structural ordering ---"

LINE_MAKROTYP=$(grep -n ";MAKROTYP=0" "$HOP_FILE" | head -1 | cut -d: -f1)
LINE_VARS=$(grep -n "^VARS$" "$HOP_FILE" | head -1 | cut -d: -f1)
LINE_START=$(grep -n "^START$" "$HOP_FILE" | head -1 | cut -d: -f1)
LINE_FERTIGTEIL=$(grep -n "Fertigteil" "$HOP_FILE" | head -1 | cut -d: -f1)

if [ -n "$LINE_MAKROTYP" ] && [ -n "$LINE_VARS" ] && [ "$LINE_MAKROTYP" -lt "$LINE_VARS" ]; then
  pass ";MAKROTYP=0 (line $LINE_MAKROTYP) before VARS (line $LINE_VARS)"
else
  fail ";MAKROTYP=0 not before VARS"
fi

if [ -n "$LINE_VARS" ] && [ -n "$LINE_START" ] && [ "$LINE_VARS" -lt "$LINE_START" ]; then
  pass "VARS (line $LINE_VARS) before START (line $LINE_START)"
else
  fail "VARS not before START"
fi

if [ -n "$LINE_START" ] && [ -n "$LINE_FERTIGTEIL" ] && [ "$LINE_START" -lt "$LINE_FERTIGTEIL" ]; then
  pass "START (line $LINE_START) before Fertigteil (line $LINE_FERTIGTEIL)"
else
  fail "START not before Fertigteil"
fi

# ------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------
echo ""
TOTAL=$((PASS_COUNT + FAIL_COUNT))
echo "=== RESULT: $PASS_COUNT/$TOTAL passed ==="

if [ "$FAIL_COUNT" -gt 0 ]; then
  echo "=== OVERALL: FAIL ($FAIL_COUNT failures) ==="
  exit 1
else
  echo "=== OVERALL: PASS ==="
  exit 0
fi
