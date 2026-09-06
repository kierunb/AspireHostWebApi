namespace WebApiMediatorCQRS.ApiModels;

/// <summary>Represents a supplier returned by the API.</summary>
public sealed record SupplierResponse(
    int SupplierId,
    string CompanyName,
    string? ContactName,
    string? ContactTitle,
    string? Address,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Fax,
    string? HomePage
);

/// <summary>Payload for creating a supplier.</summary>
public sealed record CreateSupplierRequest(
    string CompanyName,
    string? ContactName,
    string? ContactTitle,
    string? Address,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Fax,
    string? HomePage
);

/// <summary>Payload for replacing a supplier.</summary>
public sealed record UpdateSupplierRequest(
    string CompanyName,
    string? ContactName,
    string? ContactTitle,
    string? Address,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Fax,
    string? HomePage
);