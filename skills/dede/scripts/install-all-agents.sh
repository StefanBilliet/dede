#!/usr/bin/env bash

set -euo pipefail

repo_root="${1:-$(pwd)}"
skill_source="${repo_root}/skills/dede"

if [[ ! -f "${skill_source}/SKILL.md" ]]; then
  printf 'Could not find dede skill under %s\n' "${skill_source}" >&2
  exit 1
fi

link_skill() {
  local source="$1"
  local destination="$2"
  mkdir -p "$(dirname "${destination}")"
  ln -sfn "${source}" "${destination}"
  printf 'Linked %s -> %s\n' "${destination}" "${source}"
}

link_skill "${skill_source}" "${repo_root}/.github/skills/dede"
link_skill "${skill_source}" "${repo_root}/.claude/skills/dede"
link_skill "${skill_source}" "${repo_root}/.opencode/skills/dede"
link_skill "${skill_source}" "${repo_root}/.agents/skills/dede"

link_skill "${skill_source}" "${HOME}/.codex/skills/dede"
link_skill "${skill_source}" "${HOME}/.claude/skills/dede"
link_skill "${skill_source}" "${HOME}/.copilot/skills/dede"
link_skill "${skill_source}" "${HOME}/.config/opencode/skills/dede"

printf '\nRestart Codex, Claude Code, GitHub Copilot, and OpenCode if they were already running.\n'
