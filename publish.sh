#!/bin/bash
# ParaTool — local publish script
# Usage: ./publish.sh [win-x64|linux-x64|all]

set -e

VERSION="${PARATOOL_VERSION:-dev}"
OUTDIR="publish"

publish_target() {
    local rid=$1
    local name="ParaTool-${rid}"
    echo "=== Publishing ${name} ==="

    dotnet publish ParaTool.App/ParaTool.App.csproj \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:PublishTrimmed=false \
        -o "${OUTDIR}/${name}"

    echo "  -> ${OUTDIR}/${name}/"

    # Archive
    if [[ "$rid" == win-* ]]; then
        if command -v zip &>/dev/null; then
            (cd "${OUTDIR}" && zip -r "../${name}-${VERSION}.zip" "${name}/")
            echo "  -> ${name}-${VERSION}.zip"
        fi
    else
        tar -czf "${name}-${VERSION}.tar.gz" -C "${OUTDIR}" "${name}/"
        echo "  -> ${name}-${VERSION}.tar.gz"
    fi
}

case "${1:-all}" in
    win-x64)   publish_target win-x64 ;;
    linux-x64) publish_target linux-x64 ;;
    all)
        publish_target win-x64
        publish_target linux-x64
        ;;
    *)
        echo "Usage: $0 [win-x64|linux-x64|all]"
        exit 1
        ;;
esac

echo ""
echo "Done! Artifacts in ${OUTDIR}/"
