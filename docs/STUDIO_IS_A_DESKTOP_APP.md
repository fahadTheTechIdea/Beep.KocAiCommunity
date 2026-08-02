# The Studio is a desktop app

**Decided 2026-08-02.** Everything used to build and train a model moved out of the website and into
**KOC Studio**, the WinForms desktop app. The website keeps what a browser is good at: reading,
discussing, and following a competition.

## Why

Two things had already pushed in this direction before it was made explicit.

The shared hosting that will run the pilot **cannot run the Worker**, so all model training already
happened on the desktop (Phase 04). And the desktop had grown its own dataset import, run history,
model registry and offline submission queue (Phases 02, 04, 05, 06). The website's Studio pages were
increasingly the weaker copy of a thing that worked better elsewhere — two front doors to one workflow,
with the browser one unable to train.

## What moved

| Was, on the website | Now |
|---|---|
| `/datasets` | Desktop — imports with encoding/delimiter detection, profiling, and rename |
| `/workflows`, `/workflow/{id}` | Desktop — the node designer, unchanged |
| `/studio` (AutoML) | Desktop `/automl` — trains in a child process with a memory ceiling |
| `/runs` | Desktop — run history in the workspace, comparable, surviving restarts |
| `/models` | Desktop — local registry, predictions, export/import |
| `/nodes` (node catalog) | Desktop — reads the local registry, so it works offline |
| `/experiments` | Desktop — still server-backed, so it needs the network |

`Beep.KocAiCommunity.Ui.Studio` is no longer referenced by the Web. It is a desktop-only RCL now, and
the node catalog and experiments pages live inside it so both effects follow from that one reference.

## What the website kept

Home, Learn, Community, Compete and competition detail, Dashboard, Profile, Supervision, Admin, Help.
Learning and Community remain open to everyone with no sign-in, unchanged.

## Competing from a browser

The competition page used to have one way in: **Join & build your pipeline**, which created a workflow
and opened the designer. That designer is not on the website any more, so the button is gone.

What replaces it is honest rather than clever — the page says pipelines are built in KOC Studio, and
still shows the data, the rules, the live leaderboard, your own submissions, and **the predictions-file
upload**. So a browser is still enough to compete with any tool you like; it is only the node graph
that needs the desktop.

> **This raises the stakes on Phase 07.** Until there is an installer, the people who can build a
> pipeline are the people who can build the solution. That was tolerable when the website offered the
> same thing; it is not any more. **07 is now the gate on competing at all for most people.**

## Consequences worth knowing

- **Server-side training endpoints are still there.** `/api/v1/studio/*` works and is used by the
  desktop when online; nothing was removed from the API. Only the web UI went.
- **49 Arabic translations were pruned** as orphans of the deleted pages. They are in git history if a
  page ever comes back.
- **Experiments is online-only on a mostly-offline app.** It is server-backed and was moved as-is. The
  desktop's own run history is the offline equivalent and is the better tool for a local run; the two
  overlap and probably want reconciling.
- **The desktop app bar is full.** Node catalog and Experiments went behind an overflow menu rather
  than becoming the ninth and tenth buttons. The navigation wants a proper rethink — a sidebar, most
  likely — and that belongs with the accessibility pass in Phase 08.
