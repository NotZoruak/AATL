#!/usr/bin/env bash
#
# macOS 打包脚本（tools/pack.ps1 的 macOS 等价物）。
# 将 `dotnet publish` 产物与游戏资源（assets/interface.json + assets/resource）
# 组装成可运行的输出：
#   - 默认：文件夹 MATR-<版本>-<RID>/（内含原生可执行文件 ./MATR）
#   - --app：双击即用的 MATR.app 应用包（自动自包含，用户无需安装 .NET）
#
# 用法：
#   tools/pack_mac.sh [版本] [--self-contained] [--app] [--zip]
#     版本            输出名中的版本号（默认 dev）
#     --self-contained 打包自包含 .NET 运行时（免装/免设 DOTNET_ROOT，体积更大）
#     --app           产出 MATR.app（隐含 --self-contained，除非显式框架依赖）
#     --zip           额外产出压缩包
# 环境变量：
#   RID     目标运行时（默认 osx-arm64，可设 osx-x64）
#   DOTNET  dotnet 可执行文件（默认 dotnet；SDK 装在 ~/.dotnet 时设 DOTNET="$HOME/.dotnet/dotnet"）
#
set -euo pipefail

VERSION="dev"
SELF_CONTAINED=false
SELF_CONTAINED_SET=false
APP=false
ZIP=false
for arg in "$@"; do
    case "$arg" in
        --self-contained) SELF_CONTAINED=true; SELF_CONTAINED_SET=true ;;
        --framework-dependent) SELF_CONTAINED=false; SELF_CONTAINED_SET=true ;;
        --app) APP=true ;;
        --zip) ZIP=true ;;
        -*) echo "未知选项：$arg" >&2; exit 2 ;;
        *) VERSION="$arg" ;;
    esac
done
# .app 面向"下载即用"，默认自包含（除非用户显式选择框架依赖）
if [ "$APP" = true ] && [ "$SELF_CONTAINED_SET" = false ]; then
    SELF_CONTAINED=true
fi

RID="${RID:-osx-arm64}"
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSPROJ="$ROOT/_src/MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj"
PUBLISH_DIR="$ROOT/_src/bin/AnyCPU/Release/$RID/publish"
OUT="$ROOT/MATR-$VERSION-$RID"
LOGO="$ROOT/assets/resource/logo/MATR.png"

command -v "$DOTNET" >/dev/null 2>&1 || { echo "找不到 dotnet（可用 DOTNET=/path/to/dotnet 指定）" >&2; exit 1; }

echo "==> 发布 ${RID}（self-contained=${SELF_CONTAINED}）…"
rm -rf "$PUBLISH_DIR"
"$DOTNET" publish "$CSPROJ" -r "$RID" -c Release --self-contained "$SELF_CONTAINED"

# 把 publish 产物 + 资源组装到 $stage
stage_payload() {
    local dest="$1"
    mkdir -p "$dest"
    cp -R "$PUBLISH_DIR/." "$dest/"
    rm -rf "$dest/config" "$dest/debug" "$dest/temp" "$dest/backup"
    mkdir -p "$dest/assets"
    cp "$ROOT/assets/interface.json" "$dest/assets/interface.json"
    cp -R "$ROOT/assets/resource" "$dest/assets/resource"
    rm -rf "$dest/assets/resource/config" "$dest/assets/resource/temp" \
           "$dest/assets/resource/backup" "$dest/assets/resource/base/image/unused"
    [ -d "$dest/MaaAgentBinary" ] && [ -d "$dest/libs/MaaAgentBinary" ] && rm -rf "$dest/MaaAgentBinary"
    cp "$ROOT/README.md" "$dest/" 2>/dev/null || true
    cp "$ROOT/LICENSE" "$dest/" 2>/dev/null || true
    chmod +x "$dest/MATR" 2>/dev/null || true
}

# 从人类可读版本号推导合法的 macOS Bundle 版本字段。
# CFBundleShortVersionString / CFBundleVersion 只接受"点分非负整数"（如 0.12.2），
# 因此去掉前导 v、预发布后缀（-beta / -rc 等）与构建元数据；非法则回退到 0.0.0。
sanitize_version() {
    local v="$1"
    v="${v#[vV]}"      # 去掉前导 v/V（v0.12.2 → 0.12.2）
    v="${v%%-*}"       # 去掉 -beta / -rc1 等预发布后缀
    v="${v%%+*}"       # 去掉 +build 构建元数据
    if [[ "$v" =~ ^[0-9]+(\.[0-9]+){0,2}$ ]]; then
        printf '%s' "$v"
    else
        printf '0.0.0'  # dev 等非数字版本回退到合法占位值
    fi
}

# 由 PNG 生成 .icns（缺 logo 时跳过）
make_icns() {
    local src="$1" dst="$2"
    [ -f "$src" ] || return 1
    local tmp iconset
    tmp="$(mktemp -d)"; iconset="$tmp/MATR.iconset"; mkdir -p "$iconset"
    local sz
    for sz in 16 32 64 128 256 512; do
        sips -z "$sz" "$sz" "$src" --out "$iconset/icon_${sz}x${sz}.png" >/dev/null 2>&1
        sips -z "$((sz*2))" "$((sz*2))" "$src" --out "$iconset/icon_${sz}x${sz}@2x.png" >/dev/null 2>&1
    done
    iconutil -c icns "$iconset" -o "$dst" >/dev/null 2>&1
    local rc=$?; rm -rf "$tmp"; return $rc
}

if [ "$APP" = false ]; then
    echo "==> 组装 $OUT …"
    rm -rf "$OUT"
    stage_payload "$OUT"
    RESULT="$OUT"
else
    APPDIR="$ROOT/MATR.app"
    echo "==> 组装应用包 $APPDIR …"
    rm -rf "$APPDIR"
    mkdir -p "$APPDIR/Contents/MacOS" "$APPDIR/Contents/Resources"
    stage_payload "$APPDIR/Contents/MacOS"
    PLIST_VERSION="$(sanitize_version "$VERSION")"
    echo "    Bundle 版本：${PLIST_VERSION}（源版本：${VERSION}）"
    if make_icns "$LOGO" "$APPDIR/Contents/Resources/MATR.icns"; then
        ICON_LINE="    <key>CFBundleIconFile</key><string>MATR</string>"
    else
        echo "    （未找到 logo，跳过图标）"; ICON_LINE=""
    fi
    cat > "$APPDIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>MATR</string>
    <key>CFBundleDisplayName</key><string>MATR</string>
    <key>CFBundleIdentifier</key><string>com.notzoruak.matr</string>
    <key>CFBundleVersion</key><string>${PLIST_VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${PLIST_VERSION}</string>
    <key>CFBundleExecutable</key><string>MATR</string>
${ICON_LINE}
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
</dict>
</plist>
PLIST
    RESULT="$APPDIR"
fi

echo "==> 完成：$RESULT"
if [ "$APP" = true ]; then
    if [ "$SELF_CONTAINED" = true ]; then
        echo "    双击 MATR.app 即可运行（自包含，无需安装 .NET）。"
    else
        echo "    双击 MATR.app 运行；框架依赖，需已安装 .NET 10 运行时。"
    fi
    echo "    首次打开若被 Gatekeeper 拦截：右键 → 打开（未签名/未公证的正常提示），"
    echo "    或执行： xattr -dr com.apple.quarantine \"$RESULT\""
elif [ "$SELF_CONTAINED" = false ]; then
    echo "    框架依赖构建：需已安装 .NET 10 运行时。"
    echo "    dotnet 不在 PATH 时： DOTNET_ROOT=\"\$HOME/.dotnet\" \"$RESULT/MATR\""
else
    echo "    自包含：直接运行 →  \"$RESULT/MATR\""
fi

if [ "$ZIP" = true ]; then
    ZIP_NAME="MATR-$VERSION-$RID.zip"
    echo "==> 压缩 $ZIP_NAME …"
    ( cd "$ROOT" && rm -f "$ZIP_NAME" \
        && ditto -c -k --sequesterRsrc --keepParent "$RESULT" "$ZIP_NAME" )
    echo "    压缩包：$ROOT/$ZIP_NAME"
fi
