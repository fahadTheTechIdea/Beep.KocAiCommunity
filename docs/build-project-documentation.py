"""Builds docs/project-documentation.html — every project document in one self-contained page.

Run from the repository root:  python docs/build-project-documentation.py
Then republish the result to the URL in docs/PUBLISHED_ARTIFACTS.md.
"""
import html
import os
import re

import markdown

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

GROUPS = [
    ("Start here", [
        ("README.md", "Project overview", "What the platform is, how it is put together, how to run it"),
    ]),
    ("Guides", [
        ("docs/USER_GUIDE.md", "User guide", "For employees: signing in, learning, competing"),
        ("docs/DEVELOPER_GUIDE.md", "Developer guide", "Architecture, running locally, the node engine"),
        ("docs/ADMIN_GUIDE.md", "Administrator guide", "Admin console, RBAC, competition governance"),
    ]),
    ("Operations", [
        ("docs/DEPLOYMENT.md", "Deployment — website", "Containers, migrations, seeding, configuration"),
        ("docs/SECURITY.md", "Security — website", "Identity, tokens, RBAC, secrets, accepted risks"),
        ("docs/SECURITY_DESKTOP.md", "Security — desktop", "Why the workstation trust model differs, and the SQL identity to give it"),
        ("docs/DEPLOYMENT_DESKTOP.md", "Deployment — desktop", "Prerequisites, offline vs connected, distribution, upgrades"),
        ("docs/PUBLISHED_ARTIFACTS.md", "Published artifacts", "Every page on claude.ai and the file it is built from"),
    ]),
    ("Decisions", [
        ("docs/DESKTOP_DIRECT_DATABASE.md", "Desktop reads the database", "Why Studio dropped the API"),
        ("docs/STUDIO_IS_A_DESKTOP_APP.md", "Studio is a desktop app", "Where model building happens"),
    ]),
    ("Communications", [
        ("docs/comms/README.md", "Comms pack index", "What each document is for, and who it is for"),
        ("docs/comms/AI_DIGITAL_CAMPUS_WHITE_PAPER.md", "White paper", "The full case for the AI Digital Campus"),
        ("docs/comms/AI_DIGITAL_CAMPUS_EXECUTIVE_BRIEF.md", "Executive brief", "The short version, for leadership"),
        ("docs/comms/AI_DIGITAL_CAMPUS_EXECUTIVE_BRIEF.ar.md", "الملخّص التنفيذي", "Executive brief, Arabic"),
        ("docs/comms/AI_DIGITAL_CAMPUS_PILOT_PLAN.md", "Pilot plan", "How the first cohort runs"),
        ("docs/comms/EXECUTIVE_MEMO.md", "Executive memo", "The ask, on one page"),
        ("docs/comms/ANNOUNCEMENT_EMAIL.md", "Announcement email", "Launch note to staff"),
        ("docs/comms/PROTOTYPE_DEMO_SCRIPT.md", "Demo script", "Running the prototype walkthrough"),
        ("docs/comms/TFT_KICKOFF_AGENDA_AND_TOR.md", "TFT kickoff & ToR", "Task force agenda and terms of reference"),
        ("docs/comms/TFT_KICKOFF_EMAIL.md", "TFT kickoff email", "Invitation to the task force"),
        ("docs/comms/TFT_PROTOTYPE_REVIEW_EMAIL.md", "TFT prototype review request", "Asks the task force to try the live site and answer five questions"),
        ("docs/comms/WHITE_PAPER_SLIDE_OUTLINE.md", "Slide outline", "White paper as a deck"),
    ]),
    ("Localisation", [
        ("docs/ARABIC_REVIEW.md", "Arabic review", "Every Arabic string, reviewed line by line"),
    ]),
]

MD = markdown.Markdown(extensions=["tables", "fenced_code", "sane_lists", "attr_list", "toc", "md_in_html"])


def slug(path):
    return re.sub(r"[^a-z0-9]+", "-", path.lower()).strip("-")


def read(path):
    full = os.path.join(ROOT, path)
    with open(full, encoding="utf-8") as fh:
        return fh.read()


docs, nav = [], []
for group, entries in GROUPS:
    items = []
    for path, title, blurb in entries:
        if not os.path.exists(os.path.join(ROOT, path)):
            print("  MISSING", path)
            continue
        raw = read(path)
        MD.reset()
        body = MD.convert(raw)
        words = len(raw.split())
        ident = slug(path)
        docs.append((ident, path, title, blurb, words, body))
        items.append((ident, title, blurb, words))
    if items:
        nav.append((group, items))

print(f"  {len(docs)} documents, {sum(d[4] for d in docs):,} words")

nav_html = []
for group, items in nav:
    rows = "".join(
        f'<li><a class="nav-link" href="#{i}" data-doc="{i}">'
        f'<span class="nav-title">{html.escape(t)}</span>'
        f'<span class="nav-blurb">{html.escape(b)}</span>'
        f'<span class="nav-meta">{w:,} words</span></a></li>'
        for i, t, b, w in items
    )
    nav_html.append(f'<section class="nav-group"><h2>{html.escape(group)}</h2><ul>{rows}</ul></section>')

articles = []
for ident, path, title, blurb, words, body in docs:
    articles.append(
        f'<article class="doc" id="{ident}" hidden dir="auto">'
        f'<header class="doc-head">'
        f'<p class="doc-path">{html.escape(path)}</p>'
        f'<p class="doc-blurb">{html.escape(blurb)} &middot; {words:,} words</p>'
        f"</header>{body}</article>"
    )

TEMPLATE = """<title>KOC A.I. Digital Campus (Community) — Project Documentation</title>
<style>
:root {
  --accent: #1466A5; --accent-soft: #5FA3D4; --deep: #0B2E4C; --teal: #1F8A8C;
  --ink: #10222F; --ink-soft: #5A6B78; --rule: #E1E7EC;
  --ground: #FFFFFF; --panel: #F6F8FA; --raised: #FFFFFF; --grid: rgba(20,102,165,.055);
  --sans: ui-sans-serif, system-ui, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  --mono: ui-monospace, "Cascadia Mono", "SF Mono", Consolas, "Liberation Mono", monospace;
  --measure: 68ch;
}
@media (prefers-color-scheme: dark) {
  :root {
    --accent: #5FA3D4; --accent-soft: #8FC4E6; --deep: #A8CDE6; --teal: #4FB3B5;
    --ink: #DCE6EE; --ink-soft: #93A5B4; --rule: #23384A;
    --ground: #0B1620; --panel: #101E29; --raised: #12222E; --grid: rgba(95,163,212,.05);
  }
}
:root[data-theme="dark"] {
  --accent: #5FA3D4; --accent-soft: #8FC4E6; --deep: #A8CDE6; --teal: #4FB3B5;
  --ink: #DCE6EE; --ink-soft: #93A5B4; --rule: #23384A;
  --ground: #0B1620; --panel: #101E29; --raised: #12222E; --grid: rgba(95,163,212,.05);
}
:root[data-theme="light"] {
  --accent: #1466A5; --accent-soft: #5FA3D4; --deep: #0B2E4C; --teal: #1F8A8C;
  --ink: #10222F; --ink-soft: #5A6B78; --rule: #E1E7EC;
  --ground: #FFFFFF; --panel: #F6F8FA; --raised: #FFFFFF; --grid: rgba(20,102,165,.055);
}
* { box-sizing: border-box; }
body {
  margin: 0; background: var(--ground); color: var(--ink);
  font-family: var(--sans); font-size: 16px; line-height: 1.65;
  -webkit-font-smoothing: antialiased;
  background-image: linear-gradient(var(--grid) 1px, transparent 1px),
                    linear-gradient(90deg, var(--grid) 1px, transparent 1px);
  background-size: 28px 28px;
}
.masthead {
  border-bottom: 1px solid var(--rule); background: var(--raised);
  padding: 22px clamp(18px, 4vw, 40px);
  display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 18px;
}
.masthead h1 { font-size: 1.12rem; margin: 0; letter-spacing: -.01em; font-weight: 650; text-wrap: balance; }
.masthead .rule-tag {
  font-family: var(--mono); font-size: .66rem; letter-spacing: .1em; text-transform: uppercase;
  color: var(--accent); border: 1px solid var(--accent); border-radius: 2px; padding: 2px 7px;
}
.masthead p { margin: 0; color: var(--ink-soft); font-size: .84rem; }
.shell { display: grid; grid-template-columns: minmax(240px, 310px) minmax(0, 1fr); align-items: start; }
@media (max-width: 900px) { .shell { grid-template-columns: 1fr; } }
.index {
  border-right: 1px solid var(--rule); background: var(--panel);
  padding: 18px clamp(14px, 2vw, 22px) 60px;
  position: sticky; top: 0; max-height: 100vh; overflow-y: auto;
}
@media (max-width: 900px) { .index { position: static; max-height: none; border-right: 0; border-bottom: 1px solid var(--rule); } }
#filter {
  width: 100%; font-family: var(--mono); font-size: .8rem; color: var(--ink);
  background: var(--raised); border: 1px solid var(--rule); border-radius: 3px;
  padding: 9px 11px; margin-bottom: 20px;
}
#filter:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
.nav-group { margin-bottom: 22px; }
.nav-group h2 {
  font-family: var(--mono); font-size: .64rem; letter-spacing: .13em; text-transform: uppercase;
  color: var(--ink-soft); margin: 0 0 9px; padding-bottom: 6px; border-bottom: 1px solid var(--rule);
}
.nav-group ul { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 1px; }
.nav-link {
  display: grid; gap: 1px; padding: 8px 10px; border-radius: 3px;
  text-decoration: none; color: inherit; border-left: 2px solid transparent;
}
.nav-link:hover { background: var(--raised); }
.nav-link:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }
.nav-link[aria-current="true"] { background: var(--raised); border-left-color: var(--accent); }
.nav-link[aria-current="true"] .nav-title { color: var(--accent); }
.nav-title { font-size: .87rem; font-weight: 600; line-height: 1.3; }
.nav-blurb { font-size: .74rem; color: var(--ink-soft); line-height: 1.4; }
.nav-meta { font-family: var(--mono); font-size: .64rem; color: var(--ink-soft); opacity: .75; font-variant-numeric: tabular-nums; }
main { padding: 32px clamp(18px, 5vw, 60px) 96px; min-width: 0; }
.doc { max-width: var(--measure); }
.doc[hidden] { display: none; }
.doc-head { border-bottom: 1px solid var(--rule); padding-bottom: 14px; margin-bottom: 30px; }
.doc-path { font-family: var(--mono); font-size: .72rem; color: var(--accent); margin: 0 0 4px; word-break: break-all; }
.doc-blurb { font-family: var(--mono); font-size: .7rem; color: var(--ink-soft); margin: 0; font-variant-numeric: tabular-nums; }
.doc h1, .doc h2, .doc h3, .doc h4 { line-height: 1.25; text-wrap: balance; margin: 1.9em 0 .5em; letter-spacing: -.012em; }
.doc h1 { font-size: 1.72rem; margin-top: 0; }
.doc h2 { font-size: 1.24rem; padding-bottom: 6px; border-bottom: 1px solid var(--rule); }
.doc h3 { font-size: 1.03rem; }
.doc h4 { font-size: .92rem; color: var(--ink-soft); }
.doc p, .doc li { overflow-wrap: break-word; }
.doc a { color: var(--accent); text-underline-offset: 2px; }
.doc strong { font-weight: 650; }
.doc code {
  font-family: var(--mono); font-size: .85em; background: var(--panel);
  border: 1px solid var(--rule); border-radius: 3px; padding: .1em .35em;
}
.doc pre {
  background: var(--panel); border: 1px solid var(--rule); border-left: 2px solid var(--teal);
  border-radius: 3px; padding: 13px 15px; overflow-x: auto;
}
.doc pre code { background: none; border: 0; padding: 0; font-size: .8rem; line-height: 1.55; }
.doc blockquote {
  margin: 1.3em 0; padding: 2px 0 2px 16px;
  border-left: 2px solid var(--accent); color: var(--ink-soft);
}
.doc blockquote p:first-child { margin-top: 0; } .doc blockquote p:last-child { margin-bottom: 0; }
.table-scroll { overflow-x: auto; margin: 1.3em 0; }
.doc table { border-collapse: collapse; font-size: .86rem; min-width: 100%; }
.doc th, .doc td { border: 1px solid var(--rule); padding: 7px 11px; text-align: start; vertical-align: top; }
.doc th { background: var(--panel); font-family: var(--mono); font-size: .7rem; letter-spacing: .05em; text-transform: uppercase; color: var(--ink-soft); font-weight: 600; }
.doc hr { border: 0; border-top: 1px solid var(--rule); margin: 2.2em 0; }
.doc img { max-width: 100%; height: auto; }
.doc :target { scroll-margin-top: 20px; }
.empty { font-family: var(--mono); font-size: .78rem; color: var(--ink-soft); padding: 10px; }
@media (prefers-reduced-motion: reduce) { * { animation: none !important; transition: none !important; } }
</style>

<header class="masthead">
  <span class="rule-tag">KOC T&amp;CD</span>
  <h1>A.I. Digital Campus (Community) — Project Documentation</h1>
  <p>__COUNT__ documents · __WORDS__ words · everything the project ships with, in one place.</p>
</header>

<div class="shell">
  <nav class="index" aria-label="Documents">
    <input id="filter" type="search" placeholder="Filter documents…" aria-label="Filter documents" autocomplete="off">
    <div id="nav">__NAV__</div>
    <p class="empty" id="no-match" hidden>Nothing matches that.</p>
  </nav>
  <main>__ARTICLES__</main>
</div>

<script>
(function () {
  var links = Array.prototype.slice.call(document.querySelectorAll(".nav-link"));
  var docs = Array.prototype.slice.call(document.querySelectorAll(".doc"));

  // Tables carry wide comparison rows; each gets its own scroller so the page never slides sideways.
  document.querySelectorAll(".doc table").forEach(function (t) {
    var box = document.createElement("div");
    box.className = "table-scroll";
    t.parentNode.insertBefore(box, t);
    box.appendChild(t);
  });

  function show(id, push) {
    var found = docs.some(function (d) { return d.id === id; });
    if (!found) { id = docs[0].id; }
    docs.forEach(function (d) { d.hidden = d.id !== id; });
    links.forEach(function (a) {
      var on = a.dataset.doc === id;
      if (on) { a.setAttribute("aria-current", "true"); } else { a.removeAttribute("aria-current"); }
    });
    if (push && location.hash !== "#" + id) { history.replaceState(null, "", "#" + id); }
    window.scrollTo(0, 0);
  }

  links.forEach(function (a) {
    a.addEventListener("click", function (e) { e.preventDefault(); show(a.dataset.doc, true); });
  });
  window.addEventListener("hashchange", function () { show(location.hash.slice(1), false); });

  var filter = document.getElementById("filter");
  var noMatch = document.getElementById("no-match");
  filter.addEventListener("input", function () {
    var q = filter.value.trim().toLowerCase();
    var hits = 0;
    links.forEach(function (a) {
      var hit = !q || a.textContent.toLowerCase().indexOf(q) !== -1;
      a.parentNode.hidden = !hit;
      if (hit) { hits++; }
    });
    document.querySelectorAll(".nav-group").forEach(function (g) {
      g.hidden = !g.querySelector("li:not([hidden])");
    });
    noMatch.hidden = hits > 0;
  });

  show(location.hash.slice(1) || docs[0].id, false);
})();
</script>
"""

out = (TEMPLATE
       .replace("__NAV__", "".join(nav_html))
       .replace("__ARTICLES__", "".join(articles))
       .replace("__COUNT__", str(len(docs)))
       .replace("__WORDS__", f"{sum(d[4] for d in docs):,}"))

dest = os.path.join(ROOT, "docs", "project-documentation.html")
with open(dest, "w", encoding="utf-8") as fh:
    fh.write(out)
print("  wrote", dest, f"({len(out):,} bytes)")
