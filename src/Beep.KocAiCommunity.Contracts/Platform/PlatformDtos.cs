namespace Beep.KocAiCommunity.Contracts.Platform;

/// <summary>
/// Lightweight, non-sensitive platform facts any client can read anonymously at startup —
/// used to decide whether to surface the demonstration-environment notice.
/// </summary>
/// <param name="DemoMode">
/// True when the app runs with development authentication (no Entra tenant and no intranet
/// Windows SSO configured) — i.e. a demo/evaluation build rather than a production deployment.
/// </param>
/// <param name="DemoDataSeeded">
/// True when demonstration content (namespaced <c>demo-*</c> colleagues, competitions,
/// discussions, and datasets) is currently present in the database.
/// </param>
public sealed record PlatformMetaDto(bool DemoMode, bool DemoDataSeeded);
