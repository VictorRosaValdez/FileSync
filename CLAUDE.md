# FileSync — projectinstructies

Schoolproject (programmeeropdracht Systems & Security): client-server bestandssynchronisatie
via een centrale server, een eenvoudige Dropbox-variant. De normatieve protocolspecificatie
staat in `PROTOCOL.md` — implementeer die exact; wijzig het protocol nooit zonder overleg,
want het is gedeeld met partnerteams en ingeleverd bij de docent.

## Harde opdrachtregels (beoordelingscriteria — nooit schenden)

- **Kale TCP-sockets, géén frameworks**: geen HttpListener/HttpClient, geen ASP.NET, geen
  bestaande sync/HTTP-libraries. Alleen `System.Net.Sockets` (TcpListener/TcpClient/Socket)
  en standaard BCL (streams, SHA256, threading).
- Server bedient **meerdere clients tegelijk** (thread of Task per verbinding + per-pad write-lock → 423).
- Bestanden **> 4 GB**: alles streamend in blokken (bijv. 64 KiB buffer), 64-bits lengtes/offsets,
  nooit een heel bestand in het geheugen.
- **Netwerkstoringen**: nooit halve/verminkte bestanden — .part + SHA-256-verificatie +
  atomische rename (File.Move) conform PROTOCOL.md §5; hervatting via STAT/Part-Size/Offset.
- **Geen UI** — console-apps.
- Demo draait als **zelfstandige executables buiten de IDE** (dotnet publish, self-contained),
  client en server op **verschillende fysieke machines**; host/poort via commandline-argumenten.
- Er moet **aantoonbaar getest** zijn (unit tests + gedocumenteerde handmatige testscenario's).

## Techniek

- C# / .NET 8, console-apps. Solution `FileSync.sln` met projecten:
  - `FileSync.Shared` — protocol-parser/-writer (kopregel, headers, statuscodes), padvalidatie,
    hashing, manifest-(de)serialisatie. Geen socketcode; volledig unit-testbaar.
  - `FileSync.Server` — TcpListener, verbinding-per-thread/Task, commandhandlers, per-pad
    locking, .part-beheer, opslagmap via argument.
  - `FileSync.Client` — syncmap scannen, lokale manifest/hash-cache, sync-cyclus
    (MANIFEST → diff → STAT → UPLOAD/DOWNLOAD/DELETE), retry/hervatting, pollinterval.
  - `FileSync.Tests` — NUnit. Prioriteit: protocolparser (rare input, te lange regels,
    ontbrekende headers), padvalidatie (traversal, verboden tekens, reserved names, NFC),
    hervattingslogica, dedup-beslissing.
- Codekwaliteit boven slimmigheid: dit wordt in een assessment **regel voor regel uitgelegd**
  door studenten. Kleine methodes, duidelijke namen, Nederlands commentaar bij de
  niet-triviale stukken (offsetberekening, lockstrategie, atomische rename).
- Foutafhandeling: elke socket-/IO-fout leidt tot nette afsluiting van die verbinding en een
  logregel; de server mag nooit crashen door één slechte client.
- Logging: eenvoudige console-logging van commando's, statuscodes en fouten (geen logframework nodig).

## Acceptatietests (uit het requirementsdocument — moeten demo-baar zijn)

1. Bestand op werkplek A verschijnt via de server op werkplek B (FR-1/FR-4).
2. Tweede sync van identiek bestand → aantoonbaar géén overdracht, server antwoordt 204 of
   client slaat over o.b.v. hash (FR-2).
3. Upload van ≥ 5 GB-bestand afbreken (kabel eruit) → hervatten → eindbestand hash-correct;
   nooit een half doelbestand zichtbaar (FR-3/FR-5/FR-7).
4. Twee clients tegelijk: verschillende paden slagen beide; zelfde pad → één krijgt 423 (FR-6).
5. Pad met unicode + submappen werkt Windows↔Linux; verboden pad → 400 (FR-8).
6. `SYNC/9.9` in HELLO → 505 (FR-10).
7. Delete op A propageert naar server en B (FR-9, should).

## Werkwijze

- Bouw in deze volgorde: Shared (parser + tests) → Server → Client → integratietest lokaal
  (twee mappen, localhost) → publish-profielen.
- Na elke stap: `dotnet build` en `dotnet test` groen houden.
- `dotnet publish -c Release -r win-x64 --self-contained` (en `linux-x64` indien nodig) voor de demo.
- Extra uitdagingen pas ná een werkende basis: TLS-variant (PROTOCOL.md §8), server in
  Docker-container (extra punten; er is al Docker-leerstof in de cursus).
