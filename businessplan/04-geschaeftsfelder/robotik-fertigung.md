# 04 · Geschäftsfeld: Robotische Fertigung (6-Achs-Freiformfräsen)

**Status:** Entwurf · **Zuletzt aktualisiert:** 2026-07-18

## Idee

Ein Industrieroboter (6/7-Achs) mit Frässpindel als Ergänzung zur 3-Achs-Nesting-CNC:
Freiformflächen, doppelt gekrümmte Teile, große Skulptur-/Formenbauteile, Styropor-/
Schaum-Formen, komplexe Verbindungen — alles, was klassische Werkstätten in der Region
nicht oder nur teuer über Modellbauer liefern können.

**Persönlicher Hintergrund:** Master Robotik/Computational Design (IAAC Barcelona),
laufende Arbeit an CAM-/Postprozessor-Themen (u. a. NC-Hops/Rhino-Toolchain — dieses Repo 🙂).
Der Workflow Rhino/Grasshopper → Roboter ist Kernkompetenz, nicht Zukauf.

## Anwendungsfälle im Ausstellungsbau (Synergie zum Bestandsgeschäft)

- Museums-/Ausstellungsexponate mit Freiformgeometrie (Reliefs, Topografien, Displays)
- Formenbau für GFK/Beton-Sonderteile (Messestände, Shop-Fittings)
- Skulpturale Elemente für Architektur (Fassaden-Mockups, Innenausbau-Sonderteile)
- Bearbeitung von Großformaten, die nicht auf die Nesting-CNC passen

## Externe Dienstleistung (zweites Standbein)

Lohnfräsen für: Modellbauer, Bühnenbild (Köln = Medienstadt!), Künstler, Architekturbüros
(1:1-Mockups), Bootsbau/Formenbau, Möbeldesigner.

## Investitionsrahmen [ANNAHME — zu validieren]

| Position | Spanne |
|---|---|
| Gebrauchter Industrieroboter (KUKA KR 210–500 / ABB), inkl. Achse 7 (Linearachse) optional | 25–80 T€ |
| Frässpindel + Werkzeugwechsler + Steuerungsintegration | 15–40 T€ |
| Sicherheitstechnik, Einhausung, Absaugung, Fundament | 15–30 T€ |
| Software (CAM/Offline-Programmierung; teils Eigenbau) | 5–20 T€ |
| **Summe Startausbau** | **60–170 T€** |

Förderhebel: Digitalisierungs-/Investitionsförderung NRW, KfW — siehe [05-finanzierung](../05-finanzierung/).

## Stufenplan

1. **Phase 0 (Studium/Thesis):** Workflows, Postprozessoren, Demoprojekte dokumentieren → Portfolio
2. **Phase 1 (Jahr 1 nach Übernahme):** Gebrauchtroboter, interne Projekte + 2–3 Leuchtturmaufträge
3. **Phase 2:** aktiver Vertrieb als Dienstleistung, Auslastungsziel [ANNAHME] ≥ 40 % extern
4. **Phase 3:** ggf. Ausgründung in Additive/Robotics GmbH (siehe Kap. 03, Variante B)

## Offene Fragen

- [ ] Hallenhöhe/Fundament/Strom an beiden Standorten robotertauglich?
- [ ] Make-or-buy CAM-Software (PowerMill Robot / SprutCAM / RoboDK / Eigenbau auf Rhino-Basis)?
- [ ] Versicherung & CE/Arbeitsschutz für Roboterzelle im Handwerksbetrieb
