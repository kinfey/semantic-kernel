﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Functions.OpenAPI.Builders.Query;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Xunit;

namespace SemanticKernel.Functions.UnitTests.OpenAPI.Builders;
public class QueryStringBuilderTests
{
    private readonly QueryStringBuilder _sut;

    public QueryStringBuilderTests()
    {
        this._sut = new QueryStringBuilder();
    }

    [Fact]
    public void ShouldAddQueryStringParametersAndUseValuesFromArguments()
    {
        // Arrange
        var firstParameterMetadata = new RestApiOperationParameter(
            "p1",
            "fake_type",
            true,
            RestApiOperationParameterLocation.Query);

        var secondParameterMetadata = new RestApiOperationParameter(
            "p2",
            "fake_type",
            true,
            RestApiOperationParameterLocation.Query);

        var operation = new RestApiOperation(
            "fake_id",
            new Uri("https://fake-random-test-host"),
            "/",
            HttpMethod.Get,
            "fake_description",
            new List<RestApiOperationParameter> { firstParameterMetadata, secondParameterMetadata },
            new Dictionary<string, string>());

        var _sut = new QueryStringBuilder();

        var arguments = new Dictionary<string, string>
        {
            { "p1", "v1" },
            { "p2", "v2" }
        };

        // Act
        var queryString = this._sut.Build(operation, arguments);

        // Assert
        Assert.Equal("p1=v1&p2=v2", queryString);
    }

    [Fact]
    public void ShouldAddQueryStringParametersAndUseTheirDefaultValues()
    {
        // Arrange
        var firstParameterMetadata = new RestApiOperationParameter(
            "p1",
            "fake_type",
            true,
            RestApiOperationParameterLocation.Query,
            defaultValue: "dv1");

        var secondParameterMetadata = new RestApiOperationParameter(
            "p2",
            "fake_type",
            true,
            RestApiOperationParameterLocation.Query,
            defaultValue: "dv2");

        var operation = new RestApiOperation(
            "fake_id",
            new Uri("https://fake-random-test-host"),
            "/",
            HttpMethod.Get,
            "fake_description",
            new List<RestApiOperationParameter> { firstParameterMetadata, secondParameterMetadata },
            new Dictionary<string, string>());

        var arguments = new Dictionary<string, string>();

        // Act
        var queryString = this._sut.Build(operation, arguments);

        // Assert
        Assert.Equal("p1=dv1&p2=dv2", queryString);
    }

    [Fact]
    public void ShouldSkipNotRequiredQueryStringParametersIfTheirValuesMissing()
    {
        // Arrange
        var firstParameterMetadata = new RestApiOperationParameter(
            "p1",
            "fake_type",
            false,
            RestApiOperationParameterLocation.Query);

        var secondParameterMetadata = new RestApiOperationParameter(
            "p2",
            "fake_type",
            false,
            RestApiOperationParameterLocation.Query);

        var operation = new RestApiOperation(
            "fake_id",
            new Uri("https://fake-random-test-host"),
            "/",
            HttpMethod.Get,
            "fake_description",
            new List<RestApiOperationParameter> { firstParameterMetadata, secondParameterMetadata },
            new Dictionary<string, string>());

        var arguments = new Dictionary<string, string>
        {
            { "p2", "v2" }
        };

        // Act
        var queryString = this._sut.Build(operation, arguments);

        // Assert
        Assert.Equal("p2=v2", queryString);
    }

    [Fact]
    public void ShouldThrowExceptionIfNoValueIsProvideForRequiredQueryStringParameter()
    {
        // Arrange
        var firstParameterMetadata = new RestApiOperationParameter(
            "p1",
            "fake_type",
            true,
            RestApiOperationParameterLocation.Query);

        var secondParameterMetadata = new RestApiOperationParameter(
            "p2",
            "fake_type",
            false,
            RestApiOperationParameterLocation.Query);

        var operation = new RestApiOperation(
            "fake_id",
            new Uri("https://fake-random-test-host"),
            "/",
            HttpMethod.Get,
            "fake_description",
            new List<RestApiOperationParameter> { firstParameterMetadata, secondParameterMetadata },
            new Dictionary<string, string>());

        var arguments = new Dictionary<string, string>
        {
            { "p2", "v2" }
        };

        //Act and assert
        Assert.Throws<SKException>(() => this._sut.Build(operation, arguments));
    }

    [Theory]
    [InlineData(":", "%3a")]
    [InlineData("/", "%2f")]
    [InlineData("?", "%3f")]
    [InlineData("#", "%23")]
    public void ItShouldEncodeSpecialSymbolsInQueryStringValues(string specialSymbol, string encodedEquivalent)
    {
        // Arrange
        var metadata = new List<RestApiOperationParameter>
        {
            new RestApiOperationParameter("fake_query_param", "string", false, RestApiOperationParameterLocation.Query, RestApiOperationParameterStyle.Simple)
        };

        var arguments = new Dictionary<string, string>
        {
            { "fake_query_param", $"fake_query_param_value{specialSymbol}" }
        };

        var operation = new RestApiOperation("fake_id", new Uri("https://fake-random-test-host"), "fake_path", HttpMethod.Get, "fake_description", metadata, new Dictionary<string, string>());

        // Act
        var queryString = this._sut.Build(operation, arguments);

        // Assert
        Assert.NotNull(queryString);

        Assert.EndsWith(encodedEquivalent, queryString, StringComparison.Ordinal);
    }

    [Fact]
    public void ItShouldNotWrapQueryStringValuesOfStringTypeIntoSingleQuotes()
    {
        // Arrange
        var metadata = new List<RestApiOperationParameter>
        {
            new RestApiOperationParameter("fake_query_string_param", "string", false, RestApiOperationParameterLocation.Query, RestApiOperationParameterStyle.Simple)
        };

        var arguments = new Dictionary<string, string>
        {
            { "fake_query_string_param", "fake_query_string_param_value" }
        };

        var operation = new RestApiOperation("fake_id", new Uri("https://fake-random-test-host"), "fake_path", HttpMethod.Get, "fake_description", metadata, new Dictionary<string, string>());

        // Act
        var queryString = this._sut.Build(operation, arguments);

        // Assert
        Assert.NotNull(queryString);

        var parameterValue = HttpUtility.ParseQueryString(queryString)["fake_query_string_param"];
        Assert.NotNull(parameterValue);
        Assert.Equal("fake_query_string_param_value", parameterValue); // Making sure that query string value of string type is not wrapped with quotes.
    }
}
