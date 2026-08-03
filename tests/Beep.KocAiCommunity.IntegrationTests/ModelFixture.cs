using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Studio;
using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// A real trained model for the tests that need one — the registry, promotion, deployment and
/// inference.
/// <para>
/// Those features are kept: registering what the desktop trained, and serving predictions from a model
/// already registered, are not training. Their tests used to get a model by posting to
/// <c>/studio/train</c>, and that route is gone.
/// </para>
/// <para>
/// So the fixture calls <see cref="IStudioService"/> in-process instead. That is not a way back to
/// server-side training: the service is what the desktop uses, and nothing a client can reach exposes
/// it. What the tests need is a model to register, not a route that makes one.
/// </para>
/// </summary>
public static class ModelFixture
{
    /// <summary>Balanced and cleanly separable, so a short AutoML budget still lands a usable model.</summary>
    public static string SeparableCsv(int pairs = 60)
    {
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < pairs; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        return sb.ToString();
    }

    /// <summary>Trains and records a run owned by <paramref name="userId"/>, returning its id.</summary>
    public static async Task<Guid> TrainAsync(KocApiFactory factory, string userId, string datasetName)
    {
        using var scope = factory.Services.CreateScope();
        var studio = scope.ServiceProvider.GetRequiredService<IStudioService>();

        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(SeparableCsv()));

        // Longer than the production default: this is a fixture several assertions hang off, and a
        // budget that lands no trial at all under a loaded CI box fails as "training failed" rather
        // than as anything the test is about.
        var run = await studio.TrainAsync(userId, datasetName, "label", MlTaskType.BinaryClassification, csv, maxSeconds: 25);
        return run.Id;
    }
}
