using BizScraper.Api.Features.ExecuteScript.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Validation;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class ExecuteScriptValidatorTests
{
    private readonly ExecuteScriptValidator _validator = new();

    [Fact]
    public void Validate_AllRequiredParamsPresent_DoesNotThrow()
    {
        var definition = CreateDefinition("searchTerm");
        var parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" };

        var exception = Record.Exception(() => _validator.Validate("co-business-search", parameters, definition));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingRequiredParam_ThrowsMissingParametersException()
    {
        var definition = CreateDefinition("searchTerm");
        var parameters = new Dictionary<string, object?>();

        var ex = Assert.Throws<MissingParametersException>(
            () => _validator.Validate("co-business-search", parameters, definition));

        Assert.Contains("searchTerm", ex.MissingParameters);
    }

    [Fact]
    public void Validate_NullParameters_ThrowsMissingParametersException()
    {
        var definition = CreateDefinition("searchTerm");

        Assert.Throws<MissingParametersException>(
            () => _validator.Validate("co-business-search", null, definition));
    }

    [Fact]
    public void Validate_WhitespaceParamValue_ThrowsMissingParametersException()
    {
        var definition = CreateDefinition("searchTerm");
        var parameters = new Dictionary<string, object?> { ["searchTerm"] = "   " };

        Assert.Throws<MissingParametersException>(
            () => _validator.Validate("co-business-search", parameters, definition));
    }

    [Fact]
    public void Validate_ExtraParamsIgnored_DoesNotThrow()
    {
        var definition = CreateDefinition("searchTerm");
        var parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme", ["extraParam"] = "ignored" };

        var exception = Record.Exception(() => _validator.Validate("co-business-search", parameters, definition));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_CorrelationIdNotRequired_EvenWhenInDefinition()
    {
        var variables = new List<VariableV2>
        {
            new() { Name = "correlationId", Source = "runtime", Required = true },
            new() { Name = "searchTerm", Source = "runtime", Required = true }
        };
        var definition = CreateDefinitionWithVariables(variables);
        var parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" };

        var exception = Record.Exception(() => _validator.Validate("test", parameters, definition));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ConfigSourceVariablesNotRequired()
    {
        var variables = new List<VariableV2>
        {
            new() { Name = "searchTerm", Source = "runtime", Required = true },
            new() { Name = "maxPages", Source = "config", Required = true }
        };
        var definition = CreateDefinitionWithVariables(variables);
        var parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" };

        var exception = Record.Exception(() => _validator.Validate("test", parameters, definition));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MultipleMissingParams_ListsAll()
    {
        var variables = new List<VariableV2>
        {
            new() { Name = "searchTerm", Source = "runtime", Required = true },
            new() { Name = "state", Source = "runtime", Required = true }
        };
        var definition = CreateDefinitionWithVariables(variables);
        var parameters = new Dictionary<string, object?>();

        var ex = Assert.Throws<MissingParametersException>(
            () => _validator.Validate("test", parameters, definition));

        Assert.Equal(2, ex.MissingParameters.Count);
        Assert.Contains("searchTerm", ex.MissingParameters);
        Assert.Contains("state", ex.MissingParameters);
    }

    [Fact]
    public void Validate_IaSearch_RequiresSearchTerm()
    {
        var definition = CreateDefinition("searchTerm");
        var parameters = new Dictionary<string, object?>();

        var ex = Assert.Throws<MissingParametersException>(
            () => _validator.Validate("us-ia-business-search", parameters, definition));

        Assert.Contains("searchTerm", ex.MissingParameters);
    }

    [Fact]
    public void Validate_IaDetails_RequiresDetailsUrlAfterDecode()
    {
        var definition = CreateDefinition("detailsUrl");
        var parameters = new Dictionary<string, object?> { ["detailsUrl"] = "https://sos.iowa.gov/search/business/Entity.aspx?BusinessNumber=123" };

        var exception = Record.Exception(() => _validator.Validate("us-ia-entity-details", parameters, definition));

        Assert.Null(exception);
    }

    private static FlowDefinitionV2 CreateDefinition(string runtimeParamName) =>
        CreateDefinitionWithVariables([new VariableV2 { Name = runtimeParamName, Source = "runtime", Required = true }]);

    private static FlowDefinitionV2 CreateDefinitionWithVariables(IReadOnlyList<VariableV2> variables) =>
        new(
            SchemaVersion: "2.0",
            Metadata: new FlowMetadata { Id = "test", Name = "Test", State = "CO", Endpoint = "search" },
            Environment: null,
            Browser: null,
            Variables: variables,
            Actions: [],
            Scripts: null,
            Output: new OutputDeclarationV2 { VariableName = "results", Type = "array" });
}
