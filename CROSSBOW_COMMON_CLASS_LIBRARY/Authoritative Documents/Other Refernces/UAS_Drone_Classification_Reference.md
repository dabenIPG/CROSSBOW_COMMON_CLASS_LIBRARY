# UAS / Drone Classification Reference
*Compiled reference across U.S. DoD and NATO standards*

---

## Overview

Two primary classification frameworks exist for military UAS:

- **U.S. DoD (JP 3-30)** — Five groups (1–5), based on max gross takeoff weight, operating altitude, and airspeed. Adopted in 2008, updated continuously. Weight is the primary determining factor if attributes conflict.
- **NATO (STANAG 4670 / JCG-UAS)** — Three classes (I–III), agreed in 2009. Weight is again the deciding factor where parameters conflict. Designed to align multinational force integration across alliance members.

> If a UAS exceeds any single attribute threshold (weight, altitude, or speed), it is assigned to the **higher** group/class regardless of other characteristics.

---

## U.S. DoD — Five Group System

| Group | Max Weight | Max Altitude | Max Speed | Role / Scale | Examples |
|-------|-----------|--------------|-----------|--------------|---------|
| **Group 1** | < 20 lbs (9 kg) | < 1,200 ft AGL | < 100 kts | Micro/mini tactical — squad & platoon level ISR, hand-launched | DJI Mavic, RQ-11 Raven, Puma AE, WASP III |
| **Group 2** | 21–55 lbs (10–25 kg) | < 3,500 ft AGL | < 250 kts | Small tactical — battalion-level ISR, comms relay, maritime ops | ScanEagle, RQ-20 Puma, Altius-600 |
| **Group 3** | 55–1,320 lbs (25–600 kg) | < 18,000 ft MSL | < 250 kts | Medium tactical — extended ISR, EW, precision strike capable | RQ-7B Shadow, RQ-21 Blackjack, V-BAT |
| **Group 4** | > 1,320 lbs (600 kg) | < 18,000 ft MSL | Any speed | Large tactical / strike — runway required, heavy payloads | MQ-1C Gray Eagle, Bayraktar TB2, Shahed-136 |
| **Group 5** | > 1,320 lbs (600 kg) | > 18,000 ft MSL | Any speed | Strategic MALE/HALE — persistent global ISR, precision strike | MQ-9 Reaper, RQ-4 Global Hawk, MQ-4 Triton |

> **sUAS** (Small UAS) = Groups 1–3 combined. This is the DoD threshold aligned with FAA 14 C.F.R. Part 1 (max 55 lbs for small UAS).

---

## NATO — Three Class System

| Class | Sub-category | Max Weight | Normal Altitude | Typical Range | Role | Examples |
|-------|-------------|-----------|-----------------|--------------|------|---------|
| **Class I** | Micro | < 2 kg | < 200 ft AGL | < 5 km | Individual/squad ISR, indoor recon | Black Hornet, Nano Hummingbird |
| **Class I** | Mini | 2–20 kg | < 3,000 ft AGL | < 25 km | Platoon/company ISR, hand- or catapult-launched | DJI Matrice series, Skylark I |
| **Class I** | Small | 20–150 kg | < 5,000 ft AGL | < 50 km | Battalion ISR, small payload delivery | ScanEagle, Hermes 90 |
| **Class II** | Tactical | 150–600 kg | < 10,000 ft AGL | < 200 km | Brigade/division ISR, comms relay, targeting | Hermes 450, Watchkeeper, Heron 1 |
| **Class III** | MALE | > 600 kg | < 45,000 ft | Theater-wide | Theater persistent ISR, strike | MQ-9 Reaper, Heron TP, Bayraktar TB2 |
| **Class III** | HALE | > 600 kg | 45,000–65,000 ft | Global / unlimited | Strategic ISR, SIGINT, comms relay | RQ-4 Global Hawk, EuroHawk, Triton |
| **Class III** | Strike/Combat | > 600 kg | Variable | Variable | Precision strike, SEAD, escort | Neuron, nEUROn demonstrator |

---

## Cross-Reference: DoD Groups vs. NATO Classes

| DoD Group | NATO Class | Approx. Weight Band | Threat Context |
|-----------|-----------|---------------------|----------------|
| Group 1 | Class I – Micro/Mini | < 20 lbs / < 9 kg | Commercial off-the-shelf (COTS), FPV, swarm attack, ISR |
| Group 2 | Class I – Small | 21–55 lbs / 10–25 kg | Weaponized tactical UAS, loitering munitions (small) |
| Group 3 | Class II – Tactical | 55–1,320 lbs / 25–600 kg | One-way attack (OWA), larger loitering munitions |
| Group 4 | Class III – MALE (lower) | > 1,320 lbs / > 600 kg | State-actor strike UAS, Shahed-101/136 class |
| Group 5 | Class III – MALE/HALE | > 1,320 lbs / > 600 kg | Strategic ISR and strike, peer/near-peer adversaries |

---

## Threat Examples by Group

### Group 1 / NATO Class I Micro–Mini
- **DJI Mavic / Phantom series** — Commercial quadcopter; widely used for ISR and grenade drops in Ukraine and Middle East conflicts
- **FPV Racing Drones** — Modified first-person-view drones used as one-way attack munitions; < $500 cost
- **Switchblade 300** — U.S. loitering munition; hand-launched, ~6 lb, anti-personnel

### Group 2 / NATO Class I Small
- **Shahed-101** — Iranian fixed-wing; ~2.5m wingspan, used for ISR and light strike
- **RQ-20 Puma AE** — U.S. military SUAS; ~13 lbs, 2+ hr endurance, maritime/land ISR
- **ScanEagle** — Boeing/Insitu; ~44 lbs, 20+ hr endurance, ship-launched

### Group 3 / NATO Class II
- **RQ-7B Shadow** — U.S. Army; ~375 lbs, tactical ISR and targeting
- **RQ-21 Blackjack** — U.S. Navy/Marines; modular, ship-launched
- **Bayraktar Mini TB2** — Turkish; smaller variant of TB2 family

### Group 4 / NATO Class III (MALE lower tier)
- **Shahed-136 / Geran-2** — Iranian/Russian one-way attack drone; ~200 kg, 2,500 km range, used extensively in Ukraine
- **MQ-1C Gray Eagle** — U.S. Army; ~1,650 lbs, Hellfire-capable strike UAS
- **Bayraktar TB2** — Turkish; ~650 kg, precision strike, proven in multiple conflicts

### Group 5 / NATO Class III MALE–HALE
- **MQ-9 Reaper** — U.S. Air Force; ~4,700 lbs, 1,700 nm range, primary U.S. strike/ISR platform
- **RQ-4 Global Hawk** — U.S. Air Force; ~32,000 lbs, 60,000 ft ceiling, 24+ hr endurance
- **MQ-4 Triton** — U.S. Navy; maritime HALE ISR

---

## Key Terminology

| Term | Definition |
|------|-----------|
| **UAS** | Unmanned Aircraft System — includes the aircraft, ground control, and data links |
| **UAV** | Unmanned Aerial Vehicle — the aircraft component only |
| **sUAS** | Small UAS — DoD Groups 1–3, max 55 lbs; FAA Part 107 threshold |
| **MALE** | Medium Altitude Long Endurance — typically 10,000–45,000 ft, 24+ hr endurance |
| **HALE** | High Altitude Long Endurance — > 45,000 ft, global range |
| **OWA** | One-Way Attack — single-use loitering munition (also: kamikaze drone, suicide drone) |
| **Loitering Munition** | UAS that orbits a target area before terminal attack; blurs line between missile and drone |
| **FPV** | First-Person View — drone flown via live video feed; widely used as low-cost OWA |
| **COTS** | Commercial Off-The-Shelf — commercially available drones repurposed for military use |
| **C-UAS** | Counter-UAS — systems and tactics designed to detect, track, and defeat UAS threats |
| **AGL** | Above Ground Level |
| **MSL** | Mean Sea Level |

---

*Sources: U.S. DoD JP 3-30 Joint Air Operations; Congressional Research Service IF12797 (2024); NATO STANAG 4670 / JCG-UAS Classification (2009); U.S. Air Force sUAS Identification & Reporting Guide (2022)*
