namespace FinanceCore.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<FinanceCoreApiFactory>
{
    public const string Name = "api";
}
