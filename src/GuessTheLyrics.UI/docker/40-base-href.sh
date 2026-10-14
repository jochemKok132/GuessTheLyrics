#!/bin/sh
# Runs at container start (nginx image entrypoint): sets <base href> from BASE_PATH so the same image
# works at the root of a domain or under a path such as /guessthelyrics/.
set -e

BASE_PATH="${BASE_PATH:-/}"
case "$BASE_PATH" in /*) ;; *) BASE_PATH="/$BASE_PATH" ;; esac
case "$BASE_PATH" in */) ;; *) BASE_PATH="$BASE_PATH/" ;; esac

INDEX=/usr/share/nginx/html/index.html
sed -i "s|<base href=\"[^\"]*\" */>|<base href=\"$BASE_PATH\" />|" "$INDEX"

# Pre-compressed copies would still contain the old base href.
rm -f "$INDEX.gz" "$INDEX.br"

echo "GuessTheLyrics UI: base href set to $BASE_PATH"
