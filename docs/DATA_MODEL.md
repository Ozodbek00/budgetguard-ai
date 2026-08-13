# Data model

## Entities

### `Dataset`

One uploaded body of spending data, analysed as a unit.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string(200)` | Display name; defaults to the filename |
| `SourceFileName` | `string(400)` | Original filename, kept for provenance |
| `UploadedAtUtc` | `DateTimeOffset` | Stored as UTC ticks — see note below |
| `IsSyntheticDemo` | `bool` | True for generator output. Drives the warning banner |
| `RowCount` | `int` | Transactions accepted at ingest |

Analysis is always scoped to a dataset, because every baseline the engine uses
— the Benford expectation, peer-group means, vendor shares — is relative to the
population it is computed over. Merging two unrelated procurement exports into
one population would produce meaningless flags.

Datasets are **immutable once ingested**. There is no update path. This is what
makes caching an analysis result safe.

### `ProcurementTransaction`

One payment: from one department, to one vendor, in one category.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `DatasetId` | `Guid` | FK to `Dataset`, cascade delete, indexed |
| `ExternalReference` | `string(100)` | Source-system reference, preserved verbatim |
| `TransactionDate` | `DateOnly` | Payment or award date |
| `Amount` | `decimal` | Stored as text — see note below |
| `Currency` | `string(10)` | Defaults to `UZS` |
| `VendorName` | `string(300)` | Grouping key |
| `Category` | `string(200)` | Grouping key |
| `Department` | `string(300)` | Grouping key |
| `Description` | `string(1000)` | Free text, optional |

### Relationship

```
Dataset 1 ──── * ProcurementTransaction        (cascade delete)
```

Deliberately flat. Forensic analysis is column-oriented, and a flat shape maps
directly onto the CSV and Excel exports procurement portals actually publish.
Normalising vendors and categories into their own tables would add joins with no
analytical benefit — the detectors group by string key in memory regardless.

## Two storage decisions worth knowing

**`Amount` is stored as text.** The SQLite provider maps `decimal` to `TEXT`,
preserving exact digits. This is not incidental: Benford analysis reads the
*literal leading digit* of an amount, so a value round-tripped through a binary
floating-point type could come back with a different first digit than it went in
with, silently corrupting the primary detector.

**`UploadedAtUtc` is stored as UTC ticks.** SQLite has no native date type and
cannot `ORDER BY` a `DateTimeOffset`; the "newest dataset first" listing that
every screen depends on returned a 500 before this conversion was added. The
offset is not preserved, which is correct — this value is always UTC.

Both are provider-specific and flagged in `BudgetGuardDbContext` for anyone
moving to PostgreSQL. See [ADR 0002](adr/0002-sqlite-over-postgres-for-mvp.md).

---

## Input file schema

CSV or Excel (`.xlsx`), up to 32 MB. The first row must be a header row.

### Required columns

| Field | Accepted header names (case- and punctuation-insensitive) |
|---|---|
| `TransactionDate` | `TransactionDate`, `Date`, `PaymentDate`, `AwardDate`, `ContractDate`, `SignedDate`, `Period` |
| `Amount` | `Amount`, `Value`, `Sum`, `Total`, `ContractValue`, `PaymentAmount`, `Price` |
| `VendorName` | `VendorName`, `Vendor`, `Supplier`, `SupplierName`, `Contractor`, `ContractorName`, `Counterparty`, `Payee` |
| `Category` | `Category`, `SpendCategory`, `ProcurementCategory`, `GoodsCategory`, `Classification`, `Type` |
| `Department` | `Department`, `Agency`, `Ministry`, `Buyer`, `BuyerName`, `Organisation`, `Organization`, `Entity`, `ProcuringEntity` |

These five are required because the detectors have nothing to work with
otherwise: `Amount` is what is tested, and the other three are the peer groups
it is tested within.

### Optional columns

| Field | Accepted names | Default if absent |
|---|---|---|
| `ExternalReference` | `ExternalReference`, `Reference`, `Ref`, `ContractNumber`, `ContractNo`, `ContractId`, `PaymentReference`, `TransactionId`, `Id` | `ROW-00002`, from the file line number |
| `Currency` | `Currency`, `CurrencyCode`, `Ccy` | `UZS` |
| `Description` | `Description`, `Details`, `Subject`, `Purpose`, `Goods`, `Item`, `Notes` | empty |

Header matching strips spaces, underscores and punctuation, so `Vendor Name`,
`vendor_name` and `VENDORNAME` all resolve. Where a file contains two matching
headers, the leftmost wins.

### Example

```csv
ExternalReference,TransactionDate,Amount,Currency,VendorName,Category,Department,Description
BG-00001,2025-03-14,48750000,UZS,Oltin Yo'l Ta'minot,Construction,Ministry of Transport,Road resurfacing
BG-00002,2025-03-16,1250000.50,UZS,Toshkent Raqamli Tizimlar,IT Equipment,Ministry of Education,Laptops
```

### Parsing rules

**Amounts** tolerate currency symbols, spaces and either separator convention.
When both `.` and `,` appear, whichever comes last is the decimal separator —
so `1.234,56` and `1,234.56` both read as 1234.56. A lone comma is treated as a
thousands separator only when followed by exactly three digits.

**Dates** are tried against explicit formats before a general parse.
**Day-first orderings are tried before month-first**, because the target users
are Uzbek and wider CIS agencies where `dd.MM.yyyy` is standard. This is a
genuine ambiguity — `03/04/2025` is a different day under each convention — so
the choice is stated rather than left implicit. ISO `yyyy-MM-dd` is unambiguous
and preferred. Excel date serial numbers are also handled.

### Row-level handling

A row is **skipped, not fatal**, when its amount or date cannot be parsed, or
when vendor, category or department is blank. Real procurement exports routinely
contain a few malformed rows, and rejecting a 5,000-row file over three bad
dates would make the tool unusable — but silently dropping them would corrupt
the statistics. Both counts are reported, and the reasons are shown per row (up
to 50) on the upload screen.

A **missing required column is fatal**, and the error names both what was
required and what the file actually provided.

### Values the detectors treat specially

- **Zero and negative amounts** are excluded from Benford analysis (no
  first-digit information) and from concentration analysis (they would distort
  shares). They are counted and reported, never silently dropped. Under the
  default log comparison scale they are also excluded from outlier detection,
  since they have no logarithm.
- **Grouping keys** are trimmed and matched case-insensitively, so
  `" construction "` and `CONSTRUCTION` land in one peer group. Without this the
  minimum-group-size guard would silently suppress findings on untidy data.

---

## Demo data

The "Load demo dataset" button and `POST /api/datasets/demo` generate data with
`SyntheticDatasetGenerator`.

**This data is entirely fictional.** Vendor names, departments and amounts are
invented; no real supplier or agency is represented. It is labelled
`IsSyntheticDemo`, the dataset name itself carries the warning text, and a
banner appears on every screen that displays it. It must never be presented as
real government procurement data.

Default output: 1,710 transactions across 8 categories, 5 departments and 20
vendors, spanning 2025.

Four manipulations are planted deliberately, and the generator **records what it
planted** so the test suite can score the engine against ground truth:

| Kind | What is planted |
|---|---|
| `ThresholdEvasion` | 190 contracts priced between 80% and 99.8% of a 50,000,000 approval ceiling, over-representing leading digit 4 |
| `RoundNumberInvoicing` | 80 invoices from one vendor, all round multiples of 5,000,000 |
| `VendorConcentration` | 34 high-value Construction contracts funnelled to one supplier |
| `ExtremeOutlier` | 6 payments far above the entire normal range for their category |

The clean baseline is drawn as `10^u` with a uniform exponent — the standard
construction for Benford-conforming data — so the honest portion of the dataset
genuinely behaves like natural spending, and a false positive on it is a real
failure rather than an artefact of unrealistic test data.

Generation is deterministic for a fixed seed (default `20260813`), so demos and
tests are reproducible.

To write the demo data to CSV:

```bash
dotnet run --project tools/BudgetGuard.DataGenerator -- csv demo-procurement-data.csv
```
