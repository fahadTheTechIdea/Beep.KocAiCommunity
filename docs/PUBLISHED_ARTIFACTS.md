# Published artifacts

Every page published to claude.ai for this initiative, and the HTML in this repository it is built
from. **The file is the source; the URL is a copy of it.** Edit the file, then republish to the same
URL — a new URL means a second document that will drift from the first.

All of these use the **KOC blueprint theme** taken from
`src/Beep.KocAiCommunity.Ui.Shared/wwwroot/css/koc-blueprint.css` and `Branding/KocBrand.cs` — accent
`#1466A5`, secondary teal `#1F8A8C`, ink `#10222F`, surface `#F6F8FA`. Anything published for this
initiative should look like the product it describes. Read those files rather than copying the values
here; they can change.

## Documents

| Document | Source in this repo | Published | State |
|---|---|---|---|
| **Project documentation** — every project document in one searchable page | [`project-documentation.html`](project-documentation.html) | [open](https://claude.ai/code/artifact/62bd1398-baff-4a7c-be45-f1dd401f3080) | **Generated** — rebuild with `build-project-documentation.py`, do not hand-edit |
| **Security deployment review** — for KOC information-security | [`security-deployment-review.html`](security-deployment-review.html) | [open](https://claude.ai/code/artifact/264078d2-b51e-4964-add6-1b9fc2fbcd7d) | Current — revised 4 Aug 2026 for the API/website merge |
| **Security — KOC Studio (desktop)** | [`security-desktop.html`](security-desktop.html) | [open](https://claude.ai/code/artifact/f7285199-de5a-4d9b-94b0-f180a17d4a3e) | **Generated** from `SECURITY_DESKTOP.md` — rebuild with `build-doc-page.py` |
| **Deployment — KOC Studio (desktop)** | [`deployment-desktop.html`](deployment-desktop.html) | [open](https://claude.ai/code/artifact/ac00728f-ebba-4c84-bfcc-7358c20ef7b0) | **Generated** from `DEPLOYMENT_DESKTOP.md` — rebuild with `build-doc-page.py` |
| **TFT prototype review request** — email to the task force | [`comms/tft-prototype-review-email.html`](comms/tft-prototype-review-email.html) | [open](https://claude.ai/code/artifact/0dfbc9e4-fa16-4997-927a-72fcbad7568f) | Current |
| **White paper** | [`comms/ai-digital-campus-white-paper.html`](comms/ai-digital-campus-white-paper.html) | [open](https://claude.ai/code/artifact/1b60d51b-4578-4c71-bedf-8f89b3f44604) | ⚠️ Repo edited (two containers, not three); **republish pending** |
| **Executive brief** (plain language) | [`comms/ai-digital-campus-exec-brief.html`](comms/ai-digital-campus-exec-brief.html) | [open](https://claude.ai/code/artifact/66d73e46-8349-4587-9fcb-7596f1cd1e42) | Not re-checked since 23 Jul; still on an older palette |
| **الملخّص التنفيذي** — executive brief, Arabic | [`comms/ai-digital-campus-exec-brief-ar.html`](comms/ai-digital-campus-exec-brief-ar.html) | [open](https://claude.ai/code/artifact/c1c86f0b-dea3-4d40-9a0c-aa06856fab65) | Not re-checked since 29 Jul; still on an older palette |
| **Management deck** | [`comms/ai-digital-campus-deck.html`](comms/ai-digital-campus-deck.html) | [open](https://claude.ai/code/artifact/8615eba7-3405-4d81-abde-395441aa1ed5) | Not re-checked since 23 Jul; still on an older palette |
| **KOC Studio help** — screenshot tour + developer orientation | [`help/index.html`](help/index.html) | [open](https://claude.ai/code/artifact/70c05686-8020-44cd-9531-5ee425cf26d0) | ⚠️ Repo edited (the API run commands were impossible); **republish pending** |

Not listed, deliberately:

- **Beep.Godot — Per-Genre Scene Connectivity & Events** — published under the same account but belongs
  to a different project, so its source does not live here.
- **The demo-hosting runbook** — the prototype is shown from a temporary third-party host so people can
  click through it. That arrangement is scaffolding, not architecture: the working notes live under
  `deploy/`, out of the documentation set, and no document should present it as a deployment target.
  The real target is in [`DEPLOYMENT.md`](DEPLOYMENT.md).

## Republishing

Publish the file to the **existing URL**. A conversation that did not itself publish an artifact has to
pass the URL explicitly, or it mints a new one.

If a republish is refused because the session has not seen the latest version, someone else published
from another session: read the live page first, re-apply the change on top of it, then publish. Do not
force — that discards their work.

## Adding one

**From a markdown document** (the usual case) — add a row to `PAGES` in
[`build-doc-page.py`](build-doc-page.py) and run `python docs/build-doc-page.py`. It renders a
standalone KOC-themed page; you do not write any HTML.

**Hand-authored** — write the HTML into `docs/` (or `docs/comms/` if it is management-facing), themed
as above.

Then publish it, and add a row here — plus one in [`comms/README.md`](comms/README.md) if it belongs to
the communications pack.
