#!/bin/bash
# Developer helper: drives the macOS build with synthetic input and saves screenshots of the
# game window. Clicks are given in the game's 1280x720 virtual screen and mapped onto the
# window, so they stay correct however macOS places the window.
#
#   tools/capture.sh <scenario>
#
# Scenarios: shots, lightcycle, scores, gameover
set -u
cd "$(dirname "$0")/.."

BIN=PSSCollection.DesktopGL/bin/Debug/net10.0/PSSCollection
SCENARIO=${1:-shots}
OUT=/tmp/pss-shots
mkdir -p "$OUT"

# Another app's window can sit over the game and swallow synthetic input, so raise it first.
raise_game() {
  osascript <<'AS' >/dev/null 2>&1
tell application "System Events"
  tell process "PSSCollection"
    set frontmost to true
    try
      perform action "AXRaise" of window 1
    end try
  end tell
end tell
AS
}

window_rect() {
  osascript -e 'tell application "System Events" to tell process "PSSCollection" to get {position, size} of window 1' 2>/dev/null | tr -d ' '
}

# click <virtual x> <virtual y>
click() {
  raise_game
  local rect x y h title
  rect=$(window_rect)
  x=$(echo "$rect" | cut -d, -f1)
  y=$(echo "$rect" | cut -d, -f2)
  h=$(echo "$rect" | cut -d, -f4)
  x=${x:-320}; y=${y:-152}; h=${h:-748}
  title=$((h - 720)); [ "$title" -lt 0 ] && title=0
  # Hold the button for a while: a synthetic click can otherwise start and finish between frames.
  cliclick "m:$((x + $1)),$((y + title + $2))" "w:120" "dd:" "w:180" "du:" >/dev/null 2>&1
  sleep 0.6
}

# key <cliclick key name>; keys are only used where no clickable control exists.
key() {
  raise_game
  cliclick "kd:$1" "w:160" "ku:$1" >/dev/null 2>&1
  sleep 0.45
}

snap() {
  raise_game
  sleep 0.2
  screencapture -x "$OUT/$1-full.png"
  local rect x y h
  rect=$(window_rect)
  x=$(echo "$rect" | cut -d, -f1)
  y=$(echo "$rect" | cut -d, -f2)
  h=$(echo "$rect" | cut -d, -f4)
  python3 - "$1" "${x:-320}" "${y:-152}" "${h:-748}" <<'PY'
import sys
from PIL import Image
name, x, y, h = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
title_bar = max(0, h - 720)
img = Image.open(f"/tmp/pss-shots/{name}-full.png")
img.crop((x, y + title_bar, x + 1280, y + title_bar + 720)).resize((1000, 562), Image.LANCZOS) \
   .convert("RGB").save(f"/tmp/pss-shots/{name}.jpg", quality=85)
PY
}

pkill -f "$BIN" >/dev/null 2>&1
sleep 0.5
"$BIN" > "$OUT/run.log" 2>&1 &
sleep 3
raise_game
sleep 0.4
raise_game

# A click on the title bar gives the window keyboard focus; the first click on the game
# content is otherwise used up by macOS focusing the window.
rect=$(window_rect)
x=$(echo "$rect" | cut -d, -f1)
y=$(echo "$rect" | cut -d, -f2)
cliclick "c:$(( ${x:-320} + 640 )),$(( ${y:-152} + 10 ))" >/dev/null 2>&1
sleep 0.5

snap "$SCENARIO-1-title"

case "$SCENARIO" in
  shots)
    click 640 560                      # any click on the title starts the game
    sleep 1
    snap "$SCENARIO-2-menu"
    click 234 219                      # INVADERS
    sleep 1
    snap "$SCENARIO-3-skill"
    click 640 302                      # SKILLED
    sleep 2.5
    for i in 1 2 3 4 5 6; do key space; done
    key right; key right; key right
    sleep 1
    snap "$SCENARIO-4-play"
    key p
    sleep 0.6
    snap "$SCENARIO-5-paused"
    ;;
  lightcycle)
    click 640 600
    click 640 600
    sleep 1.2
    snap "$SCENARIO-2-menu"
    click 640 445                      # LIGHT CYCLE
    sleep 1
    snap "$SCENARIO-3-skill"
    click 640 302                      # SKILLED
    sleep 2.5
    key up; key left; key down
    sleep 0.5
    snap "$SCENARIO-4-play"
    key p
    sleep 0.6
    snap "$SCENARIO-5-paused"
    ;;
  hopper)
    click 640 600
    click 640 600
    sleep 1.2
    click 1046 219                     # HOPPER
    sleep 1
    click 640 302                      # SKILLED
    sleep 2.5
    key up; key up; key up
    sleep 0.6
    snap "$SCENARIO-1-play"
    ;;
  storm)
    click 640 600
    click 640 600
    sleep 1.2
    click 234 441                      # ELEKTRO STORM
    sleep 1
    click 640 302                      # SKILLED
    sleep 3
    click 500 260                      # aim and fire
    sleep 0.25
    click 700 180
    sleep 0.3
    snap "$SCENARIO-1-play"
    sleep 1
    snap "$SCENARIO-2-later"
    ;;
  scores)
    key h
    sleep 1
    snap "$SCENARIO-2-table"
    key right
    sleep 0.8
    snap "$SCENARIO-3-next"
    key right; key right; key right
    sleep 0.8
    snap "$SCENARIO-4-lightcycle"
    ;;
  gameover)
    click 640 600
    click 640 600
    sleep 1.2
    click 234 219                      # INVADERS
    sleep 1
    click 640 514                      # ACE
    sleep 120
    snap "$SCENARIO-2-after-run"
    ;;
esac

pkill -f "$BIN" >/dev/null 2>&1
ls "$OUT"
