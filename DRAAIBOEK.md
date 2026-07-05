# Draaiboek — FileSync handmatig testen

Dit is een stap-voor-stap script om de applicatie zelf te testen, bij voorkeur op twee
fysiek gescheiden machines (zoals de opdracht vereist). Alles wat hieronder staat, is al
één keer door mij lokaal doorlopen (zie `TESTING.md` voor die resultaten) — dit draaiboek
is bedoeld zodat jij het zelf, op je eigen machines, kunt herhalen en verifiëren.

## 0. Vereisten

- .NET 8 SDK op de machine(s) waar je bouwt/publiceert (`dotnet --version`).
- Op de machines waar je **alleen draait** (niet bouwt) is geen .NET nodig, mits je de
  self-contained gepubliceerde `.exe`'s gebruikt (stap 2).
- Beide machines op hetzelfde netwerk, en een vrije poort (standaard **4711**). Als er een
  Windows-firewall tussen zit, moet je op de servermachine inkomend TCP-verkeer op die
  poort toestaan (zie §6 Troubleshooting).

## 1. Code ophalen

```
git clone <pad-naar-deze-repo> FileSync
cd FileSync
```

Of: kopieer de repo-map naar de tweede machine (bv. via een USB-stick of netwerkshare) —
er is geen internetverbinding voor nodig, dit is puur lokaal netwerkverkeer.

## 2. Bouwen, testen en publiceren

Op de machine waar je bouwt:

```
dotnet build FileSync.sln
dotnet test FileSync.sln
```

Verwacht resultaat: build succeeded, **90 van de 90 tests** groen.

Publiceer daarna zelfstandige executables (geen IDE, geen losse .NET-installatie nodig
op de machine die ze draait):

```
dotnet publish FileSync.Server/FileSync.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/server/win-x64
dotnet publish FileSync.Client/FileSync.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/client/win-x64
```

(Gebruik `-r linux-x64` in plaats van `-r win-x64` als een van de machines Linux draait.)

Kopieer de map `publish/server/win-x64` naar de servermachine en `publish/client/win-x64`
naar elke werkplekmachine.

## 3. Server starten (machine 1)

```
FileSync.Server.exe --port 4711 --storage D:\filesync-server-data
```

Verwacht: een logregel `Server luistert op poort 4711, opslagmap '...'`. Laat dit venster
open staan — dit is de servercomponent.

Noteer het IP-adres van deze machine (`ipconfig` → IPv4-adres), dat heb je zo nodig.

## 4. Client(s) starten (machine 2, machine 3, ...)

```
FileSync.Client.exe --host <IP-van-machine-1> --port 4711 --folder D:\filesync-sync --interval 5 --client-id werkplek-a
```

Start op een tweede werkplekmachine dezelfde executable met een ander `--client-id` en
eigen `--folder`, bv. `werkplek-b`.

Verwacht: een logregel `Start synchronisatie van '...' met <ip>:4711 (elke 5s)`, gevolgd
door periodieke cycli. Op de servermachine verschijnt bij elke cyclus een regel als
`Nieuwe verbinding van ... -> HELLO ... -> MANIFEST ... -> BYE`.

## 5. De 7 acceptatietests

Doorloop ze in deze volgorde; elke test bouwt voort op de vorige toestand.

### Test 1 — Bestand propageert A → server → B
1. Zet een bestand in de syncmap van werkplek A (bv. `notitie.txt`).
2. Wacht één poll-interval (5s). Controleer in de servermap dat het bestand er staat.
3. Wacht nog een interval. Controleer dat het bestand nu ook in de syncmap van werkplek B
   staat, met identieke inhoud.
- ✅ Geslaagd als: bestand op alle drie de plekken byte-identiek is (vergelijk desnoods
  met `certutil -hashfile notitie.txt SHA256` op beide machines).

### Test 2 — Geen overdracht bij ongewijzigd bestand
1. Wacht, zonder iets te wijzigen, nog een aantal poll-cycli op werkplek A.
2. Kijk in het serverlog: er mag voor dat pad geen nieuwe `UPLOAD`-regel meer verschijnen
   (enkel `STAT`, of helemaal niets als het al in het MANIFEST-verschil wegvalt).
- ✅ Geslaagd als: het serverlog na de eerste succesvolle upload geen tweede `UPLOAD` voor
  hetzelfde pad toont, hoeveel cycli er ook verstrijken.

### Test 3 — Onderbroken upload van een groot bestand → hervatting
1. Zet een groot bestand (idealiter ≥ 5 GB; `fsutil file createnew groot.bin 5368709120`
   maakt snel een testbestand van de juiste grootte) in de syncmap van werkplek A.
2. Zodra de upload zichtbaar bezig is (servermap toont `groot.bin.part` dat in grootte
   groeit), trek de netwerkkabel eruit (of schakel de wifi uit) op werkplek A, óf sluit
   het clientproces geforceerd af.
3. Controleer op de server: alleen `groot.bin.part` bestaat, **nooit** `groot.bin` zelf.
4. Herstel de verbinding / herstart de client. Wacht op de volgende cyclus.
5. Controleer: `groot.bin.part` is verdwenen, `groot.bin` staat er nu volledig, en de
   SHA-256 komt exact overeen met het origineel.
- ✅ Geslaagd als: er nooit een halfgevuld `groot.bin` (zonder `.part`) zichtbaar is
  geweest, en de hash na hervatting klopt.

### Test 4 — Twee clients gelijktijdig
1. Start (indien nog niet actief) een derde werkplek-instantie, of laat A en B tegelijk
   bestanden op **verschillende** paden wegschrijven. Beide moeten gewoon slagen.
2. Om het 423-scenario expliciet te zien: laat twee clients zo goed als gelijktijdig een
   wijziging op **hetzelfde pad** doorvoeren (bv. hetzelfde bestand op exact hetzelfde
   moment op A en B aanpassen, vlak voor een poll-cyclus). In het serverlog zie je dat één
   verbinding de upload voltooit en de andere — als hij exact overlapt — `423`
   terugkrijgt op zijn eigen upload-poging.
- ✅ Geslaagd als: verschillende paden nooit blokkeren, en een botsing op hetzelfde pad
  nooit tot een corrupt bestand leidt (de "verliezer" probeert het gewoon een cyclus
  later opnieuw).

### Test 5 — Unicode, submappen, verboden pad
1. Maak op werkplek A een submap met een naam met spatie en accenten, bv.
   `mappen\café münchen\verslag.txt`, en laat die synchroniseren naar B.
2. Controleer dat de mapstructuur en bestandsnaam op B exact (inclusief accenten)
   overeenkomen.
3. (Optioneel, voor de foutafhandeling) test een verboden pad met een raw request — zie
   §7 hieronder voor hoe je dat zonder de normale client kunt versturen — en controleer
   dat je `400 Bad Request` terugkrijgt.
- ✅ Geslaagd als: unicode/submap correct synchroniseert, en een verboden pad altijd 400
  oplevert, nooit een serverfout of crash.

### Test 6 — Onbekende protocolversie → 505
Gebruik de raw-socket methode uit §7 om een `HELLO SYNC/9.9` te versturen.
- ✅ Geslaagd als: het antwoord `SYNC/1.0 505 Version Not Supported` is, en de server
  daarna gewoon doorblijft draaien voor andere clients.

### Test 7 — Delete propageert
1. Verwijder op werkplek A een bestand dat al naar de server en werkplek B
   gesynchroniseerd was.
2. Wacht een poll-cyclus van A: controleer dat het bestand uit de servermap verdwijnt.
3. Wacht een poll-cyclus van B: controleer dat het bestand ook uit werkplek B verdwijnt.
- ✅ Geslaagd als: het bestand nergens terugkomt (dit was precies de bug die ik eerder
  vond en fixte — zie `TESTING.md`), en er in het serverlog precies één `DELETE`-regel
  voor dat pad verschijnt.

## 6. Extra uitdaging: TLS testen

1. Start de server met een extra TLS-poort:
   ```powershell
   FileSync.Server.exe --port 4711 --tls-port 4712 --storage D:\filesync-server-data
   ```
   Bij de allereerste keer verschijnen `server-cert.pfx` (privé, blijft op de server) en
   `server-cert.cer` (publiek) in de werkmap. Het serverlog toont een regel die je eraan
   herinnert dit `.cer`-bestand naar clients te kopiëren.
2. Kopieer `server-cert.cer` naar elke clientmachine (USB-stick, netwerkshare, e-mail —
   maakt niet uit, dit hoeft niet via het protocol zelf).
3. Start de client met dat certificaat, tegen de TLS-poort:
   ```powershell
   FileSync.Client.exe --host <server-ip> --port 4712 --folder D:\filesync-sync --server-cert D:\server-cert.cer
   ```
   Serverlog moet tonen: `Nieuwe verbinding van ... (TLS)` gevolgd door
   `TLS-handshake geslaagd met ...`.
- ✅ **Positieve test**: bestanden synchroniseren normaal, exact als over de kale poort
  4711 — het protocol zelf is ongewijzigd, alleen de transportlaag is versleuteld.
- ✅ **Negatieve test** (het certificaat wordt écht gecontroleerd, niet alleen decoratief
  ingesteld): geef een client per ongeluk (of expres, als test) een ánder `.cer`-bestand
  mee dan wat de server daadwerkelijk gebruikt. Verwacht resultaat: de sync-cyclus faalt
  met een foutmelding over een geweigerd certificaat, en er wordt niets gesynchroniseerd.

## 7. Extra uitdaging: server in Docker testen

Vereist Docker Desktop (Windows/Mac) of Docker Engine (Linux) op de servermachine.

```powershell
cd "C:\Git\Netwerk System en Security\FileSync"
docker build -t filesync-server .
docker run -d --name filesync-server -p 4711:4711 -p 4712:4712 -v filesync-data:/data filesync-server
```

Controleer dat de container draait en luistert:
```powershell
docker logs filesync-server
```
Verwacht: dezelfde logregel als een normaal gestarte server (`Server luistert op poort
4711, opslagmap '/data'`).

Verbind een gewone client (op een andere machine, of gewoon op localhost) precies zoals
bij een niet-gecontaineriseerde server — de container is voor de client onzichtbaar, het
is gewoon een TCP-server op poort 4711:
```powershell
FileSync.Client.exe --host <docker-host-ip> --port 4711 --folder D:\filesync-sync --interval 5 --client-id docker-test
```
- ✅ Geslaagd als: sync werkt identiek aan de niet-gecontaineriseerde server, en
  `docker stop filesync-server && docker start filesync-server` de eerder
  gesynchroniseerde bestanden behoudt (dankzij het `filesync-data`-volume).

Opruimen na afloop:
```powershell
docker rm -f filesync-server
docker volume rm filesync-data
```

## 8. Troubleshooting

- **Client krijgt geen verbinding**: controleer dat de servermachine poort 4711 toestaat
  binnenkomend (`New-NetFirewallRule -DisplayName "FileSync" -Direction Inbound -LocalPort 4711 -Protocol TCP -Action Allow`
  in een PowerShell-venster **als Administrator** op de servermachine).
- **"Adres al in gebruik"**: een vorige serverinstantie draait nog; sluit dat venster of
  gebruik `taskkill /F /IM FileSync.Server.exe`.
- **Client logt niets zichtbaars**: de console-output kan gebufferd zijn als je hem naar
  een bestand omleidt; kijk in een los venster live mee in plaats van de output om te
  leiden, of gebruik `dotnet run ... | more` om zeker te zijn dat je live output ziet.

## 9. Een rauw protocolverzoek sturen (voor test 5/6)

Zonder extra tools, met PowerShell op elke Windows-machine:

```powershell
$client = New-Object System.Net.Sockets.TcpClient("127.0.0.1", 4711)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)
$writer.NewLine = "`r`n"
$writer.Write("HELLO SYNC/9.9`r`nClient-Id: raw-test`r`n`r`n")
$writer.Flush()
Start-Sleep -Milliseconds 500
$reader = New-Object System.IO.StreamReader($stream)
$buffer = New-Object char[] 200
$read = $reader.Read($buffer, 0, 200)
[string]::new($buffer, 0, $read)
$client.Close()
```

Verander de eerste regel van het verzoek om andere gevallen te testen, bv.
`STAT ../../secret.txt SYNC/1.0` (na een geslaagde `HELLO SYNC/1.0`) voor het
verboden-pad-scenario.
