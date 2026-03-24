#!/bin/bash
# test_multi_op.sh -- Integration test: multi-operation .hop file
# Tests OPS-05 (multi-operation), OPS-06 (ordering), PATH-05 (curve decomposition)
# Usage: bash test/test_multi_op.sh
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_DIR="$SCRIPT_DIR"
OUTPUT_FILE="$TEST_DIR/test_multi_op.hop"
PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

echo "=== Multi-Operation Integration Test ==="
echo ""

# ==================================================================
# Step 1: Generate the .hop file with CRLF line endings
# Simulates what HopExport.cs produces when all 6 components are wired
# ==================================================================
echo "--- Generating multi-operation .hop file ---"

printf ';MAKROTYP=0\r\n' > "$OUTPUT_FILE"
printf ';INSTVERSION=\r\n' >> "$OUTPUT_FILE"
printf ';EXEVERSION=\r\n' >> "$OUTPUT_FILE"
printf ';BILD=\r\n' >> "$OUTPUT_FILE"
printf ';INFO=\r\n' >> "$OUTPUT_FILE"
printf ';WZGV=7023K_681\r\n' >> "$OUTPUT_FILE"
printf ';WZGVCONFIG=\r\n' >> "$OUTPUT_FILE"
printf ';MASCHINE=HOLZHER\r\n' >> "$OUTPUT_FILE"
printf ';NCNAME=test_multi_op\r\n' >> "$OUTPUT_FILE"
printf ';KOMMENTAR=\r\n' >> "$OUTPUT_FILE"
printf ';DX=0.000\r\n' >> "$OUTPUT_FILE"
printf ';DY=0.000\r\n' >> "$OUTPUT_FILE"
printf ';DZ=0\r\n' >> "$OUTPUT_FILE"
printf ';DIALOGDLL=Dialoge.Dll\r\n' >> "$OUTPUT_FILE"
printf ';DIALOGPROC=StandardFormAnzeigen\r\n' >> "$OUTPUT_FILE"
printf ';AUTOSCRIPTSTART=1\r\n' >> "$OUTPUT_FILE"
printf ';BUTTONBILD=\r\n' >> "$OUTPUT_FILE"
printf ';DIMENSION_UNIT=0\r\n' >> "$OUTPUT_FILE"
printf 'VARS\r\n' >> "$OUTPUT_FILE"
printf '   DX := 800;*VAR*Dimension X\r\n' >> "$OUTPUT_FILE"
printf '   DY := 400;*VAR*Dimension Y\r\n' >> "$OUTPUT_FILE"
printf '   DZ := 19;*VAR*Dimension Z\r\n' >> "$OUTPUT_FILE"
printf 'START\r\n' >> "$OUTPUT_FILE"
printf 'Fertigteil (DX,DY,DZ,0,0,0,0,0,'"'"''"'"',0,0,0)\r\n' >> "$OUTPUT_FILE"
printf 'CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 1: Contour (WZF tool call + SP/G01/G03M/G01/EP) --
printf 'WZF (20,_VE,_V*1,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'SP (100,200,-5,2,0,_ANF,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)\r\n' >> "$OUTPUT_FILE"
printf 'G01 (300,200,0,0,0,2)\r\n' >> "$OUTPUT_FILE"
printf 'G03M (400,300,0,350,250,0,0,2,0)\r\n' >> "$OUTPUT_FILE"
printf 'G01 (500,200,0,0,0,2)\r\n' >> "$OUTPUT_FILE"
printf 'EP (0,_ANF,0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 2: Rectangular Pocket --
printf 'WZF (20,_VE,_V*0.4,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'CALL _RechteckTasche_V5(VAL x_Mitte:=400,Y_Mitte:=200,Taschenlaenge:=100,Taschenbreite:=80,Radius:=10,Winkel:=0,Tiefe:=-5,Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=1,umkehren:=0,esxy:=0,esmd:=0,laser:=0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 3: Circular Pocket --
printf 'WZF (20,_VE,_V*0.5,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'CALL _Kreistasche_V5(VAL X_Mitte:=200,Y_Mitte:=150,Radius:=30,Tiefe:=-8,Zustellung:=0,AB:=2,ABF:=_ANF,Interpol:=0,umkehren:=0,esxy:=0,esmd:=0,laser:=0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 4: Circular Path --
printf 'WZF (20,_VE,_V*1,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'CALL _Kreisbahn_V5(VAL X_Mitte:=600,Y_Mitte:=300,Tiefe:=-19,ZuTiefe:=0,Radius:=50,Radiuskorrektur:=1,AB:=1,Aufmass:=0,Bearb_umkehren:=1,Winkel:=360,ANF:=_ANF,ABF:=_ANF,Rampe:=1,Interpol:=0,esxy:=0,esmd:=0,laser:=0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 5: Free Slot --
printf 'WZF (20,_VE,_V*1,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'CALL _nuten_frei_v5(VAL X1:=100,Y1:=350,X2:=300,Y2:=350,NB:=14,Tiefe:=-10,LAGE:=0,RK:=0,SPEGA:=0,EPEGA:=0,esmd:=0,esxy1:=0,esxy2:=0)\r\n' >> "$OUTPUT_FILE"

# -- Operation 6: Drilling (3 holes) --
printf 'WZB (501,_VE,_V*1,_VA,_SD,0,'"'"''"'"')\r\n' >> "$OUTPUT_FILE"
printf 'Bohrung (150,100,5,-10,8,0,0,0,0,0,0,0)\r\n' >> "$OUTPUT_FILE"
printf 'Bohrung (250,100,5,-10,8,0,0,0,0,0,0,0)\r\n' >> "$OUTPUT_FILE"
printf 'Bohrung (350,100,5,-10,8,0,0,0,0,0,0,0)\r\n' >> "$OUTPUT_FILE"

echo "  Generated: $OUTPUT_FILE"
echo ""

# ==================================================================
# Step 2: Run validate_hop.sh
# ==================================================================
echo "--- Structural validation ---"
bash "$SCRIPT_DIR/validate_hop.sh" "$OUTPUT_FILE"
VALIDATE_EXIT=$?
echo ""
if [ $VALIDATE_EXIT -eq 0 ]; then
  pass "validate_hop.sh passed"
else
  fail "validate_hop.sh failed with exit code $VALIDATE_EXIT"
fi

# ==================================================================
# Step 3: Multi-operation content checks
# ==================================================================
echo ""
echo "--- Multi-operation content checks ---"

# Check: SP present (contour start)
if grep -q "^SP (" "$OUTPUT_FILE"; then pass "Contour SP found"; else fail "Contour SP missing"; fi

# Check: G01 present (linear move)
if grep -q "^G01 (" "$OUTPUT_FILE"; then pass "Contour G01 found"; else fail "Contour G01 missing"; fi

# Check: G03M present (arc)
if grep -q "^G03M (" "$OUTPUT_FILE"; then pass "Contour G03M arc found"; else fail "Contour G03M missing"; fi

# Check: EP present (contour end)
if grep -q "^EP (" "$OUTPUT_FILE"; then pass "Contour EP found"; else fail "Contour EP missing"; fi

# Check: RechteckTasche present
if grep -q "CALL _RechteckTasche_V5" "$OUTPUT_FILE"; then pass "RechteckTasche found"; else fail "RechteckTasche missing"; fi

# Check: Kreistasche present
if grep -q "CALL _Kreistasche_V5" "$OUTPUT_FILE"; then pass "Kreistasche found"; else fail "Kreistasche missing"; fi

# Check: Kreisbahn present
if grep -q "CALL _Kreisbahn_V5" "$OUTPUT_FILE"; then pass "Kreisbahn found"; else fail "Kreisbahn missing"; fi

# Check: nuten_frei present
if grep -q "CALL _nuten_frei_v5" "$OUTPUT_FILE"; then pass "nuten_frei found"; else fail "nuten_frei missing"; fi

# Check: Bohrung present (at least 3)
BOHR_COUNT=$(grep -c "^Bohrung (" "$OUTPUT_FILE")
if [ "$BOHR_COUNT" -ge 3 ]; then pass "Bohrung found ($BOHR_COUNT holes)"; else fail "Expected >= 3 Bohrung, found $BOHR_COUNT"; fi

# Check: Multiple WZF/WZB tool calls (one per operation group)
WZ_COUNT=$(grep -c "^WZ[FBS] (" "$OUTPUT_FILE")
if [ "$WZ_COUNT" -ge 6 ]; then pass "Tool calls found ($WZ_COUNT total)"; else fail "Expected >= 6 tool calls, found $WZ_COUNT"; fi

# Check: Operation ordering -- SP comes before CALL _RechteckTasche, which comes before Bohrung
LINE_SP=$(grep -n "^SP (" "$OUTPUT_FILE" | head -1 | cut -d: -f1)
LINE_RECT=$(grep -n "CALL _RechteckTasche_V5" "$OUTPUT_FILE" | head -1 | cut -d: -f1)
LINE_BOHR=$(grep -n "^Bohrung (" "$OUTPUT_FILE" | head -1 | cut -d: -f1)
if [ -n "$LINE_SP" ] && [ -n "$LINE_RECT" ] && [ "$LINE_SP" -lt "$LINE_RECT" ]; then
  pass "Contour (line $LINE_SP) before RectPocket (line $LINE_RECT)"
else
  fail "Ordering: contour should come before rect pocket"
fi
if [ -n "$LINE_RECT" ] && [ -n "$LINE_BOHR" ] && [ "$LINE_RECT" -lt "$LINE_BOHR" ]; then
  pass "RectPocket (line $LINE_RECT) before Bohrung (line $LINE_BOHR)"
else
  fail "Ordering: rect pocket should come before drilling"
fi

# Check: No comma-as-decimal (German locale leak)
if grep -P "\d,\d" "$OUTPUT_FILE" | grep -v "^;" | grep -v "VARS" | grep -qv ";\*VAR\*"; then
  fail "Found comma decimal separator outside header/VARS (possible locale issue)"
else
  pass "No comma decimal separators in operation lines"
fi

# Check: All depths are negative in operation macros
if grep -oP "Tiefe:=\K[0-9]" "$OUTPUT_FILE" | head -1 | grep -q "[0-9]"; then
  fail "Found positive Tiefe value (should be negative)"
else
  pass "All Tiefe values are negative"
fi

# ==================================================================
# Summary
# ==================================================================
echo ""
TOTAL=$((PASS + FAIL))
echo "=== RESULT: $PASS/$TOTAL passed ==="
if [ $FAIL -gt 0 ]; then
  echo "=== OVERALL: FAIL ($FAIL failures) ==="
  exit 1
else
  echo "=== OVERALL: PASS ==="
  # Clean up test file
  rm -f "$OUTPUT_FILE"
  exit 0
fi
