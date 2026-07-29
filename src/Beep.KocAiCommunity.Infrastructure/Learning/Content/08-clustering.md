title: Group without labels
summary: Let the data tell you which wells, intervals, or operating states belong together — no labels, no target column, no assumptions about how many groups there are.
level: Intermediate
order: 8

## Lesson 1 — When you don't know the categories yet

Classification needs you to know the categories in advance. Clustering is for when you don't.

- *Which of our wells behave alike, so a lesson from one transfers to the others?*
- *How many distinct operating states does this compressor actually have?*
- *Do these log intervals fall into natural facies groups?*

The **`cluster`** node runs k-means: you say how many groups, and it finds them. No target column, no
labels, no split required — there is nothing to leak.

## Lesson 2 — What k-means actually does

It places k centre points among your data, assigns every row to its nearest centre, moves each centre
to the middle of the rows it captured, and repeats until nothing moves.

Two consequences follow, and both matter:

**Distance is everything.** A column measured in thousands dominates one measured in tenths purely
because of its units. **Standardize before clustering** — this is not optional advice here, it decides
your result.

**It always finds k groups.** Ask for four and you get four, whether or not four exist. The algorithm
never says "actually there are two". Judging that is your job.

## Lesson 3 — Choosing k

Start with what the operation suggests. If engineers already talk about "high rate, low rate, and
shut-in", try 3.

Then vary it. The `cluster` node reports two numbers on each run:

- **Average distance** — how tight the groups are. It always improves as k rises, so it cannot be
  used alone; at k = number of rows it reaches zero and means nothing.
- **Davies–Bouldin index** — how well separated the groups are relative to their spread. **Lower is
  better**, and unlike average distance it does not automatically improve with k.

Run k = 2, 3, 4, 5, 6 and write down the Davies–Bouldin each time. A clear dip is a real structure. A
flat line means the data has no strong grouping, which is itself a finding worth reporting.

## Lesson 4 — The judgement the numbers can't make

A statistically tidy clustering that no engineer recognises is not useful. Once you have candidate
groups, look at them:

- What is the average rate, pressure, depth in each?
- Do the wells in a group share something an engineer would name?
- Can you describe each group in one sentence a colleague would accept?

If you cannot, either the features are wrong — you clustered on things that don't distinguish
anything — or k is wrong. Interpretability is the acceptance test, not the index.

## Lesson 5 — Choosing features

Clustering uses every numeric feature equally, so what you include *is* the definition of similarity.

Include the things that should make two wells alike for your purpose. Exclude identifiers, dates, and
anything you're not prepared to have shape the answer. If you cluster on depth and location, you will
discover geography — true, and rarely what you wanted.

Fewer, well-chosen features usually beat everything you have. Use `select-columns` deliberately rather
than throwing the whole table at it. If a dozen sensors move together, `pca` first, then cluster on
the components.

## Lesson 6 — Build it yourself

1. `dataset` → `convert-numeric` → `replace-missing` → `standardize` → `cluster`
2. On `cluster`, set **clusters** to 3 and pick your feature columns.
3. Run it. Note the average distance and Davies–Bouldin.
4. Repeat for k = 2 through 6.
5. Take the best k and describe each group in one sentence.

Note there is no `split`, no `evaluate`, and no metric of correctness — there is no right answer to be
correct about. The output is a description of your data, and its value is whether it tells you
something you didn't know and can act on.

## Lesson 7 — Where clustering leads

Clustering is often the first step rather than the last:

- **Segment, then model.** Build a separate decline forecast per group; a model fitted to one
  behaviour usually beats one averaged across all of them.
- **Find the odd one out.** A group with two members among thousands is worth a look — though for that
  purpose proper anomaly detection is the better tool.
- **Frame a classification.** Once the groups have names an engineer accepts, they become labels, and
  you can predict them for new wells with the *Predict a category* track.
