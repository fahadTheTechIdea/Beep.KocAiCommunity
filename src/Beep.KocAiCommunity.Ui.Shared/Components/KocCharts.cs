using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace Beep.KocAiCommunity.Ui.Shared.Components;

/// <summary>Dependency-free inline SVG/CSS charts (donut + horizontal bars) shared across pages.</summary>
public static class KocCharts
{
    public sealed record Seg(string Label, double Value, string Color);

    public static MarkupString Donut(Seg[] segments, string center, string caption)
    {
        var ci = CultureInfo.InvariantCulture;
        var total = segments.Sum(s => s.Value);
        if (total <= 0)
        {
            total = 1;
        }

        var sb = new StringBuilder();
        sb.Append("<div style=\"text-align:center;\">");
        sb.Append("<svg viewBox=\"0 0 42 42\" width=\"170\" height=\"170\" role=\"img\">");
        sb.Append("<circle cx=\"21\" cy=\"21\" r=\"15.915\" fill=\"none\" stroke=\"#eef2f6\" stroke-width=\"5\"></circle>");
        double cumulative = 0;
        foreach (var s in segments)
        {
            var pct = s.Value / total * 100;
            sb.Append($"<circle cx=\"21\" cy=\"21\" r=\"15.915\" fill=\"none\" stroke=\"{s.Color}\" stroke-width=\"5\" stroke-dasharray=\"{pct.ToString("0.###", ci)} {(100 - pct).ToString("0.###", ci)}\" stroke-dashoffset=\"{(25 - cumulative).ToString("0.###", ci)}\"></circle>");
            cumulative += pct;
        }
        sb.Append($"<text x=\"21\" y=\"23\" text-anchor=\"middle\" font-size=\"7\" font-weight=\"700\" fill=\"#10222f\">{center}</text>");
        sb.Append("</svg><div style=\"margin-top:6px;font-size:12px;\">");
        foreach (var s in segments)
        {
            sb.Append($"<span style=\"display:inline-flex;align-items:center;gap:4px;margin:2px 6px;\"><span style=\"width:10px;height:10px;border-radius:2px;background:{s.Color};display:inline-block;\"></span>{Encode(s.Label)} {s.Value:0}</span>");
        }
        sb.Append($"</div><div style=\"font-size:12px;opacity:.65;\">{Encode(caption)}</div></div>");
        return new MarkupString(sb.ToString());
    }

    public static MarkupString Bar(string label, double value, double max, string color)
    {
        var ci = CultureInfo.InvariantCulture;
        var pct = (max <= 0 ? 0 : value / max * 100).ToString("0.#", ci);
        return new MarkupString(
            "<div style=\"display:flex;align-items:center;gap:8px;margin:3px 0;\">"
            + $"<div style=\"width:120px;font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;\">{Encode(label)}</div>"
            + $"<div style=\"flex:1;background:#eef2f6;border-radius:4px;height:16px;\"><div style=\"width:{pct}%;background:{color};height:16px;border-radius:4px;\"></div></div>"
            + $"<div style=\"width:54px;text-align:right;font-size:12px;\">{value.ToString("0.###", ci)}</div></div>");
    }

    private static string Encode(string s) => System.Net.WebUtility.HtmlEncode(s);
}
