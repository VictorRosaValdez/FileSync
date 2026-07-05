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
| `FileSync.Tests` | 98 NUnit-tests: protocolparser, padvalidatie, upload/resume/dedup-logica, client-side diff-beslissingen, certificaatgeneratie/-pinning. |

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

## Extra uitdaging: TLS (versleutelde bestandsuitwisseling)

Optionele variant conform `PROTOCOL.md §8`: dezelfde applicatieprotocol, maar over een
`SslStream` in plaats van een kale `NetworkStream`, op een apart poortnummer (standaard
4712) naast de gewone poort 4711. De server genereert bij de eerste start automatisch een
zelfondertekend certificaat (privé `.pfx` + publiek `.cer`); dat publieke certificaat moet
je één keer naar elke client kopiëren, die het vervolgens expliciet vertrouwt (certificate
pinning op thumbprint — geen normale CA-keten, want die zou voor een zelfondertekend
certificaat toch altijd falen).

```
FileSync.Server.exe --port 4711 --tls-port 4712 --storage <opslagmap>
# → genereert server-cert.pfx (privé) en server-cert.cer (publiek) in de werkmap

# server-cert.cer kopiëren naar de clientmachine, dan:
FileSync.Client.exe --host <server-ip> --port 4712 --folder <syncmap> --server-cert server-cert.cer
```

Een client zonder `--server-cert` verbindt gewoon onversleuteld op poort 4711; een client
met het verkeerde certificaat krijgt een mislukte TLS-handshake (geen sync, geen data over
de lijn) — dit is expliciet getest, zie `TESTING.md`.

## Extra uitdaging: server in Docker

```
docker build -t filesync-server .
docker run -d --name filesync-server -p 4711:4711 -p 4712:4712 -v filesync-data:/data filesync-server
```

Optioneel met TLS en een cert die de containerherstart overleeft:
```
docker run -d --name filesync-server -p 4711:4711 -p 4712:4712 \
  -v filesync-data:/data \
  filesync-server --port 4711 --tls-port 4712 --storage /data --cert /data/server-cert.pfx --public-cert /data/server-cert.cer
```

Kopieer het publieke certificaat daarna uit de container naar de clientmachine:
```
docker cp filesync-server:/data/server-cert.cer .
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
