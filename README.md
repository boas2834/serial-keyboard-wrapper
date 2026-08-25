# Serial HID Wrapper

Serial HID Wrapper ist eine kleine Windows-Tray-Anwendung, die Text von einem seriell angeschlossenen Barcodescanner liest und wie Tastatureingaben an das aktuell aktive Fenster sendet.

> Die Anwendung installiert keinen HID-Treiber und erscheint daher nicht als separates Eingabegerät im Windows-Gerätemanager. Die Ausgabe erfolgt über die Windows-API `SendInput`.

## Bedienung

1. Die Anwendung starten und das Tray-Symbol öffnen.
2. Unter **Einstellungen** COM-Port, Baudrate, Datenbits, Parität und Stoppbits des Scanners wählen.
3. Scan-Timeout und gewünschtes Suffix (`Enter`, `Tab` oder keines) einstellen.
4. **Verbinden** wählen und anschließend das gewünschte Eingabefeld aktivieren.

Ein Scan endet bei `CR`, `LF`, `CR+LF` oder nach dem konfigurierten Timeout ohne weitere serielle Daten. Wird der Scanner getrennt, versucht die Anwendung alle zwei Sekunden, den gespeicherten Port erneut zu öffnen.

Die Tray-Funktion **Ausgabe pausieren** verwirft Scans, solange sie aktiv ist. Auch während das Tray-Menü oder der Einstellungsdialog geöffnet ist, wird keine Tastatureingabe erzeugt.

## Datenschutz und Einschränkungen

- Barcodeinhalte werden nicht protokolliert oder gespeichert.
- Die Statusanzeige enthält nur Zeit und Zeichenanzahl des letzten ausgegebenen Scans.
- Windows blockiert simulierte Eingaben in Programme, die mit höheren Rechten als Serial HID Wrapper laufen.
- Der serielle Text wird als ASCII interpretiert. Die Tastaturausgabe selbst verwendet Unicode.

## Entwicklung

Voraussetzung ist das .NET 8 SDK für Windows.

```powershell
dotnet restore SerialHidWrapper.sln
dotnet test SerialHidWrapper.sln -c Release
dotnet publish src\SerialHidWrapper\SerialHidWrapper.csproj -c Release -r win-x64 --self-contained true -o artifacts\publish
```

Die portable Einzeldatei wird als `artifacts\publish\SerialHidWrapper.exe` erzeugt.
