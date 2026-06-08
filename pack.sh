#!/bin/bash
# AATL 发布打包脚本
# 用法: ./pack.sh [版本号]
# 示例: ./pack.sh v0.4   → 生成 AATL-v0.4.zip

set -euo pipefail

VERSION="${1:-}"
if [ -z "$VERSION" ]; then
    VERSION=$(git describe --tags --abbrev=0 2>/dev/null || echo "dev")
fi

ZIP_NAME="AATL-${VERSION}.zip"
TMP_FILE=$(mktemp)
cleanup() { rm -f "$TMP_FILE"; }
trap cleanup EXIT

# 仅开发/调试用的文件，不进入发布包
EXCLUDE=("CLAUDE.md" ".gitignore" "开发日志.md" "resource/base/pipeline/sample.json")

echo "==> 收集发布文件列表（排除仅开发用文件）..."

git ls-files -z | {
    while IFS= read -r -d '' path; do
        skip=false
        for p in "${EXCLUDE[@]}"; do
            [[ "$path" == *"$p"* ]] && skip=true && break
        done
        [ "$skip" = false ] && printf '%s\0' "$path"
    done || true
} > "$TMP_FILE"

FILE_COUNT=$(tr '\0' '\n' < "$TMP_FILE" | wc -l)
echo "==> 共 ${FILE_COUNT} 个文件"

echo "==> 打包 ${ZIP_NAME} ..."
xargs -0 git archive --format=zip --output="${ZIP_NAME}" HEAD -- < "$TMP_FILE"

SIZE=$(du -h "${ZIP_NAME}" | cut -f1)
echo "==> 完成: ${ZIP_NAME} (${SIZE})"
