title: Predict a category
summary: Will this pump fail? Which facies is this interval? Learn classification end to end, and every algorithm the Studio offers for it.
level: Intermediate
order: 4

## Lesson 1 — Questions with a categorical answer

Classification answers questions whose answer is one of a fixed set of options.

- **Binary** — two options. *Will this ESP fail in the next 30 days: yes or no?*
- **Multiclass** — several. *Which facies is this well-log interval: sand, shale, or carbonate?*

The difference matters in the Studio because it changes which algorithms apply and which metrics mean
anything. Set it on the Train node's **Task** field, or from the task chip in the toolbar.

What you need: one column holding the answer (the **label**), and columns that might explain it (the
**features**). Nothing else.

## Lesson 2 — The shape of a classification pipeline

Every classification pipeline is a variation on six nodes:

```
dataset → (preparation) → split → train → score → evaluate
```

- **`dataset`** — the rows.
- **preparation** — whatever the *Prepare the data* track taught you the data needs.
- **`split`** — holds back rows the model never sees. Non-negotiable; the platform enforces it.
- **`train`** — set Task to Binary or Multiclass, pick the target column and an algorithm.
- **`score`** and **`evaluate`** — apply the model to the held-out rows and report the metrics.

Build that once with the defaults before changing anything. A working baseline you understand beats a
clever pipeline you don't.

## Lesson 3 — The linear algorithms

These learn a weighted sum of your features. Fast, small, and easy to reason about.

**`sdca`** — the default, and a good one. Handles both binary and multiclass. Two knobs matter: **L1
regularization** pushes unhelpful feature weights to zero (useful when you have many columns and
suspect most are noise), and **L2** keeps all weights small. Blank means "let it choose".

**`lbfgs`** — logistic regression for binary, maximum entropy for multiclass. Often a little more
accurate than SDCA on smaller datasets, and slower. **History size** trades memory for convergence
quality; 20 is fine.

**`sgd`** — stochastic gradient descent, calibrated. Reach for it on very large datasets where the
others are too slow. Sensitive to **learning rate**: too high and it never settles, too low and it
never arrives.

**`perceptron`** — averaged perceptron. Simple, fast, binary only. Rarely the best choice, but it
trains almost instantly and makes a useful sanity check.

> Linear algorithms care about scale. Standardize your numeric columns first, or a column measured in
> thousands will dominate one measured in tenths for no reason but its units.

## Lesson 4 — The tree algorithms

These learn a sequence of yes/no questions — *is intake pressure above 700? then is vibration above
2.1?* — which is much closer to how an engineer reasons.

**`fasttree`** — gradient-boosted trees. Usually the strongest performer on tabular sensor data, and
the one to try when the linear models plateau. Knobs: **trees** (more is stronger and slower),
**leaves per tree** (more captures finer detail and overfits sooner), **min rows per leaf** (raise it
when the model is memorising noise), and **learning rate**.

**`fastforest`** — bagged trees. Less prone to overfitting than FastTree and usually a little weaker.
A good choice when your dataset is small and you are worried about it.

**`ova-fasttree`** — one-vs-all FastTree, for multiclass. FastTree is natively binary, so this trains
one tree model per class and takes the most confident. Slower, but it brings tree strength to a
multiclass problem.

Trees do **not** need scaling — they only compare values within a column. If you are using trees, you
can skip `standardize` entirely.

## Lesson 5 — The interpretable and the specialised

**`gam`** — generalised additive model. Learns a separate shaped curve per feature and adds them up.
Its advantage is that you can *look at* those curves and see exactly how each sensor drives the
prediction. When you have to explain a model to an operations review, this is the one to bring.

**`naivebayes`** — multiclass only. Assumes features are independent, which is almost never true, yet
it works surprisingly well on text and trains in almost no time. Worth a run before anything heavier.

## Lesson 6 — Which metric to believe

**Accuracy** is the share of correct predictions. It is intuitive and, on imbalanced data, actively
misleading. If 2% of pumps fail, predicting "no failure" always is 98% accurate and useless.

**AUC** asks a better question: if you take one failing pump and one healthy pump, how often does the
model rank the failing one as more likely? 0.5 is a coin flip, 1.0 is perfect. It is unaffected by how
rare the positive class is, which is why it is the honest metric for failure prediction.

For multiclass, **MicroAccuracy** counts every row equally; **MacroAccuracy** counts every *class*
equally. A large gap between them means the model is doing well on common classes and ignoring rare
ones — which may be exactly backwards from what you need.

## Lesson 7 — Comparing algorithms fairly

Change one thing at a time, and hold the split constant.

1. Baseline with `sdca` and defaults. Write the number down.
2. Switch to `fasttree`. Write it down.
3. Try `fastforest` and `gam`.
4. Only then start adjusting hyperparameters, on whichever won.

The split is seeded, so the held-out rows are identical between runs and the comparison is fair. If
you change the preparation, all previous numbers are void — start the comparison again.

Use `cross-validate` if the differences are small and you are not sure they are real.

## Lesson 8 — Take it to a competition

You now know enough to enter. Open **Compete**, choose an active classification competition, and press
*Join & build your pipeline* — the Studio opens on the host's data with the target, id, and task
already fixed.

Build the six-node pipeline. Run it to see your local metric. Then submit: your pipeline is trained
and scored on a hidden test set nobody can tune against, and your best result goes on the leaderboard.

That gap between your local score and your leaderboard score is the most instructive number you will
see all week.
