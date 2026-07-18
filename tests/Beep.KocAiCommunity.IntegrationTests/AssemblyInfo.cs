using Xunit;

// Each test class boots its own API host over an in-memory database; run them sequentially so
// concurrent host startup and seeding don't contend.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
