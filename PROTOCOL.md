# SYNC/1.0 — Applicatieprotocol FileSync

Dit is de normatieve protocolspecificatie. De implementatie moet hier exact aan voldoen;
het protocol is gedeeld met partnerteams en ingeleverd bij de docent. Wijzigingen alleen
na afstemming.

## 1. Transport en algemene opbouw

- Transport: TCP. Standaardpoort **4711** (configureerbaar). Optionele TLS-variant op poort 4712 (zie §8).
- Strikt **request-response**: de client stuurt één verzoek en wacht op één antwoord.
  Nooit meer dan één verzoek tegelijk per verbinding. Parallellisme = meerdere verbindingen.

```
verzoek     = kopregel CRLF *( header CRLF ) CRLF [ body ]
kopregel    = COMMANDO [ SP pad ] SP "SYNC/1.0"
antwoord    = "SYNC/1.0" SP statuscode SP reden CRLF *( header CRLF ) CRLF [ body ]
header      = naam ":" SP waarde
```

- Tekstdelen (kopregel + headers): **UTF-8**, regeleinde **CRLF** (0x0D 0x0A),
  max 4096 bytes per regel incl. CRLF. Headernamen hoofdletterongevoelig.
  Lege regel = einde headers.
- Body: rauw binair, exact `Content-Length` bytes. `Content-Length` is decimaal,
  max 19 cijfers (**64-bits** → bestanden > 4 GB). Geen chunked encoding, geen compressie.
- MANIFEST-tekstbody: UTF-8, regels gescheiden door **LF** (0x0A), velden door **TAB** (0x09).

## 2. Padregels (canonieke vorm)

- Relatief t.o.v. de syncmap, **forward slashes**, begint niet met `/`.
- UTF-8 in **NFC**. Segment ≤ 240 bytes; volledig pad ≤ 2048 bytes.
- Verboden: `\ : * ? " < > |`, stuurtekens 0x00–0x1F; segmenten `.` of `..`;
  segmenten eindigend op spatie of punt; Windows-reserved names (CON, PRN, AUX, NUL, COM1–9, LPT1–9).
- Paden zijn hoofdlettergevoelig in het protocol. Client op case-insensitief FS meldt botsingen als conflict.
- Server valideert elk pad → **400** bij overtreding. `..` weigeren is een security-eis (directory traversal).

## 3. Commando's (semantiek)

| Commando | Argument / headers | Betekenis |
|---|---|---|
| `HELLO` | header `Client-Id` | Opent sessie, versieonderhandeling. 200 bij ondersteunde versie, anders 505. Elk ander commando vóór geslaagde HELLO → 400. |
| `MANIFEST` | — | 200 + tekstbody: per bestand één regel `hash TAB grootte TAB modified-utc TAB pad`. |
| `STAT <pad>` | — | 200 + headers `Exists: yes/no`, `Size`, `Hash`, `Part-Size` (bytes van onvoltooide upload). |
| `UPLOAD <pad>` | `Offset`, `Content-Length`, `Total-Length`, `Hash`, `Modified` | Stuurt bytes vanaf Offset. Compleet (Offset+Content-Length == Total-Length): server verifieert SHA-256 → 201, of 409 bij mismatch (+ .part verwijderen). Tussendeel → 200. Al identiek aanwezig → 204 vóór body-lezen. |
| `DOWNLOAD <pad>` | optioneel `Offset` | 200 + `Content-Length`, `Hash` + binaire body, of 404. |
| `DELETE <pad>` | — | 200, of 404. |
| `BYE` | — | 200, daarna verbinding sluiten. |

Headerformaten: `Hash` = SHA-256 van het **volledige** bestand, lowercase hex.
`Modified` = UTC ISO 8601, bijv. `2026-07-05T14:03:22Z`. Alle groottes/offsets decimaal 64-bits.

## 4. Statuscodes

| Code | Reden | Gebruik |
|---|---|---|
| 200 | OK | Geslaagd; bij MANIFEST/DOWNLOAD volgt body; UPLOAD-tussendeel ontvangen. |
| 201 | Created | Upload compleet, hash geverifieerd, bestand atomisch geplaatst. |
| 204 | Identical | Zelfde hash al aanwezig; geen overdracht. |
| 400 | Bad Request | Syntaxfout, ongeldige header, verboden pad, commando vóór HELLO. |
| 404 | Not Found | Pad bestaat niet. |
| 409 | Hash Mismatch | Hash klopt niet; server verwijdert .part. |
| 423 | Locked | Pad in gebruik door gelijktijdige schrijfactie. |
| 500 | Server Error | Onverwachte serverfout. |
| 505 | Version Not Supported | Onbekende protocolversie in HELLO. |

## 5. Sequentie

Normaal: connect → `HELLO` → `MANIFEST` → per verschil `STAT` → `UPLOAD`/`DOWNLOAD`/`DELETE` → `BYE`.

Atomiciteit: server schrijft inkomende uploads naar `<pad>.part` (verborgen/tijdelijk).
Doelbestand ontstaat **alleen** door: upload compleet → SHA-256 van samengesteld bestand == `Hash`
→ atomische rename `.part` → doelpad → 201. Een netwerkstoring laat dus nooit een half
bestand als echt bestand achter.

Hervatting: na verbroken verbinding → reconnect → `HELLO` → `STAT <pad>` → lees `Part-Size`
→ `UPLOAD` met `Offset = Part-Size`. Bij 409 verwijdert de server de .part en begint de
client opnieuw vanaf 0.

Gelijktijdigheid: server behandelt elke verbinding in een eigen thread. Per-pad
schrijfvergrendeling: tweede gelijktijdige schrijver op hetzelfde pad krijgt direct **423**.
Lees-acties op andere paden lopen door.

Conflictstrategie: **last writer wins**. Geen versiegeschiedenis.

## 6. Deduplicatie

Vergelijking op inhoud (SHA-256), niet op naam of tijd. Client vergelijkt lokale hash met
MANIFEST/STAT; alleen bij afwijking overdracht. Server mag een UPLOAD met reeds bekende
hash voor dat pad beantwoorden met 204 vóórdat de body gelezen wordt; client ziet 204 na
de headers en verstuurt de body niet.

## 7. Voorbeeldsessie

```
C:  HELLO SYNC/1.0
C:  Client-Id: werkplek-anna
C:
S:  SYNC/1.0 200 OK
S:
C:  STAT docs/rapport.pdf SYNC/1.0
C:
S:  SYNC/1.0 200 OK
S:  Exists: no
S:  Part-Size: 0
S:
C:  UPLOAD docs/rapport.pdf SYNC/1.0
C:  Offset: 0
C:  Total-Length: 5368709120
C:  Content-Length: 5368709120
C:  Hash: 9f2a…e41c
C:  Modified: 2026-07-05T14:03:22Z
C:
C:  <5 368 709 120 bytes binaire data>
S:  SYNC/1.0 201 Created
S:
C:  BYE SYNC/1.0
C:
S:  SYNC/1.0 200 OK
```

## 8. Optioneel: TLS (extra uitdaging)

TLS-handshake direct na TCP-connect, vóór HELLO, over dezelfde socket
(.NET: `SslStream`; Java: `SSLSocket`). Protocol zelf ongewijzigd. Aparte poort 4712,
zelfondertekend certificaat dat clients expliciet vertrouwen.
