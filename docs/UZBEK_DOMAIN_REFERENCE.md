# Uzbek domain reference

Calibration data and domain vocabulary for tuning BudgetGuard against Uzbek
public spending. Salvaged from an earlier July 2026 prototype of this project
before that working tree was removed; kept here because it is the part of that
prototype worth keeping — the code was superseded, the domain research was not.

**None of the figures below are verified against Uzbek procurement law.** They
are plausible working assumptions from the prototype, recorded so that whoever
tunes the tool for a real deployment knows what was assumed and can check it.

---

## Competition context

The project targets **Uzbekistan's President AI Award, Government AI track**.
That audience shapes two things: findings must be readable in Uzbek and Russian
(they are — see `docs/CODING_STANDARDS.md` rule 5), and the framing is oversight
of public money rather than generic anomaly detection.

## Administrative regions

The 14 top-level administrative regions: 12 provinces, Tashkent City, and the
Republic of Karakalpakstan.

| | |
|---|---|
| Karakalpakstan | Republic |
| Andijan, Bukhara, Jizzakh, Kashkadarya, Navoiy, Namangan | Provinces |
| Samarkand, Surkhandarya, Syrdarya, Tashkent Region, Fergana, Khorezm | Provinces |
| Tashkent City | City of republican significance |

Useful as a **grouping dimension the current model does not have**. Today a
transaction has `Department`; region is a genuinely different axis — a supplier
capturing one province's road budget is a different finding from one capturing a
ministry's. Adding it means a new column, a new `OutlierGrouping`, and a new
`ConcentrationScope`.

## Spending categories

Education · Healthcare · Construction · Transport · IT · Social Protection

Broader than the current demo generator's eight procurement-flavoured categories
(Construction, Medical Supplies, IT Equipment, Office Supplies, Vehicle Fleet,
Catering Services, Road Maintenance, Textbooks). The prototype's list maps to
budget *functions*; the current one maps to *what was bought*. Both are valid;
real data will dictate which.

## Vendor naming

Uzbek company names carry legal-form suffixes that make synthetic data read as
authentic: **MChJ** (LLC), **XK** (private enterprise), plus Latin-script
transliterations. Category-appropriate examples from the prototype:

| Category | Examples |
|---|---|
| Education | Bilim Media Group LLC · Maktab Ta'minot XK · Ilm Ziyo Trade |
| Healthcare | Med Farm Distribution · Shifo Medical Supply · Diagnostika Plus MChJ |
| Construction | Qurilish Invest MChJ · Mustahkam Bino LLC · Yo'l Qurilish Trest |
| Transport | Avto Yo'l Servis · Temir Yo'l Ta'minot · Karvon Logistics |
| IT | Raqamli Yechim LLC · Uz Soft Systems · Tarmoq Texnologiya |
| Social Protection | Ijtimoiy Ta'minot Fond · Nafaqa Servis Markaz · Mehr Nuri Charity Ops |

The current generator draws vendors from one flat pool independent of category.
Matching vendors to categories would be more realistic — and would make
concentration findings sharper, since a vendor would then compete in one arena
rather than all of them. It also **changes the statistics**: fewer vendors per
category raises the even-split expectation and therefore moves every
concentration threshold. Not a cosmetic change; re-verify the ground-truth tests
after making it.

## Amount calibration

The prototype modelled amounts as log-normal over natural-log so'm:

| Parameter | Value | Meaning |
|---|---|---|
| mu | 16.8 | median ≈ 19.8M so'm |
| sigma | 1.45 | spans several orders of magnitude |
| Approval threshold | 100,000,000 so'm | the gate structuring tries to dodge |

The current generator instead draws `10^u` with a uniform exponent, which is
*exactly* Benford-conforming rather than approximately so — deliberate, because
the clean baseline has to be genuinely clean for the false-positive tests to
mean anything (see `docs/DETECTION_METHODOLOGY.md`). Log-normal is the more
realistic shape and the better choice once validated against real data.

The two prototypes assumed different approval ceilings — 100M so'm here,
50M so'm in the current generator. **Confirm the real figure before quoting
either in a pitch**; it is the single number a domain expert in the room is most
likely to know.
