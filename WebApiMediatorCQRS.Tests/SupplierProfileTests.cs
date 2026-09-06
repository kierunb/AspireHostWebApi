using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;
using WebApiMediatorCQRS.Profiles;

namespace WebApiMediatorCQRS.Tests.Tests;

public class SupplierProfileTests
{
    private readonly MapperConfiguration _configuration;
    private readonly IMapper _mapper;

    public SupplierProfileTests()
    {
        _configuration = new MapperConfiguration(
            configuration => configuration.AddProfile<SupplierProfile>(),
            NullLoggerFactory.Instance
        );
        _mapper = _configuration.CreateMapper();
    }

    [Fact]
    public void GivenSupplierProfile_WhenConfigurationValidated_ExpectedValidConfiguration()
    {
        _configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void GivenSupplierResponseContract_WhenPropertiesInspected_ExpectedNoProductsNavigation()
    {
        var responseProperties = typeof(SupplierResponse).GetProperties();

        Assert.DoesNotContain(
            responseProperties,
            property => property.Name == nameof(Suppliers.Products)
        );
    }

    [Fact]
    public void GivenFullyPopulatedSupplier_WhenMapped_ExpectedEveryScalarValue()
    {
        var supplier = new Suppliers
        {
            SupplierId = 42,
            CompanyName = "Alpine Foods",
            ContactName = "Ada Nowak",
            ContactTitle = "Purchasing Manager",
            Address = "1 Market Street",
            City = "Warsaw",
            Region = "Mazowieckie",
            PostalCode = "00-001",
            Country = "Poland",
            Phone = "+48 22 123 45 67",
            Fax = "+48 22 765 43 21",
            HomePage = "https://alpine.example",
        };

        var response = _mapper.Map<SupplierResponse>(supplier);

        Assert.Equal(supplier.SupplierId, response.SupplierId);
        Assert.Equal(supplier.CompanyName, response.CompanyName);
        Assert.Equal(supplier.ContactName, response.ContactName);
        Assert.Equal(supplier.ContactTitle, response.ContactTitle);
        Assert.Equal(supplier.Address, response.Address);
        Assert.Equal(supplier.City, response.City);
        Assert.Equal(supplier.Region, response.Region);
        Assert.Equal(supplier.PostalCode, response.PostalCode);
        Assert.Equal(supplier.Country, response.Country);
        Assert.Equal(supplier.Phone, response.Phone);
        Assert.Equal(supplier.Fax, response.Fax);
        Assert.Equal(supplier.HomePage, response.HomePage);
    }
}