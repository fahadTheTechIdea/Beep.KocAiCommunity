# Vendored competition data

## `titanic.csv`

The real Titanic passenger manifest — 891 passengers, the training half of the well-known Kaggle
"Titanic: Machine Learning from Disaster" set, itself derived from the public passenger records
compiled by the Encyclopedia Titanica.

| | |
|---|---|
| Source | <https://raw.githubusercontent.com/datasciencedojo/datasets/master/titanic.csv> |
| Retrieved | 2026-08-04 |
| MD5 | `61fdd54abdbf6a85b778e937122e1194` |
| Size | 60,302 bytes — 891 rows, 12 columns |
| Licence | Public domain. The passenger list is historical record, not an authored work. |

**Stored byte-for-byte as retrieved**, so the checksum above verifies it against the public copy.
`CompetitionSeeder.BuildTitanic()` does the column projection and the train/evaluation split in code;
nothing here is edited.

Why the real thing rather than a generated imitation: this is the one challenge that trades on being
*the* famous first problem, and a synthetic stand-in undercuts that. It also arrives with mess nobody
has to invent — **177 of the 891 ages are blank** (19.9%) and two passengers have no port of
embarkation — which is exactly the cleaning work the challenge should teach. The generated datasets get
their gaps from `CompetitionSeeder.Mess`; this one came by them honestly.

Every other competition dataset is generated deterministically at seed time. See `CompetitionSeeder`.
