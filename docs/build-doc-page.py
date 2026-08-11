"""Renders single markdown documents as standalone, KOC-themed HTML pages.

Run from anywhere:  python docs/build-doc-page.py
Then publish each result to the URL recorded in docs/PUBLISHED_ARTIFACTS.md.

Add a row to PAGES to give another document a page. The theme is the product's own
(src/Beep.KocAiCommunity.Ui.Shared/wwwroot/css/koc-blueprint.css, Branding/KocBrand.cs) — every page
published for this initiative should look like the thing it describes.
"""
import html
import os

import markdown

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (source markdown, output html, eyebrow, blurb under the title)
PAGES = [
    ("docs/SECURITY_DESKTOP.md", "docs/security-desktop.html",
     "KOC Studio · Information security",
     "Why a workstation reading the platform database directly is a different trust model from the "
     "website, and which SQL identity KOC Studio should be given."),
    ("docs/DEPLOYMENT_DESKTOP.md", "docs/deployment-desktop.html",
     "KOC Studio · Deployment",
     "Prerequisites, offline versus connected builds, distribution, upgrades, and how to verify an "
     "install of the desktop app."),
]

TEMPLATE = """<title>{title}</title>

<style>
  /* The KOC blueprint theme, taken from the product: koc-blueprint.css and Branding/KocBrand.cs. */
  :root {{
    --ink:#10222F; --ink-soft:#5A6B78; --ink-faint:#8496A3;
    --paper:#F6F8FA; --surface:#FFFFFF; --rule:#E1E7EC; --rule-strong:#C6D2DB;
    --accent:#1466A5; --accent-mid:#2A7CBE; --accent-soft:#5FA3D4; --accent-wash:#EAF2F8;
    --ground-deep:#0B2E4C; --teal:#1F8A8C; --teal-wash:#E9F4F4;
    --risk:#9B2C2C; --risk-wash:#FBEAEA; --grid:rgba(20,102,165,.055);
    --mono: ui-monospace,"SF Mono","Cascadia Mono",Menlo,Consolas,monospace;
    --sans: "Inter","IBM Plex Sans","Segoe UI",-apple-system,BlinkMacSystemFont,Roboto,Helvetica,Arial,sans-serif;
    --measure: 72ch;
  }}
  @media (prefers-color-scheme: dark) {{
    :root {{
      --ink:#DCE6EE; --ink-soft:#93A5B4; --ink-faint:#7A8C9B;
      --paper:#0B1620; --surface:#12222E; --rule:#23384A; --rule-strong:#33506A;
      --accent:#5FA3D4; --accent-mid:#2A7CBE; --accent-soft:#8FC4E6; --accent-wash:#12283A;
      --ground-deep:#07202F; --teal:#4FB3B5; --teal-wash:#122A2B;
      --risk:#E88A8A; --risk-wash:#2C1618; --grid:rgba(95,163,212,.05);
    }}
  }}
  :root[data-theme="dark"] {{
    --ink:#DCE6EE; --ink-soft:#93A5B4; --ink-faint:#7A8C9B;
    --paper:#0B1620; --surface:#12222E; --rule:#23384A; --rule-strong:#33506A;
    --accent:#5FA3D4; --accent-mid:#2A7CBE; --accent-soft:#8FC4E6; --accent-wash:#12283A;
    --ground-deep:#07202F; --teal:#4FB3B5; --teal-wash:#122A2B;
    --risk:#E88A8A; --risk-wash:#2C1618; --grid:rgba(95,163,212,.05);
  }}
  :root[data-theme="light"] {{
    --ink:#10222F; --ink-soft:#5A6B78; --ink-faint:#8496A3;
    --paper:#F6F8FA; --surface:#FFFFFF; --rule:#E1E7EC; --rule-strong:#C6D2DB;
    --accent:#1466A5; --accent-mid:#2A7CBE; --accent-soft:#5FA3D4; --accent-wash:#EAF2F8;
    --ground-deep:#0B2E4C; --teal:#1F8A8C; --teal-wash:#E9F4F4;
    --risk:#9B2C2C; --risk-wash:#FBEAEA; --grid:rgba(20,102,165,.055);
  }}

  *{{box-sizing:border-box;}}
  body{{margin:0;background:var(--paper);color:var(--ink);font-family:var(--sans);font-size:16px;line-height:1.68;-webkit-font-smoothing:antialiased;}}
  .wrap{{max-width:920px;margin:0 auto;padding:0 24px 96px;}}

  /* The blueprint is drawn on white paper, not printed white on navy: surface ground, a hairline
     accent rule, grid-paper behind it. A dark banner belongs to a different design system. */
  .masthead{{background:var(--surface);color:var(--ink);padding:40px 0 32px;margin-bottom:32px;
    border-block-start:3px solid var(--accent);border-block-end:1px solid var(--rule);
    background-image:linear-gradient(var(--grid) 1px,transparent 1px),linear-gradient(90deg,var(--grid) 1px,transparent 1px);
    background-size:28px 28px;}}
  .masthead .wrap{{padding-bottom:0;}}
  .eyebrow{{font-size:.75rem;font-weight:600;letter-spacing:.1em;text-transform:uppercase;color:var(--accent);}}
  .masthead h1{{color:var(--ink);font-size:clamp(1.6rem,3.6vw,2.3rem);font-weight:700;letter-spacing:-.02em;line-height:1.2;text-wrap:balance;margin:10px 0 12px;}}
  .masthead p{{color:var(--ink-soft);max-width:68ch;margin:0;font-size:1.02rem;}}

  .doc{{position:relative;background:var(--surface);border:1px solid var(--rule);padding:clamp(22px,4vw,44px);}}
  .doc::before,.doc::after{{content:"";position:absolute;width:14px;height:14px;border:2px solid var(--accent);pointer-events:none;}}
  .doc::before{{inset-block-start:-1px;inset-inline-start:-1px;border-inline-end:0;border-block-end:0;}}
  .doc::after{{inset-block-end:-1px;inset-inline-end:-1px;border-inline-start:0;border-block-start:0;}}

  h1,h2,h3,h4{{line-height:1.25;text-wrap:balance;}}
  .doc>h1:first-child{{margin-top:0;font-size:1.65rem;font-weight:700;letter-spacing:-.015em;}}
  h2{{font-size:1.08rem;font-weight:600;letter-spacing:.01em;text-transform:uppercase;margin:2.1em 0 .6em;padding-bottom:7px;border-bottom:1px solid var(--rule-strong);}}
  h3{{font-size:1rem;font-weight:650;margin:1.7em 0 .4em;}}
  p,li{{overflow-wrap:break-word;}} p{{max-width:var(--measure);}}
  a{{color:var(--accent);text-underline-offset:2px;}}
  strong{{font-weight:650;}}
  code{{font-family:var(--mono);font-size:.86em;background:var(--accent-wash);color:var(--accent);border:1px solid var(--rule);border-radius:3px;padding:.1em .35em;word-break:break-word;}}
  pre{{background:var(--paper);color:var(--ink);border:1px solid var(--rule);border-inline-start:2px solid var(--teal);padding:14px 16px;border-radius:3px;overflow-x:auto;margin:1.3em 0;}}
  pre code{{background:none;border:0;padding:0;color:inherit;font-size:.82rem;line-height:1.55;}}
  blockquote{{margin:1.4em 0;padding:14px 18px;border:1px solid var(--rule);border-inline-start:4px solid var(--accent);background:var(--accent-wash);max-width:var(--measure);}}
  blockquote p:first-child{{margin-top:0;}} blockquote p:last-child{{margin-bottom:0;}}
  .table-scroll{{overflow-x:auto;margin:1.4em 0;border:1px solid var(--rule);border-radius:6px;}}
  table{{border-collapse:collapse;width:100%;font-size:.88rem;}}
  th,td{{text-align:start;padding:9px 13px;border-bottom:1px solid var(--rule);vertical-align:top;}}
  th{{background:var(--accent-wash);color:var(--accent);font-size:.69rem;font-weight:700;letter-spacing:.07em;text-transform:uppercase;}}
  tbody tr:last-child td{{border-bottom:none;}}
  ul,ol{{max-width:var(--measure);padding-inline-start:1.35rem;}} li{{margin:6px 0;}} li::marker{{color:var(--teal);}}
  hr{{border:0;border-top:1px solid var(--rule);margin:2.2em 0;}}
  input[type=checkbox]{{accent-color:var(--accent);margin-inline-end:.4em;}}
  .foot{{margin-top:28px;font-size:.83rem;color:var(--ink-faint);max-width:var(--measure);}}
  @media print{{
    .masthead{{background:none;border-block-start-width:2px;}}
    .doc{{border:0;padding:0;}} .doc::before,.doc::after{{display:none;}}
  }}
  @media (prefers-reduced-motion: reduce){{*{{animation:none!important;transition:none!important;}}}}
</style>

<header class="masthead">
  <div class="wrap">
    <div class="eyebrow">{eyebrow}</div>
    <h1>{heading}</h1>
    <p>{blurb}</p>
  </div>
</header>

<div class="wrap">
  <article class="doc" dir="auto">{body}</article>
  <p class="foot">Source: <code>{source}</code> in the Beep.KocAiCommunity repository. Edit that file and
  rebuild with <code>python docs/build-doc-page.py</code> — this page is generated.</p>
</div>

<script>
document.querySelectorAll(".doc table").forEach(function (t) {{
  var box = document.createElement("div");
  box.className = "table-scroll";
  t.parentNode.insertBefore(box, t);
  box.appendChild(t);
}});
</script>
"""

MD = markdown.Markdown(extensions=["tables", "fenced_code", "sane_lists", "attr_list"])

for source, out, eyebrow, blurb in PAGES:
    with open(os.path.join(ROOT, source), encoding="utf-8") as fh:
        raw = fh.read()
    MD.reset()
    body = MD.convert(raw)

    # The first heading becomes the masthead title, so it is not repeated inside the card.
    heading = raw.splitlines()[0].lstrip("# ").strip()
    if body.startswith("<h1"):
        body = body[body.index("</h1>") + 5:]

    page = TEMPLATE.format(
        title=html.escape(heading), heading=html.escape(heading),
        eyebrow=html.escape(eyebrow), blurb=html.escape(blurb),
        body=body, source=html.escape(source))

    dest = os.path.join(ROOT, out)
    with open(dest, "w", encoding="utf-8") as fh:
        fh.write(page)
    print(f"  {source} -> {out} ({len(page):,} bytes)")
