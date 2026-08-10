#!/usr/bin/env bash
#
# Provision an Oracle Cloud (or any Linux ARM64/x64) box to run the long measurement
# workloads — Stryker mutation testing and SharpFuzz fuzzing — off the dev machine.
#
# WHY THIS EXISTS
#   Stryker takes ~33 minutes and holds obj/ for the duration, so running it locally
#   blocks the machine and makes it a pre-merge gate nobody wants to wait for. On a
#   dedicated box it becomes a signal that arrives afterwards rather than a gate.
#   Fuzzing has the same shape: it wants hours, not minutes.
#
# WHAT THIS BOX CANNOT DO
#   - Windows UI tests. No Windows licence on the free tier, and WinAppDriver needs an
#     interactive desktop session. That workload stays local.
#   - Benchmarks. Shared cloud vCPUs are too noisy for BenchmarkDotNet. The NoteDiff
#     MaxLines numbers only meant something because the hardware was stable.
#
# NOT A GITHUB SELF-HOSTED RUNNER, deliberately. See docs/memory/project_self_hosted_runners.md:
# a public repo with pull_request triggers means stranger-authored workflow code runs on
# the runner. SSH in, or let cron pull. No webhook.
#
# Usage:  bash provision-measurement-box.sh [repo-url]
set -euo pipefail

REPO_URL="${1:-https://github.com/PinKushin/PokemonBattleJournal.git}"
WORKDIR="${HOME}/pbj"

echo "==> Architecture: $(uname -m)   (aarch64 = Ampere A1)"

# ---------------------------------------------------------------------------
# 1. System packages
#    clang is for libfuzzer-dotnet, which is distributed as SOURCE. A prebuilt
#    binary from a third party is exactly the supply-chain risk this repo's
#    dependency policy declines elsewhere — and the x86-64 one from a dev machine
#    will not run here anyway.
# ---------------------------------------------------------------------------
echo "==> Installing system packages"
sudo apt-get update -qq
# TWO STEPS, and it has to be two. Naming the runtime package requires knowing clang's major
# version, and the only way to ask is `clang --version` — which does not exist yet on the first
# pass. A single apt-get line with that in a command substitution expands to the malformed
# "libclang-rt--dev", the whole line fails, and an `|| fallback` then installs the runtime
# WITHOUT clang. That failed silently on the fuzz box: exit 0, and the build died 80 lines later
# with "clang: command not found".
sudo apt-get install -y --no-install-recommends clang git curl ca-certificates

# libclang-rt-*-dev supplies libclang_rt.fuzzer-<arch>.a. Ubuntu's clang package does NOT
# include the sanitizer runtime archives, so without this the libfuzzer-dotnet link fails with
# "cannot find .../libclang_rt.fuzzer-aarch64.a" and the script dies before the smoke test.
CLANG_MAJOR="$(clang --version | grep -oE 'version [0-9]+' | grep -oE '[0-9]+' | head -1)"
echo "==> clang major version: ${CLANG_MAJOR}"
sudo apt-get install -y --no-install-recommends "libclang-rt-${CLANG_MAJOR}-dev"

# ---------------------------------------------------------------------------
# 2. .NET 10 SDK
#    Installed to $HOME rather than system-wide so DOTNET_ROOT is unambiguous.
#    Every project in the Stryker path is plain net10.0, but that is NOT enough to
#    avoid the MAUI workloads — see 2b below for why.
# ---------------------------------------------------------------------------
if [ ! -x "${HOME}/.dotnet/dotnet" ]; then
  echo "==> Installing .NET 10 SDK"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0
fi

# DOTNET_ROOT is REQUIRED, not optional. Without it the sharpfuzz apphost cannot find
# the runtime sitting in ~/.dotnet/shared and fails with a misleading
# "You must install .NET to run this application". Cost real time in WSL.
export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"

if ! grep -q "DOTNET_ROOT" "${HOME}/.bashrc"; then
  {
    echo ''
    echo '# .NET — DOTNET_ROOT is required for tool apphosts to find the runtime'
    echo 'export DOTNET_ROOT="$HOME/.dotnet"'
    echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"'
  } >> "${HOME}/.bashrc"
  echo "==> Added DOTNET_ROOT and PATH to ~/.bashrc"
fi

dotnet --version

# ---------------------------------------------------------------------------
# 2b. MAUI workloads
#     Needed even though every project in the measurement path is plain net10.0.
#     PokemonBattleJournal.Tests has a ProjectReference to the multi-targeted app head, and
#     NETSDK1147 fires at EVALUATION — merely evaluating the net10.0-android TFM demands the
#     workload, regardless of which TFM is actually consumed. ~30s, once.
# ---------------------------------------------------------------------------
echo "==> Installing MAUI workloads (maui-tizen, android)"
dotnet workload install maui-tizen android 2>&1 | tail -2

# ---------------------------------------------------------------------------
# 3. Repo
# ---------------------------------------------------------------------------
if [ ! -d "${WORKDIR}/.git" ]; then
  echo "==> Cloning ${REPO_URL}"
  git clone --quiet "${REPO_URL}" "${WORKDIR}"
else
  echo "==> Updating existing clone"
  git -C "${WORKDIR}" fetch --quiet origin
  git -C "${WORKDIR}" reset --quiet --hard origin/master
fi
cd "${WORKDIR}"

# ---------------------------------------------------------------------------
# 4. Stryker
#    Pinned in .config/dotnet-tools.json (4.16.0, rollForward false), so restore
#    rather than install — the pin is deliberate.
# ---------------------------------------------------------------------------
echo "==> Restoring dotnet tools (Stryker)"
dotnet tool restore

# ---------------------------------------------------------------------------
# 5. Fuzzing toolchain
#    SharpFuzz instruments IL, so that half is architecture-agnostic. The
#    libfuzzer-dotnet bridge is native C++ and MUST be built for this machine's
#    architecture — copying the x86-64 binary from a dev box does not work.
# ---------------------------------------------------------------------------
echo "==> Installing SharpFuzz.CommandLine"
dotnet tool install --global SharpFuzz.CommandLine 2>/dev/null || \
  dotnet tool update --global SharpFuzz.CommandLine

if [ ! -x "${HOME}/libfuzzer-dotnet" ]; then
  echo "==> Building libfuzzer-dotnet for $(uname -m)"
  curl -sSL -o /tmp/libfuzzer-dotnet.cc \
    https://raw.githubusercontent.com/Metalnem/libfuzzer-dotnet/master/libfuzzer-dotnet.cc
  clang -fsanitize=fuzzer /tmp/libfuzzer-dotnet.cc -o "${HOME}/libfuzzer-dotnet"
fi

# ---------------------------------------------------------------------------
# 6. Smoke test — build and run the fast suites. If MAUI package references on the
#    test project need a workload, this is where it surfaces, before anything long.
# ---------------------------------------------------------------------------
echo "==> Smoke test: build + unit tests"
dotnet build PokemonBattleJournal.Core/PokemonBattleJournal.Core.csproj --nologo -v q
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --nologo -v q

cat <<'DONE'

==> Provisioned.

Next:
  bash build/run-measurements.sh stryker-core   # ~33 min, expect ~57.96% at 2026-08-09
  bash build/run-measurements.sh stryker-scraper
  bash build/run-measurements.sh fuzz 300

Verify the Core score reproduces before automating anything. If it differs materially
from the local x64 figure, that is worth understanding rather than accepting — the
projects are plain net10.0 and should not care about architecture.
DONE
