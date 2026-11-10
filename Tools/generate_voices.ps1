# Genera las voces de radio con la sintesis de voz de Windows.
# Lee el manifiesto que escribe Unity (hash<TAB>texto) y crea un .wav por frase que falte.
# Una grabacion real con el mismo nombre de archivo no se toca: solo se generan las que no existen.
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [Parameter(Mandatory = $true)][string]$OutputFolder
)

Add-Type -AssemblyName System.Speech

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice = $synth.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -like 'es*' } | Select-Object -First 1
if ($null -eq $voice) { throw 'No hay ninguna voz en espanol instalada.' }
$synth.SelectVoice($voice.VoiceInfo.Name)
$synth.Rate = 1

New-Item -ItemType Directory -Force $OutputFolder | Out-Null
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

$created = 0
$skipped = 0
foreach ($line in [IO.File]::ReadAllLines($Manifest, [Text.Encoding]::UTF8)) {
    if ($line.Trim().Length -eq 0) { continue }
    $parts = $line.Split("`t", 2)
    $path = Join-Path $OutputFolder ($parts[0] + '.wav')
    if (Test-Path $path) { $skipped++; continue }
    $synth.SetOutputToWaveFile($path, $format)
    $synth.Speak($parts[1])
    $synth.SetOutputToNull()
    $created++
}
$synth.Dispose()
"Voces creadas: $created, ya existentes: $skipped"
