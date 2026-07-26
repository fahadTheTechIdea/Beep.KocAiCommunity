# `cluster` — Cluster (k-means)

**Category:** Model · **Ports:** Table → Model · **Handler:** `ClusterHandler`

Unsupervised grouping — no label needed (e.g. well-log facies).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `clusters` | Clusters | Number | `3` | no | clamped 2–20 | ✅ yes |

## Panel on click
One number field, pre-filled `3`.

## Notes
Skipped in predict mode. Does not require a label, so it's the exception to the `RequireLabel` guard.
