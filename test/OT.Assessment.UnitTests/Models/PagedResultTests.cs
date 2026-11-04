using OT.Assessment.Core.Models;

namespace OT.Assessment.UnitTests.Models;

public class PagedResultTests
{
    [Theory]
    [InlineData(35, 10, 4)]
    [InlineData(30, 10, 3)]
    [InlineData(1, 10, 1)]
    [InlineData(0, 10, 0)]
    public void Create_CalculatesTotalPages(int total, int pageSize, int expectedTotalPages)
    {
        var data = new List<string>();

        var result = PagedResult<string>.Create(data, page: 1, pageSize, total);

        Assert.Equal(expectedTotalPages, result.TotalPages);
    }

    [Fact]
    public void Create_KeepsPagingValues()
    {
        var data = new List<string> { "first", "second" };

        var result = PagedResult<string>.Create(data, page: 2, pageSize: 10, total: 12);

        Assert.Equal(data, result.Data);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(12, result.Total);
    }
}
