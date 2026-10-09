using Azure;
using Umbraco.Community.Search.Provider.AzureAI.Services;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchErrorsTests
{
    [TestCase(0, "could not be reached")]
    [TestCase(401, "must be an admin key")]
    [TestCase(403, "must be an admin key")]
    [TestCase(404, "the index does not exist yet")]
    [TestCase(429, "throttling")]
    [TestCase(500, "returned 500")]
    public void Describes_Failures_With_The_Setting_To_Check(int status, string expected)
        => Assert.That(AzureSearchErrors.Describe(new RequestFailedException(status, "boom")), Does.Contain(expected));

    [Test]
    public void Field_Changes_Suggest_A_Rebuild()
        => Assert.That(
            AzureSearchErrors.Describe(new RequestFailedException(400, "Existing field 'd_price' cannot be changed")),
            Does.Contain("Rebuild the index"));
}
