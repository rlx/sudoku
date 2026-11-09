#!/bin/bash
# Builds dist/Sudoku.apk from the web game in ../web, without Gradle.
# Needs: openjdk@21 (brew), Android SDK platform 36 + build-tools 37 in ~/Library/Android/sdk.
set -euo pipefail
cd "$(dirname "$0")"

export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@21}"
export PATH="$JAVA_HOME/bin:$PATH"
SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
BT="$SDK/build-tools/37.0.0"
JAR="$SDK/platforms/android-36/android.jar"
VERSION_CODE=1
VERSION_NAME=1.0

# Signing key. Keep release.keystore and keystore.properties safe and private:
# Android only accepts updates signed with the same key.
if [ ! -f release.keystore ]; then
  PASS=$(openssl rand -hex 16)
  keytool -genkeypair -keystore release.keystore -alias sudoku -keyalg RSA -keysize 3072 \
    -validity 20000 -storepass "$PASS" -keypass "$PASS" -dname "CN=Sudoku" >/dev/null
  echo "STORE_PASS=$PASS" > keystore.properties
  echo "Created new signing key: android/release.keystore (back it up)"
fi
source keystore.properties

rm -rf build && mkdir -p build/assets/www build/gen build/classes build/dex build/res

# Game files
cp -R ../web/. build/assets/www/
rm -f build/assets/www/sudoku.apk build/assets/www/sw.js

# Launcher icons
for d in mdpi:48 hdpi:72 xhdpi:96 xxhdpi:144 xxxhdpi:192; do
  mkdir -p "build/res/mipmap-${d%%:*}"
  python3 ../tools/make_icon.py "build/res/mipmap-${d%%:*}/ic_launcher.png" "${d##*:}"
done
cp -R res/. build/res/

"$BT/aapt2" compile --dir build/res -o build/res.zip
"$BT/aapt2" link -I "$JAR" --manifest AndroidManifest.xml -o build/unsigned.apk \
  --java build/gen -A build/assets -R build/res.zip --auto-add-overlay \
  --min-sdk-version 24 --target-sdk-version 36 \
  --version-code $VERSION_CODE --version-name $VERSION_NAME

javac -source 17 -target 17 -Xlint:-options -classpath "$JAR" -d build/classes \
  $(find src build/gen -name '*.java')
"$BT/d8" --lib "$JAR" --min-api 24 --release --output build/dex $(find build/classes -name '*.class')
(cd build/dex && zip -q ../unsigned.apk classes.dex)

"$BT/zipalign" -f -p 4 build/unsigned.apk build/aligned.apk
mkdir -p ../dist
"$BT/apksigner" sign --v4-signing-enabled false --ks release.keystore --ks-key-alias sudoku --ks-pass "pass:$STORE_PASS" \
  --out ../dist/Sudoku.apk build/aligned.apk
"$BT/apksigner" verify ../dist/Sudoku.apk
echo "Built dist/Sudoku.apk"
