# AI Digital Campus — Prototype Demo Script

A tight **3–4 minute** live demo for the "What we built" moment (deck slide 5). Goal: show the
**Learn → Compete → Build** loop working on real KOC-style problems — not to tour every screen. Keep
narration short; let the screen do the talking.

> **Golden rule:** rehearse it twice, know your click path, and have the fallback screenshots ready.

---

## Before the meeting (setup checklist)

- [ ] App running and reachable; sign-in working (Windows SSO or the persona switcher in dev).
- [ ] **Demo data seeded** (Admin → Demo data → *Seed*) so competitions, a leaderboard, and datasets exist.
- [ ] Signed in as a **Platform Admin / power** persona so everything is visible.
- [ ] Browser zoom ~110–125%, window maximized, notifications silenced, single clean tab.
- [ ] Know the **featured competition** name and that its countdown is live.
- [ ] **Fallback:** the illustrated help page / screenshots open in a second tab in case of a glitch.
- [ ] Optional: a pre-built pipeline ready to submit, to save time.

---

## The run of show (~3–4 min)

**1 · Land on the arena (20s).** Open **Home**.
> "This is what an employee sees — the platform leads with a live competition: a countdown to reveal
> day, the current top-three, and how many colleagues are taking part."

**2 · Open a competition (30s).** Click **Enter** on the featured challenge → the competition page.
> "Every challenge is a real KOC-style problem — here, [predicting ESP failures]. There's the data to
> download, the scoring metric, and what you can win."

**3 · Show the live leaderboard (30s).** Open the **Leaderboard** tab.
> "Submissions are scored automatically on data we hold back, so only modelling skill moves your rank.
> The board updates live, with medals and movement — this is what drives the friendly competition."

**4 · Build a model — no code (60–75s).** Go to **Studio → AutoML** (fast path) *or* open a workflow in
the **designer**.
> "You don't need to be a programmer. With AutoML you pick a dataset and a task and the platform trains
> and compares models for you. For power users, the visual designer wires data → prepare → train →
> evaluate as a pipeline." *(Run a quick AutoML job or open a ready pipeline.)*

**5 · Submit and see the impact (30s).** Submit the pipeline to the competition (or show a prior
submission) → return to the **Leaderboard**.
> "Submitting trains and scores it against the hidden data, and the leaderboard reflects it. The same
> activity that trains our people can produce a model we actually use."

**6 · One line on governance (15s).** Briefly show **Admin** (RBAC / audit) or say it.
> "And it's all inside KOC — single sign-on, role-based access, data never leaves, full audit trail."

**Close the demo:** return to the deck.
> "That's the loop: learn, build, compete — on our data, in-house."

---

## Talking-point cheat sheet

- **"Private Kaggle for KOC."** Real problems, objective scoring, live leaderboard.
- **No coding required.** AutoML for beginners; the visual designer for depth.
- **Hidden test data.** Only skill moves your rank — credible and fair.
- **Sovereign.** Data and models never leave KOC; SSO, RBAC, audit.
- **Standard .NET, self-hosted.** Low cost, no per-seat fees.

## If something breaks

- Switch to the **second tab** with the illustrated help / screenshots and narrate from there.
- Don't debug live — say "let me show you that from our reference," and continue.
- Keep going; the story matters more than any single click.

---
*Pairs with `WHITE_PAPER_SLIDE_OUTLINE.md` (slide 5) and the deck. Bracketed items to confirm.*
