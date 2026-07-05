# FileSync

Een eenvoudige Dropbox-variant: client-server bestandssynchronisatie via een centrale
server, gebouwd op kale TCP-sockets (`System.Net.Sockets`) in C# / .NET 8 — zonder
HTTP-frameworks of bestaande sync-libraries. Schoolproject voor het vak Systems &
Security.

## Projectstructuur

| Project | Inhoud |
|---|---|
| `FileSync.Shared` | Protocolparser/-writer, padvalidatie, SHA-256-hashing, manifest-(de)serialisatie. Geen socketcode — volledig unit-testbaar. |
| `FileSync.Server` | `TcpListener` met een eigen Task per verbinding, commandhandlers, per-pad write-locking (423 bij conflict), streaming `.part`-upload met atomische rename. |
| `FileSync.Client` | Syncmap scannen, lokale hash-cache, sync-cyclus (`MANIFEST` → diff → `UPLOAD`/`DOWNLOAD`/`DELETE`), hervatting na netwerkstoring, poll-loop. |
| `FileSync.Tests` | 90 NUnit-tests: protocolparser, padvalidatie, upload/resume/dedup-logica, client-side diff-beslissingen. |

Het protocol zelf (`SYNC/1.0`) staat normatief beschreven in [`PROTOCOL.md`](PROTOCOL.md).
De opdrachtregels en acceptatiecriteria staan in [`CLAUDE.md`](CLAUDE.md).

## Bouwen en testen

```
dotnet build FileSync.sln
dotnet test FileSync.sln
```

## Draaien

```
FileSync.Server.exe --port 4711 --storage <opslagmap>
FileSync.Client.exe --host <server-ip> --port 4711 --folder <syncmap> --interval 5 --client-id <naam>
```

Zelfstandige, self-contained executables publiceren (geen .NET-installatie nodig op de
machine die ze draait):

```
dotnet publish FileSync.Server/FileSync.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/server/win-x64
dotnet publish FileSync.Client/FileSync.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/client/win-x64
```

## Testdocumentatie

- [`TESTING.md`](TESTING.md) — overzicht van de geautomatiseerde tests en de resultaten
  van de handmatige acceptatietests zoals die al één keer lokaal zijn doorlopen.
- [`DRAAIBOEK.md`](DRAAIBOEK.md) — stap-voor-stap script om diezelfde 7 acceptatietests
  zelf te herhalen, bij voorkeur op twee fysiek gescheiden machines.

## Belangrijkste protocol-eigenschappen

- Strikt request-response over TCP, standaardpoort 4711.
- Bestanden groter dan 4 GB: alles streamend in blokken van 64 KiB, 64-bits lengtes/offsets.
- Nooit een half bestand zichtbaar: uploads gaan naar `<pad>.part`, pas na een correcte
  SHA-256-verificatie volgt een atomische rename naar het doelpad.
- Deduplicatie op inhoud (SHA-256), niet op naam of tijdstip.
- Per-pad schrijfvergrendeling: een tweede gelijktijdige schrijver op hetzelfde pad krijgt
  direct `423 Locked`, zonder te wachten.
