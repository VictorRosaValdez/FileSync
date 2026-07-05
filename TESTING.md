# FileSync — testdocumentatie

## Geautomatiseerde tests

`FileSync.Tests` (NUnit, 90 tests) dekt:

- **Protocol** (`FileSync.Tests/Protocol`): kopregel-/statusregel-/header-parsing, inclusief
  randgevallen (regel > 4096 bytes, kale LF, header zonder `": "`, pad met spaties,
  niet-ondersteunde versie-token, header-body hand-off).
- **Validation** (`FileSync.Tests/Validation`): padvalidatie — traversal, verboden tekens,
  Windows reserved names, NFC-normalisatie, lengtegrenzen.
- **Manifest / Hashing / Time**: (de)serialisatie, SHA-256-streaming, ISO-8601.
- **Server** (`FileSync.Tests/Server`): `PathLockRegistry` (nooit blokkerend), en
  `UploadCommandHandler` end-to-end tegen een echte tijdelijke opslagmap: verse upload,
  tussenchunk, hervatting met juiste/foute offset, hash-mismatch, dedup, 423 bij conflict,
  ongeldig pad.
- **Client** (`FileSync.Tests/Client`): `ManifestDiffer` (upload/download/delete-beslissingen,
  inclusief de regressietest hieronder) en `LocalHashCache`.

Uitvoeren: `dotnet test FileSync.sln`.

### Belangrijke regressietest

Tijdens handmatig testen (zie hieronder) bleek dat een client die nog een ongewijzigde lokale
kopie had van een bestand dat een andere client op de server had laten verwijderen, dat
bestand per ongeluk terugoploadde in plaats van het lokaal ook te verwijderen. Dit is opgelost
in `ManifestDiffer`/`LocalHashCache` (onderscheid tussen "hash berekend" en "hash bevestigd
gesynchroniseerd met de server") en vastgelegd in
`ManifestDifferTests.Diff_RemotelyDeletedByAnotherClient_UnchangedLocalCopy_PlansLocalDelete`.

## Handmatige testscenario's (acceptatietests)

Lokaal te reproduceren met drie mappen op één machine (server-storage + twee werkplekken);
voor de echte demo: server en clients op verschillende fysieke machines, `--host` naar het
IP-adres van de servermachine.

```
FileSync.Server.exe --port 4711 --storage <server-map>
FileSync.Client.exe --host <server-ip> --port 4711 --folder <werkplek-a> --interval 5 --client-id A
FileSync.Client.exe --host <server-ip> --port 4711 --folder <werkplek-b> --interval 5 --client-id B
```

### 1. Bestand propageert van A via server naar B (FR-1/FR-4)
Plaats een bestand in werkplek A. Wacht één poll-interval; controleer dat het in de
servermap staat. Wacht nog een interval; controleer dat het in werkplek B staat, met
identieke inhoud (`diff`/hash-vergelijking).
**Resultaat:** geverifieerd — bestand en een submap met unicode+spatie
(`mappen/café münchen/verslag.txt`) kwamen byte-voor-byte identiek aan op B.

### 2. Tweede sync van identiek bestand → geen overdracht (FR-2)
Na test 1: laat nog een aantal sync-cycli lopen zonder het bestand te wijzigen.
Controleer in het serverlog dat er geen nieuwe `UPLOAD`-regel voor dat pad verschijnt
(de client herkent de ongewijzigde hash al via `STAT` en slaat `UPLOAD` over).
**Resultaat:** geverifieerd — exact 1 `UPLOAD`-regel voor het pad, ook na meerdere
volgende cycli.

### 3. Upload van een groot bestand onderbreken → hervatten → hash-correct (FR-3/FR-5/FR-7)
Start een `UPLOAD` van een groot bestand; onderbreek de verbinding halverwege (kabel
eruit, of het clientproces geforceerd stoppen). Controleer dat de servermap alleen een
`<pad>.part` toont, nooit het doelbestand. Hervat (nieuwe `HELLO` → `STAT` → `UPLOAD` met
`Offset` = `Part-Size`); controleer dat het `.part`-bestand verdwijnt, het doelbestand
verschijnt, en de SHA-256 exact overeenkomt met het origineel.
**Resultaat:** geverifieerd via een rechtstreekse socketsessie (twee losse TCP-verbindingen
die samen één bestand in twee helften uploadden): na de eerste helft stond alleen
`bigfile.bin.part` (exacte grootte van de eerste helft) in de servermap; na hervatting met
de juiste offset volgde `201 Created` en was het eindbestand byte-voor-byte identiek aan
het origineel. Voor de echte demo met een fysieke kabelonderbreking: gebruik een bestand
van meerdere GB's zodat er tijd is om de verbinding daadwerkelijk te verbreken.

### 4. Twee clients gelijktijdig (FR-6)
Twee gelijktijdige `UPLOAD`-sessies naar verschillende paden slagen beide. Twee
gelijktijdige `UPLOAD`-sessies naar hetzelfde pad: één krijgt `201`, de andere `423 Locked`.
**Resultaat:** geautomatiseerd gedekt door `UploadCommandHandlerTests` (`PathLockRegistry`
geeft aantoonbaar direct — niet-blokkerend — `false` terug bij een reeds vergrendeld pad).

### 5. Unicode + submappen; verboden pad → 400 (FR-8)
Plaats een bestand in een submap met unicode-tekens en een spatie in de naam; controleer
dat het pad correct synchroniseert. Stuur een `STAT`/`UPLOAD` met een pad dat `..` bevat.
**Resultaat:** geverifieerd — `mappen/café münchen/verslag.txt` synchroniseerde correct;
een rauw `STAT ../../secret.txt SYNC/1.0`-verzoek gaf `400 Bad Request`.

### 6. `SYNC/9.9` in HELLO → 505 (FR-10)
Stuur een rauwe `HELLO SYNC/9.9`-kopregel.
**Resultaat:** geverifieerd — server antwoordde `SYNC/1.0 505 Version Not Supported`.

### 7. Delete propageert naar server en B (FR-9)
Verwijder een eerder gesynchroniseerd bestand in werkplek A. Wacht op de sync-cycli van A
en B.
**Resultaat:** geverifieerd — bestand verdween uit de servermap (één `DELETE`-commando) en
vervolgens ook uit werkplek B, zonder dat het ergens werd teruggezet (zie regressietest
hierboven voor de bug die dit aanvankelijk verhinderde).
