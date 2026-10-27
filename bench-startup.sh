#!/usr/bin/env bash
# Measures how long the Linux build of wCalculator takes to start.
#
#   ./bench-startup.sh                      publish two variants (il and r2rc) and compare them (10 measured launches each)
#   ./bench-startup.sh -n 20                20 measured launches per variant
#   ./bench-startup.sh --only aot           only the Native AOT variant (--only il,r2r,r2rc,aot for several)
#   ./bench-startup.sh --reuse              do not publish again when build/bench/<variant> already exists
#   ./bench-startup.sh --app PATH           measure an existing build (a folder containing wCalculator, or the executable)
#
# Variants (both built from the current source):
#   il    plain IL, compiled at every launch (JIT), TieredPGO on: how a default "dotnet publish" behaves
#   r2r   plain ReadyToRun, TieredPGO off (READY_TO_RUN_COMPOSITE=0 ./build.sh)
#   r2rc  like r2r, but one composite image for the framework and the application (Windows builds, and where Native AOT is not possible)
#   aot   Native AOT: one native executable, no runtime to start: what ./build.sh produces on Linux (needs clang and zlib)
#
# Environment variables reach the application, so an experiment needs no rebuild, for instance
#   WCALC_RENDERING=gpu ./bench-startup.sh --only r2r --reuse    OpenGL when available (the Linux default is CPU rendering)
#
# Each launch runs the application with WCALC_STARTUP_TRACE=1 and WCALC_EXIT_AFTER_STARTUP=1: it prints the moment
# it reaches each milestone and closes itself once the window is shown and idle. The times are measured from the
# moment this script starts the process, so they include the .NET host and runtime start (the "main" milestone).
#
#   main              Main() reached: the .NET host and runtime started
#   builder-ready     the Avalonia builder is configured (nothing started yet)
#   warmup-done       settings, translations and calculation engine are ready (second thread, runs in parallel with the next steps)
#   platform-ready    window system and Skia are set up
#   app-initialize-start  Avalonia core services are registered, App.Initialize starts
#   app-xaml-loaded   Avalonia is set up and App.axaml (theme, styles) is loaded
#   appearance-applied  the colours and the accent of the settings are applied
#   viewmodel-created the main view model is handed over to the UI thread (waits for warmup-done if it is not finished yet)
#   window-base-created the native window is created (base Window class)
#   window-xaml-loaded the XAML of the window and of the main view is loaded
#   window-constructed the main window is built and has its view model
#   framework-ready   end of the application start-up code
#   calculator-view-start/-loaded  the calculator page (about 200 keys) is created when the window is first laid out
#   window-opened     the window is open
#   first-idle        the first layout and render passes are done: the window is usable (the figure to watch)
#
# A window opens and closes at every launch. Without a display (DISPLAY empty) the script starts Xvfb if it is installed.
# The first launches are discarded (--warmup, default 1): they fill the disk cache.
set -euo pipefail

cd "$(dirname "$0")"
ROOT="$PWD"
BENCH="$ROOT/build/bench"
RUNS=10
WARMUP=1
REUSE=0
APP=""
VARIANTS=(il r2rc)
# In the order they happen. The first, "window-opened" and "first-idle" are required; the others are shown when the build prints them.
MILESTONES=(main builder-ready warmup-done platform-ready app-initialize-start app-xaml-loaded appearance-applied viewmodel-created window-base-created window-xaml-loaded window-constructed framework-ready calculator-view-start calculator-view-loaded window-opened first-idle)
REQUIRED=(main window-opened first-idle)

die() { echo "bench-startup: $*" >&2; exit 1; }

usage() { sed -n '2,/^set -euo/p' "$0" | sed -e '$d' -e 's/^# \{0,1\}//'; }

while [ $# -gt 0 ]; do
  case "$1" in
    -n|--runs)   RUNS="${2:?missing value for $1}"; shift 2 ;;
    --warmup)    WARMUP="${2:?missing value for $1}"; shift 2 ;;
    --only)      IFS=, read -r -a VARIANTS <<<"${2:?missing value for $1}"; shift 2 ;;
    --reuse)     REUSE=1; shift ;;
    --app)       APP="${2:?missing value for $1}"; shift 2 ;;
    -h|--help)   usage; exit 0 ;;
    *)           echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ "$RUNS" =~ ^[0-9]+$ ]] && [ "$RUNS" -ge 1 ] || die "--runs must be a positive number"
[[ "$WARMUP" =~ ^[0-9]+$ ]] || die "--warmup must be a number"
for v in "${VARIANTS[@]}"; do
  case "$v" in il|r2r|r2rc|aot) ;; *) die "unknown variant '$v' (il, r2r, r2rc or aot)" ;; esac
done

case "$(uname -m)" in
  x86_64|amd64)  RID="linux-x64" ;;
  aarch64|arm64) RID="linux-arm64" ;;
  *) die "unsupported processor: $(uname -m)" ;;
esac

# ---------------------------------------------------------------- display
XVFB_PID=""
cleanup() {
  if [ -n "$XVFB_PID" ]; then
    kill "$XVFB_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

if [ -z "${DISPLAY:-}" ]; then
  command -v Xvfb >/dev/null 2>&1 || die "no display (DISPLAY is empty) and Xvfb is not installed"
  export DISPLAY=":97"
  Xvfb :97 -screen 0 1280x1024x24 -nolisten tcp >/dev/null 2>&1 &
  XVFB_PID=$!
  for _ in $(seq 1 50); do
    [ -S /tmp/.X11-unix/X97 ] && break
    sleep 0.1
  done
  echo "No display: using Xvfb on $DISPLAY"
fi

# ---------------------------------------------------------------- publish
publish_variant() { # <variant>
  local name="$1" dir="$BENCH/$1" log="$BENCH/$1.publish.log"
  local extra=()
  case "$name" in
    il)  extra=(-p:PublishReadyToRun=false -p:TieredPGO=true) ;;
    r2r) extra=(-p:PublishReadyToRun=true) ;;
    r2rc) extra=(-p:PublishReadyToRun=true -p:PublishReadyToRunComposite=true) ;;
    aot)  extra=(-p:PublishAot=true -p:PublishReadyToRun=false) ;;
  esac

  if [ "$REUSE" = "1" ] && [ -x "$dir/wCalculator" ]; then
    echo "[$name] reusing $dir"
    return 0
  fi

  command -v dotnet >/dev/null 2>&1 || die "dotnet not found (the .NET 10 SDK is needed to publish)"
  mkdir -p "$BENCH"
  rm -rf "${dir:?}"
  echo "[$name] publishing for $RID (log: ${log#"$ROOT"/})"
  if ! dotnet publish src/Calculator.Desktop -c Release -r "$RID" --self-contained true \
        -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "$dir" "${extra[@]}" >"$log" 2>&1; then
    tail -n 25 "$log" >&2
    die "publish of '$name' failed"
  fi
}

# ---------------------------------------------------------------- one launch
# Prints the milestones as "<name>=<tenths of a millisecond since launch>" separated by spaces; fails if one is missing.
run_once() { # <executable>
  local exe="$1" err t0 out="" name line t
  err="$(mktemp)"
  t0=$(( $(date +%s%N) / 1000 ))
  WCALC_STARTUP_TRACE=1 WCALC_EXIT_AFTER_STARTUP=1 timeout 60 "$exe" >/dev/null 2>"$err" || true

  for name in "${MILESTONES[@]}"; do
    line="$(grep -m1 "^WCALC_STARTUP $name " "$err" || true)"
    if [ -z "$line" ] && [[ " ${REQUIRED[*]} " != *" $name "* ]]; then
      continue # optional milestone this build does not print
    fi
    if [ -z "$line" ]; then
      echo "  milestone '$name' not reached; last lines of the error output:" >&2
      tail -n 8 "$err" >&2
      rm -f "$err"
      return 1
    fi
    t="${line##* }"
    out+="$name=$(( (t - t0) / 100 )) "
  done
  rm -f "$err"
  echo "$out"
}

# ---------------------------------------------------------------- statistics (values in tenths of a millisecond)
fmt() { printf '%d.%d' $(( $1 / 10 )) $(( $1 % 10 )); }

# prints: min median mean max
stats() { # <values separated by spaces>
  local sorted n sum=0 v med
  # shellcheck disable=SC2207
  sorted=($(printf '%s\n' $1 | sort -n))
  n=${#sorted[@]}
  for v in "${sorted[@]}"; do sum=$(( sum + v )); done
  if (( n % 2 == 1 )); then
    med=${sorted[n/2]}
  else
    med=$(( (sorted[n/2 - 1] + sorted[n/2]) / 2 ))
  fi
  echo "${sorted[0]} $med $(( sum / n )) ${sorted[n-1]}"
}

declare -A SAMPLES   # "<variant>:<milestone>" -> values

measure() { # <label> <executable>
  local label="$1" exe="$2" i result pair
  [ -x "$exe" ] || die "$exe is not executable"
  echo "[$label] $WARMUP warm-up launch(es), then $RUNS measured"
  for (( i = 1; i <= WARMUP + RUNS; i++ )); do
    result="$(run_once "$exe")" || die "launch $i of '$label' failed"
    if (( i > WARMUP )); then
      for pair in $result; do
        SAMPLES["$label:${pair%%=*}"]+="${pair#*=} "
      done
      printf '  run %2d: first-idle %s ms\n' $(( i - WARMUP )) "$(fmt "${result##*first-idle=}")"
    fi
  done
}

if [ -n "$APP" ]; then
  if [ -d "$APP" ]; then APP="$APP/wCalculator"; fi
  VARIANTS=(app)
  measure app "$APP"
else
  for v in "${VARIANTS[@]}"; do publish_variant "$v"; done
  for v in "${VARIANTS[@]}"; do measure "$v" "$BENCH/$v/wCalculator"; done
fi

# ---------------------------------------------------------------- report
echo
echo "Startup of wCalculator ($RID, $RUNS launches per variant), milliseconds since the process was started"
printf '%-8s %-19s %9s %9s %9s %9s\n' variant milestone min median mean max
for v in "${VARIANTS[@]}"; do
  for m in "${MILESTONES[@]}"; do
    [ -n "${SAMPLES[$v:$m]:-}" ] || continue
    read -r mn md me mx <<<"$(stats "${SAMPLES[$v:$m]}")"
    printf '%-8s %-19s %9s %9s %9s %9s\n' "$v" "$m" "$(fmt "$mn")" "$(fmt "$md")" "$(fmt "$me")" "$(fmt "$mx")"
  done
  echo
done

if [ "${#VARIANTS[@]}" = "2" ]; then
  read -r _ med_a _ _ <<<"$(stats "${SAMPLES[${VARIANTS[0]}:first-idle]}")"
  read -r _ med_b _ _ <<<"$(stats "${SAMPLES[${VARIANTS[1]}:first-idle]}")"
  if [ "$med_b" -gt 0 ]; then
    ratio=$(( med_a * 100 / med_b ))
    printf 'Median time to a usable window: %s (%s ms) vs %s (%s ms): %d.%02d times as fast\n' \
      "${VARIANTS[0]}" "$(fmt "$med_a")" "${VARIANTS[1]}" "$(fmt "$med_b")" $(( ratio / 100 )) $(( ratio % 100 ))
  fi
fi
