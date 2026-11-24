# Запуск ETS на эмуляторе Android без телефона.
#
# Требует: SDK эмулятора в %LOCALAPPDATA%\Android\Sdk (emulator, platform-tools,
# system-images;android-34;google_apis;x86_64), AVD «ETS_Phone» и APK из
# Tools/Android/Build Emulator APK (Builds/Android/ETS-emulator.apk).
# Подробности — docs/MobilePlan.md, Фаза 9.
#
#   powershell -ExecutionPolicy Bypass -File tools\run_android_emulator.ps1 [-Headless]

param(
    [switch]$Headless,
    [string]$Apk = "Builds\Android\ETS-emulator.apk"
)

$ErrorActionPreference = "Stop"
$sdk = Join-Path $env:LOCALAPPDATA "Android\Sdk"
$emulator = Join-Path $sdk "emulator\emulator.exe"
$adb = Join-Path $sdk "platform-tools\adb.exe"
$package = "com.havocstudioteam.ets"
$logDir = "Logs\AndroidPort"

if (-not (Test-Path $Apk)) { throw "Нет $Apk — собери Tools/Android/Build Emulator APK." }
New-Item -ItemType Directory -Force $logDir | Out-Null

$env:ANDROID_SDK_ROOT = $sdk
$emulatorArgs = @("-avd", "ETS_Phone", "-no-snapshot-save", "-no-boot-anim")
if ($Headless) { $emulatorArgs += @("-no-window", "-no-audio", "-gpu", "swiftshader_indirect") }

Write-Host "Запускаю эмулятор ETS_Phone..."
Start-Process -FilePath $emulator -ArgumentList $emulatorArgs -RedirectStandardOutput "$logDir\emulator.log" -RedirectStandardError "$logDir\emulator-err.log"

& $adb wait-for-device
do { Start-Sleep -Seconds 3; $boot = (& $adb shell getprop sys.boot_completed).Trim() } until ($boot -eq "1")
Write-Host "Android загрузился."

& $adb install -r $Apk
& $adb logcat -c
& $adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
Write-Host "ETS запущена. Лог Unity: $logDir\logcat-unity.txt"

Start-Sleep -Seconds 25
& $adb logcat -d -s Unity:V AndroidRuntime:E CRASH:E DEBUG:V > "$logDir\logcat-unity.txt"
& $adb exec-out screencap -p > "$logDir\emulator-screen.png"
Write-Host "Скриншот: $logDir\emulator-screen.png"
