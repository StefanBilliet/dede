#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: run-dede.sh [--repo-root <path>] <dede-command> [args...]

Examples:
  run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer scan /path/to/workspace -o /path/to/graph.json
  run-dede.sh serve /path/to/graph.json --url http://127.0.0.1:5057

Environment:
  DEDE_REPO_ROOT  Optional repository root for DogEatDog.DependencyExplorer.
  DOTNET_ROOT     Optional .NET SDK root. If set, DOTNET_ROOT/dotnet is tried first.
EOF
}

find_dotnet() {
  if [[ -n "${DOTNET_ROOT:-}" && -x "${DOTNET_ROOT}/dotnet" ]]; then
    printf '%s\n' "${DOTNET_ROOT}/dotnet"
    return 0
  fi

  if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    printf '%s\n' "${HOME}/.dotnet/dotnet"
    return 0
  fi

  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return 0
  fi

  return 1
}

find_repo_root() {
  if [[ -n "${DEDE_REPO_ROOT:-}" ]]; then
    if [[ -f "${DEDE_REPO_ROOT}/DogEatDog.DependencyExplorer.sln" ]]; then
      printf '%s\n' "${DEDE_REPO_ROOT}"
      return 0
    fi

    printf 'DEDE_REPO_ROOT is set but does not point to a DogEatDog.DependencyExplorer repository: %s\n' "${DEDE_REPO_ROOT}" >&2
    return 1
  fi

  local current="${PWD}"
  while [[ "${current}" != "/" ]]; do
    if [[ -f "${current}/DogEatDog.DependencyExplorer.sln" ]]; then
      printf '%s\n' "${current}"
      return 0
    fi

    current="$(dirname "${current}")"
  done

  return 1
}

repo_root=""

if [[ $# -eq 0 ]]; then
  usage >&2
  exit 1
fi

if [[ "${1:-}" == "--repo-root" ]]; then
  if [[ $# -lt 3 ]]; then
    usage >&2
    exit 1
  fi

  repo_root="$2"
  shift 2
fi

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if ! dotnet_bin="$(find_dotnet)"; then
  printf 'Unable to find dotnet. Install the .NET SDK or set DOTNET_ROOT.\n' >&2
  exit 1
fi

if [[ -z "${repo_root}" ]]; then
  if ! repo_root="$(find_repo_root)"; then
    cat >&2 <<'EOF'
Unable to locate the DogEatDog.DependencyExplorer repository root.
Pass --repo-root /path/to/DogEatDog.DependencyExplorer or set DEDE_REPO_ROOT.
EOF
    exit 1
  fi
fi

cli_project="${repo_root}/src/DogEatDog.DependencyExplorer.Cli"
if [[ ! -d "${cli_project}" ]]; then
  printf 'Unable to locate CLI project under %s\n' "${cli_project}" >&2
  exit 1
fi

exec "${dotnet_bin}" run --project "${cli_project}" -- "$@"
