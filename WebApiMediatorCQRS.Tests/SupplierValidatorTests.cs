using FluentValidation.Results;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Tests.Tests;

public class SupplierValidatorTests
{
    private const string CreateCommand = nameof(CreateSupplierCommand);
    private const string UpdateCommand = nameof(UpdateSupplierCommand);

    [Theory]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.CompanyName), 40)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.ContactName), 30)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.ContactTitle), 30)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.Address), 60)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.City), 15)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.Region), 15)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.PostalCode), 10)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.Country), 15)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.Phone), 24)]
    [InlineData(CreateCommand, nameof(CreateSupplierCommand.Fax), 24)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.CompanyName), 40)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.ContactName), 30)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.ContactTitle), 30)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.Address), 60)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.City), 15)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.Region), 15)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.PostalCode), 10)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.Country), 15)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.Phone), 24)]
    [InlineData(UpdateCommand, nameof(UpdateSupplierCommand.Fax), 24)]
    public void GivenBoundedField_WhenValueIsAtAndAboveMaximum_ExpectedBoundaryResult(
        string commandType,
        string propertyName,
        int maximumLength
    )
    {
        var atMaximumResult = ValidateSupplierCommand(
            commandType,
            propertyName,
            new string('a', maximumLength)
        );
        var aboveMaximumResult = ValidateSupplierCommand(
            commandType,
            propertyName,
            new string('a', maximumLength + 1)
        );

        Assert.DoesNotContain(atMaximumResult.Errors, error => error.PropertyName == propertyName);
        Assert.Contains(aboveMaximumResult.Errors, error => error.PropertyName == propertyName);
    }

    [Theory]
    [InlineData(CreateCommand, null)]
    [InlineData(CreateCommand, "")]
    [InlineData(CreateCommand, " ")]
    [InlineData(UpdateCommand, null)]
    [InlineData(UpdateCommand, "")]
    [InlineData(UpdateCommand, " ")]
    public void GivenMissingOrBlankCompanyName_WhenValidated_ExpectedInvalidResult(
        string commandType,
        string? companyName
    )
    {
        var result = ValidateSupplierCommand(commandType, nameof(CreateSupplierCommand.CompanyName), companyName);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSupplierCommand.CompanyName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenNonpositiveSupplierId_WhenDeleteCommandValidated_ExpectedInvalidResult(int supplierId)
    {
        var result = new DeleteSupplierCommandValidator().Validate(new DeleteSupplierCommand(supplierId));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenNonpositiveSupplierId_WhenUpdateCommandValidated_ExpectedInvalidResult(int supplierId)
    {
        var result = new UpdateSupplierCommandValidator().Validate(CreateValidUpdateCommand() with
        {
            SupplierId = supplierId,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GivenNullOptionalFields_WhenCreateAndUpdateCommandsValidated_ExpectedValidResults()
    {
        var createResult = new CreateSupplierCommandValidator().Validate(CreateValidCreateCommand());
        var updateResult = new UpdateSupplierCommandValidator().Validate(CreateValidUpdateCommand());

        Assert.True(createResult.IsValid);
        Assert.True(updateResult.IsValid);
    }

    [Fact]
    public void GivenPositiveSupplierId_WhenDeleteCommandValidated_ExpectedValidResult()
    {
        var result = new DeleteSupplierCommandValidator().Validate(new DeleteSupplierCommand(1));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GivenPositiveSupplierId_WhenUpdateCommandValidated_ExpectedValidResult()
    {
        var result = new UpdateSupplierCommandValidator().Validate(CreateValidUpdateCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GivenUnrestrictedHomePage_WhenCreateAndUpdateCommandsValidated_ExpectedValidResults()
    {
        var unrestrictedHomePage = new string('h', 2_000);
        var createResult = new CreateSupplierCommandValidator().Validate(CreateValidCreateCommand() with
        {
            HomePage = unrestrictedHomePage,
        });
        var updateResult = new UpdateSupplierCommandValidator().Validate(CreateValidUpdateCommand() with
        {
            HomePage = unrestrictedHomePage,
        });

        Assert.True(createResult.IsValid);
        Assert.True(updateResult.IsValid);
    }

    private static CreateSupplierCommand CreateValidCreateCommand() =>
        new("Supplier", null, null, null, null, null, null, null, null, null, null);

    private static UpdateSupplierCommand CreateValidUpdateCommand() =>
        new(1, "Supplier", null, null, null, null, null, null, null, null, null, null);

    private static ValidationResult ValidateSupplierCommand(
        string commandType,
        string propertyName,
        string? value
    ) =>
        commandType switch
        {
            CreateCommand => new CreateSupplierCommandValidator().Validate(
                SetBoundedField(CreateValidCreateCommand(), propertyName, value)
            ),
            UpdateCommand => new UpdateSupplierCommandValidator().Validate(
                SetBoundedField(CreateValidUpdateCommand(), propertyName, value)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType), commandType, null),
        };

    private static CreateSupplierCommand SetBoundedField(
        CreateSupplierCommand command,
        string propertyName,
        string? value
    ) =>
        propertyName switch
        {
            nameof(CreateSupplierCommand.CompanyName) => command with { CompanyName = value! },
            nameof(CreateSupplierCommand.ContactName) => command with { ContactName = value },
            nameof(CreateSupplierCommand.ContactTitle) => command with { ContactTitle = value },
            nameof(CreateSupplierCommand.Address) => command with { Address = value },
            nameof(CreateSupplierCommand.City) => command with { City = value },
            nameof(CreateSupplierCommand.Region) => command with { Region = value },
            nameof(CreateSupplierCommand.PostalCode) => command with { PostalCode = value },
            nameof(CreateSupplierCommand.Country) => command with { Country = value },
            nameof(CreateSupplierCommand.Phone) => command with { Phone = value },
            nameof(CreateSupplierCommand.Fax) => command with { Fax = value },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null),
        };

    private static UpdateSupplierCommand SetBoundedField(
        UpdateSupplierCommand command,
        string propertyName,
        string? value
    ) =>
        propertyName switch
        {
            nameof(UpdateSupplierCommand.CompanyName) => command with { CompanyName = value! },
            nameof(UpdateSupplierCommand.ContactName) => command with { ContactName = value },
            nameof(UpdateSupplierCommand.ContactTitle) => command with { ContactTitle = value },
            nameof(UpdateSupplierCommand.Address) => command with { Address = value },
            nameof(UpdateSupplierCommand.City) => command with { City = value },
            nameof(UpdateSupplierCommand.Region) => command with { Region = value },
            nameof(UpdateSupplierCommand.PostalCode) => command with { PostalCode = value },
            nameof(UpdateSupplierCommand.Country) => command with { Country = value },
            nameof(UpdateSupplierCommand.Phone) => command with { Phone = value },
            nameof(UpdateSupplierCommand.Fax) => command with { Fax = value },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null),
        };
}