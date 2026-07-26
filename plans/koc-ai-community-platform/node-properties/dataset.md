# `dataset` — Dataset

**Category:** Source · **Ports:** None → Table · **Handler:** `DatasetHandler`

The input rows flowing into the pipeline (well headers, sensor readings, the competition's training data).

## Parameters
_None._ The dataset node is the source; it carries all columns (features + label + id) into the graph.

## Panel on click
Shows the node summary only ("This node has no settings."). Displays the row/column count in its status after a run.

## Notes
Every workflow needs exactly one `dataset` (compiler-enforced).
