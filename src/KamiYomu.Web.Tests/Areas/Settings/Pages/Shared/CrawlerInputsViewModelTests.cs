using KamiYomu.Web.Areas.Settings.Pages.Shared;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Shared;

public class CrawlerInputsViewModelTests
{
    [Fact]
    public void GetAgentMetadataValues_ParsesTrueAndFalseStringsAsBool()
    {
        CrawlerInputsViewModel viewModel = new()
        {
            AgentMetadata = new Dictionary<string, string?>
            {
                ["enabled"] = "true",
                ["disabled"] = "false"
            }
        };

        Dictionary<string, object> result = viewModel.GetAgentMetadataValues();

        Assert.Equal(true, result["enabled"]);
        Assert.Equal(false, result["disabled"]);
    }

    [Fact]
    public void GetAgentMetadataValues_KeepsNonBooleanValuesAsString()
    {
        CrawlerInputsViewModel viewModel = new()
        {
            AgentMetadata = new Dictionary<string, string?>
            {
                ["name"] = "some-value"
            }
        };

        Dictionary<string, object> result = viewModel.GetAgentMetadataValues();

        Assert.Equal("some-value", result["name"]);
    }

    [Fact]
    public void GetAgentMetadataValues_HandlesNullValue()
    {
        CrawlerInputsViewModel viewModel = new()
        {
            AgentMetadata = new Dictionary<string, string?>
            {
                ["name"] = null
            }
        };

        Dictionary<string, object> result = viewModel.GetAgentMetadataValues();

        Assert.Null(result["name"]);
    }

    [Fact]
    public void StaticGetAgentMetadataValues_ConvertsValuesToStrings()
    {
        Dictionary<string, object> values = new()
        {
            ["enabled"] = true,
            ["count"] = 5,
            ["name"] = "some-value"
        };

        Dictionary<string, string?> result = CrawlerInputsViewModel.GetAgentMetadataValues(values);

        Assert.Equal("True", result["enabled"]);
        Assert.Equal("5", result["count"]);
        Assert.Equal("some-value", result["name"]);
    }

    [Fact]
    public void StaticGetAgentMetadataValues_ConvertsNullToNull()
    {
        Dictionary<string, object> values = new()
        {
            ["name"] = null!
        };

        Dictionary<string, string?> result = CrawlerInputsViewModel.GetAgentMetadataValues(values);

        Assert.Null(result["name"]);
    }

    [Fact]
    public void RoundTrip_InstanceThenStatic_PreservesBooleanStringRepresentation()
    {
        CrawlerInputsViewModel viewModel = new()
        {
            AgentMetadata = new Dictionary<string, string?>
            {
                ["enabled"] = "true"
            }
        };

        Dictionary<string, object> objectValues = viewModel.GetAgentMetadataValues();
        Dictionary<string, string?> stringValues = CrawlerInputsViewModel.GetAgentMetadataValues(objectValues);

        // Note: bool.ToString() produces "True" (capitalized), not the original lowercase "true".
        Assert.Equal("True", stringValues["enabled"]);
    }
}
