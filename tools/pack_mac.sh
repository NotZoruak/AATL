#!/usr/bin/env bash
#
# macOS 打包脚本（tools/pack.ps1 的 macOS 等价物）。
# 将 `dotnet publish` 产物与游戏资源（assets/interface.json + assets/resource）
# 组装成一个可直接运行的文件夹 MATR-<版本>-<RID>/，内含原生可执行文件 ./MATR。
#
# 用法：
#   tools/pack_mac.sh [版本] [--self-contained] [--zip]
#     版本            输出目录/压缩包名中的版本号（默认 dev）
#     --self-contained 打包自包含 .NET 运行时（免装/免设 DOTNET_ROOT，体积更大）
#     --zip           额外产出 MATR-<版本>-<RID>.zip
# 环境变量：
#   RID     目标运行时（默认 osx-arm64，可设 osx-x64）
#   DOTNET  dotnet 可执行文件（默认 dotnet；SDK 装在 ~/.dotnet 时设 DOTNET="$HOME/.dotnet/dotnet"）
#
set -euo pipefail

VERSION="dev"
SELF_CONTAINED=false
ZIP=false
for arg in "$@"; do
    case "$arg" in
        --self-contained) SELF_CONTAINED=true ;;
        --zip) ZIP=true ;;
        -*) echo "未知选项：$arg" >&2; exit 2 ;;
        *) VERSION="$arg" ;;
    esac
done

RID="${RID:-osx-arm64}"
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSPROJ="$ROOT/_src/MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj"
PUBLISH_DIR="$ROOT/_src/bin/AnyCPU/Release/$RID/publish"
OUT="$ROOT/MATR-$VERSION-$RID"

command -v "$DOTNET" >/dev/null 2>&1 || { echo "找不到 dotnet（可用 DOTNET=/path/to/dotnet 指定）" >&2; exit 1; }

echo "==> 发布 ${RID}（self-contained=${SELF_CONTAINED}）…"
rm -rf "$PUBLISH_DIR"
"$DOTNET" publish "$CSPROJ" -r "$RID" -c Release --self-contained "$SELF_CONTAINED"

echo "==> 组装 $OUT …"
rm -rf "$OUT"; mkdir -p "$OUT"
cp -R "$PUBLISH_DIR/." "$OUT/"
# 移除运行期生成的目录（若上次在 publish 目录跑过）
rm -rf "$OUT/config" "$OUT/debug" "$OUT/temp" "$OUT/backup"

# 铺入游戏资源（不在 publish 产物中，需手动拷贝，与 pack.ps1 一致）
mkdir -p "$OUT/assets"
cp "$ROOT/assets/interface.json" "$OUT/assets/interface.json"
cp -R "$ROOT/assets/resource" "$OUT/assets/resource"
# 剔除开发期/个人数据资源子目录（与 pack.ps1 一致）
rm -rf "$OUT/assets/resource/config" "$OUT/assets/resource/temp" \
       "$OUT/assets/resource/backup" "$OUT/assets/resource/base/image/unused"

# 代理二进制在运行期从 libs/MaaAgentBinary 解析；若同时存在根部冗余副本则移除
[ -d "$OUT/MaaAgentBinary" ] && [ -d "$OUT/libs/MaaAgentBinary" ] && rm -rf "$OUT/MaaAgentBinary"

cp "$ROOT/README.md" "$OUT/" 2>/dev/null || true
cp "$ROOT/LICENSE" "$OUT/" 2>/dev/null || true
chmod +x "$OUT/MATR" 2>/dev/null || true

echo "==> 完成：$OUT"
if [ "$SELF_CONTAINED" = false ]; then
    echo "    框架依赖构建：需要已安装 .NET 10 运行时。"
    echo "    dotnet 不在 PATH 时可用： DOTNET_ROOT=\"\$HOME/.dotnet\" \"$OUT/MATR\""
    echo "    或运行 \"$OUT/DependencySetup_依赖库安装_mac.sh\" 安装 .NET 10 运行时。"
else
    echo "    自包含构建：直接运行 →  \"$OUT/MATR\""
fi

if [ "$ZIP" = true ]; then
    echo "==> 压缩…"
    ( cd "$ROOT" && rm -f "MATR-$VERSION-$RID.zip" \
        && ditto -c -k --sequesterRsrc --keepParent "$OUT" "MATR-$VERSION-$RID.zip" )
    echo "    压缩包：$ROOT/MATR-$VERSION-$RID.zip"
fi
