# Foreningsmakker – tjekliste til skiftet

Alt, hvad Jacobs Foreningsmakker kan (repo `den-ekstra-frivillige`, læst 2026-11-04), som vores version skal kunne, før
foreningsmakker.dk skiftes over i ét hug. Sæt kryds, når det virker i FalsigForening. Kolonnen *Club* siger, om
skabelonen (ITMartinClub) allerede har noget, vi kan bygge videre på: **ja**, **delvis** eller **nej**.

Kilde i Jacobs kode: `web/app/...` (sider), `web/app/*-actions.ts` (handlinger), `web/app/api/...` (API), `agents/`
(job på hans Mac), `supabase/migrations` (77 tabeller).

## 1. Login og adgang

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Log ind med link på mail (ingen adgangskode) | `login`, `auth/callback`, `sendLoginLink`, `logUd` | delvis (PIN) |
| [ ] | Invitation af personer til foreningen | `invitation`, `inviterPerson`, `gensendInvitation` | ja (Join) |
| [ ] | Personer med adgang + roller | `forening/[id]/adgang`, `skiftRolle`, `fjernAdgang` | delvis |
| [ ] | Administratorer (Jacob/Martin) | `ADMIN_EMAILS` | ja (Admin) |

## 2. Foreningen

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Opret forening (CVR-opslag) | `ny`, `opretForening`, `lib/cvr.ts` | delvis (CreateGroup) |
| [ ] | Foreningens overblik: frister, tilskud, Paragrafen, nyheder | `forening/[id]` | delvis (GroupHome) |
| [ ] | Ret foreningens og personers oplysninger | `forening/[id]/ret`, `opdaterForening`, `opdaterPerson`, `tilfoejPerson` | delvis |
| [ ] | Dokumenter: upload, ret, slet, hent | `uploadDokument`, `retDokument`, `sletDokument`, `dokument/[dokId]` | delvis (Storage) |
| [ ] | Opgaver | `opretOpgave`, `retOpgave`, `markerOpgave`, `saetOpgaveStatus` | ja (Opgaver) |
| [ ] | Vedtægter: læs og foreslå ændringer | `laesVedtaegter`, `besvarForslag` | nej |

## 3. Møder og referat

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Møder + dagsorden (tilføj, flyt, fjern punkter) | `forening/[id]/moede/[moedeId]`, `opretMoede`, `tilfoejPunkt`, `flytPunkt`, `samlDagsorden` | ja (Moeder) |
| [ ] | Indkaldelse | `markerIndkaldt` | delvis |
| [ ] | Optag mødet, transskriber, lav referat med AI | `startOptagelse`, `registrerSegment`, `transskriberNaeste`, `lavReferatAction` | nej |
| [ ] | Ret, godkend og send referat, gem som dokument | `gemReferat`, `godkendReferat`, `sendReferat`, `gemReferatSomDokument` | nej |
| [ ] | Printvenlig udgave | `moede/[moedeId]/print` | nej |
| [ ] | Referat via link uden login | `r/[token]` | nej |

## 4. Fonde og tilskud (Jacobs kerne)

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Regelkatalog ind: fonde, regler, tilskudskilder (JSON) | `regelkatalog/*.json`, `supabase/seed` | nej |
| [ ] | Find tilskud fra kommunen | `findTilskud`, `besluOrdning`, `saetTilskudStatus` | nej |
| [ ] | Projekter og fondsansøgninger | `forening/[id]/fonde`, `fonde/[projektId]`, `opretProjekt`, `startAnsoegning` | nej |
| [ ] | Skriv ansøgningsudkast med AI + eksport | `ansoegning/[ansId]`, `skrivUdkast`, `gemFelter`, `ansoegning/.../eksport` | nej |
| [ ] | Status og honorar på ansøgninger | `skiftStatus`, `honorarStatus`, `saetForloeb` | nej |
| [ ] | Offentlig side om fondsservice | `fonde` | nej |
| [ ] | Gratis tilskudstjek (bestil, kladde, mail) | `kom-i-gang`, `api/kom-i-gang`, `api/tilskudstjek`, `tak` | nej |
| [ ] | Fondsresearch-rapport (kun admin) | `fondsresearch`, `fondsresearch/[id]/rapport` | nej |

## 5. Økonomi

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Primo, budget, bevægelser + import fra bank | `forening/[id]/oekonomi`, `gemPrimo`, `gemBudget`, `importerBevaegelser` | nej |
| [ ] | Regnskabsoverblik | `oekonomi/regnskab` | nej |
| [ ] | Tilskud i regnskabet | `tilfoejTilskud`, `retTilskud` | nej |
| [ ] | Medlemmer + import | `tilfoejMedlem`, `retMedlem`, `importerMedlemmer` | delvis (Users) |
| [ ] | Udlæg: kvittering (aflæs med AI), indsend, godkend, afvis, udbetalt | `forening/[id]/udlaeg`, `aflaesKvittering`, `godkendUdlaeg`, `markerUdbetalt` | nej |
| [ ] | Kasserer-log (dansk tid) | `admin/kasserer` | nej |

## 6. Abonnement og betaling (Stripe – samme konto)

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Tilmeld foreningen + betaling | `tilmeld`, `api/tilmeld`, `tilmeld/fuldfoer` | nej |
| [ ] | Stripe-webhook med verificering | `api/stripe/webhook` | nej |
| [ ] | Foreningens abonnement | `forening/[id]/abonnement` | nej |
| [ ] | Selvbetjening (job) | `api/cron/selvbetjening`, `selvbetjening-actions.ts` | nej |
| [ ] | Admin: abonnementer | `admin/abonnementer` | nej |
| [ ] | Handelsbetingelser + privatlivspolitik | `handelsbetingelser`, `privatliv` | delvis (Kolibri /om) |

## 7. Foreningens egen hjemmeside

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Byg hjemmeside (sider, billeder, versioner) | `forening/[id]/hjemmeside`, `lib/hjemmeside` | nej |
| [ ] | Vis den offentligt | `f/[slug]/[[...side]]` | nej |

## 8. Support, hjælp og viden

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Support: opret og følg sager | `support`, `forening/[id]/support`, `api/support`, `api/support/sag/[nr]` | delvis (Beskeder) |
| [ ] | Hjælpecenter: 18 guider, FAQ, videoer | `hjaelp`, `hjaelp/[slug]` | nej |
| [ ] | Viden: artikler (årshjul, fonde, kommunalt tilskud) | `viden`, `viden/[slug]` | nej |
| [ ] | Book en snak | `book-en-snak`, `api/book-en-snak` | nej |
| [ ] | Pilotforening + feedback | `pilot`, `forening/[id]/feedback`, `admin/pilot` | nej |

## 9. Admin

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Admin: fonde, AI-forbrug, samtaler, support, tilskudstjek-funnel | `admin/*` | delvis (Admin) |
| [ ] | AI-budget og forbrug pr. kørsel | tabeller `ai_koersel`, `ai_forbrug`, `ai_budget` | nej |

## 10. Job (kører i dag på Jacobs Mac)

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Mandagsmail til bestyrelser | `agents/launchd/mandagsmail`, `run-ugentlig.ts` | delvis (Jobs.cs) |
| [ ] | Årshjul og regelmotor (frister) | `agents/src/aarshjul.ts`, `regelmotor.ts` | delvis (Reminders) |
| [ ] | Sekretær-agent | `agents/src/agents/sekretaer.ts` | nej |

## 11. Synlighed (behold Googles placering)

| Færdig | Funktion | Jacob | Club |
|---|---|---|---|
| [ ] | Forside med samme budskab | `page.tsx` | nej |
| [ ] | Sitemap, robots, titler, canonical | `sitemap.ts`, `robots.ts`, `lib/seo.ts` | nej |
| [ ] | Struktureret data (JSON-LD) + `llms.txt` | `lib/strukturerede-data.ts`, `llms.txt`, `llms-full.txt` | nej |
| [ ] | Samme adresser som i dag (alle stier ovenfor) | hele `web/app` | nej |

## 12. Selve skiftet

- [ ] Import af alle data fra en Supabase-eksport, øvet flere gange på en kopi
- [ ] Brugerne logger ind med deres mail som før (link på mail, ingen nye adgangskoder)
- [ ] Stripe-webhook peger på vores server, abonnementer kører videre
- [ ] Mail sendes fra `hej@foreningsmakker.dk` (Gmail "Send mail som")
- [ ] hvemsigerja.dk viderestiller stadig med UTM
- [ ] DNS for foreningsmakker.dk flyttet til Martins Cloudflare (aftale med Jacob)
- [ ] Vej tilbage testet: DNS tilbage til Vercel på 5 minutter

Ikke med (kun Jacobs marketing og interne værktøjer): HubSpot-CRM, SEO-noter, reklamefilm, Make.
