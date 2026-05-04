#!/usr/bin/env bash
# Install the pre-commit hook into .git/hooks/.
# Re-runnable safely.

set -e
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

cp tools/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit

echo "Installed pre-commit hook at .git/hooks/pre-commit"
echo "It blocks: dongle-ID patterns, German umlauts in .cs, failing tests"
echo "Bypass with:  git commit --no-verify"
