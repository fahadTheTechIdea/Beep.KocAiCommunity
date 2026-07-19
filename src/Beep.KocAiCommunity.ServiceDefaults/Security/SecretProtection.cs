using Beep.KocAiCommunity.Application.Admin;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary><see cref="ISecretProtector"/> backed by ASP.NET Data Protection.</summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("koc.settings.secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}

public static class SecretProtectionExtensions
{
    /// <summary>Registers ASP.NET Data Protection and the <see cref="ISecretProtector"/> used for secret settings.</summary>
    public static IServiceCollection AddKocSecretProtection(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        return services;
    }
}
