using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Tests;

public class StoreTests
{
    [Fact]
    public void Store_keeps_the_provided_address()
    {
        var store = new Store(
            "ST001",
            "Main Store",
            "King Faisal Road",
            "Manama");

        Assert.Equal("King Faisal Road", store.Address);
    }



}
