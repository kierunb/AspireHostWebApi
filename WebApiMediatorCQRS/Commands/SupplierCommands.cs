using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Commands;

public enum SupplierMutationStatus
{
    Success,
    NotFound,
    Conflict,
}

public sealed record SupplierMutationResult(
    SupplierMutationStatus Status,
    SupplierResponse? Supplier = null
);

public sealed record CreateSupplierCommand(
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
) : IRequest<SupplierMutationResult>;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(command => command.CompanyName).NotEmpty().MaximumLength(40);
        RuleFor(command => command.ContactName).MaximumLength(30);
        RuleFor(command => command.ContactTitle).MaximumLength(30);
        RuleFor(command => command.Address).MaximumLength(60);
        RuleFor(command => command.City).MaximumLength(15);
        RuleFor(command => command.Region).MaximumLength(15);
        RuleFor(command => command.PostalCode).MaximumLength(10);
        RuleFor(command => command.Country).MaximumLength(15);
        RuleFor(command => command.Phone).MaximumLength(24);
        RuleFor(command => command.Fax).MaximumLength(24);
    }
}

public sealed class CreateSupplierCommandHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<CreateSupplierCommand, SupplierMutationResult>
{
    public async Task<SupplierMutationResult> Handle(
        CreateSupplierCommand request,
        CancellationToken cancellationToken
    )
    {
        var supplier = new Suppliers
        {
            CompanyName = request.CompanyName,
            ContactName = request.ContactName,
            ContactTitle = request.ContactTitle,
            Address = request.Address,
            City = request.City,
            Region = request.Region,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Phone = request.Phone,
            Fax = request.Fax,
            HomePage = request.HomePage,
        };

        northwindContext.Suppliers.Add(supplier);
        await northwindContext.SaveChangesAsync(cancellationToken);

        return new SupplierMutationResult(
            SupplierMutationStatus.Success,
            mapper.Map<SupplierResponse>(supplier)
        );
    }
}

public sealed record UpdateSupplierCommand(
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
) : IRequest<SupplierMutationResult>;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(command => command.SupplierId).GreaterThan(0);
        RuleFor(command => command.CompanyName).NotEmpty().MaximumLength(40);
        RuleFor(command => command.ContactName).MaximumLength(30);
        RuleFor(command => command.ContactTitle).MaximumLength(30);
        RuleFor(command => command.Address).MaximumLength(60);
        RuleFor(command => command.City).MaximumLength(15);
        RuleFor(command => command.Region).MaximumLength(15);
        RuleFor(command => command.PostalCode).MaximumLength(10);
        RuleFor(command => command.Country).MaximumLength(15);
        RuleFor(command => command.Phone).MaximumLength(24);
        RuleFor(command => command.Fax).MaximumLength(24);
    }
}

public sealed class UpdateSupplierCommandHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<UpdateSupplierCommand, SupplierMutationResult>
{
    public async Task<SupplierMutationResult> Handle(
        UpdateSupplierCommand request,
        CancellationToken cancellationToken
    )
    {
        var supplier = await northwindContext.Suppliers.FindAsync(
            [request.SupplierId],
            cancellationToken
        );
        if (supplier is null)
            return new SupplierMutationResult(SupplierMutationStatus.NotFound);

        supplier.CompanyName = request.CompanyName;
        supplier.ContactName = request.ContactName;
        supplier.ContactTitle = request.ContactTitle;
        supplier.Address = request.Address;
        supplier.City = request.City;
        supplier.Region = request.Region;
        supplier.PostalCode = request.PostalCode;
        supplier.Country = request.Country;
        supplier.Phone = request.Phone;
        supplier.Fax = request.Fax;
        supplier.HomePage = request.HomePage;

        await northwindContext.SaveChangesAsync(cancellationToken);

        return new SupplierMutationResult(
            SupplierMutationStatus.Success,
            mapper.Map<SupplierResponse>(supplier)
        );
    }
}

public sealed record DeleteSupplierCommand(int SupplierId) : IRequest<SupplierMutationStatus>;

public sealed class DeleteSupplierCommandValidator : AbstractValidator<DeleteSupplierCommand>
{
    public DeleteSupplierCommandValidator()
    {
        RuleFor(command => command.SupplierId).GreaterThan(0);
    }
}

public sealed class DeleteSupplierCommandHandler(NorthwindContext northwindContext)
    : IRequestHandler<DeleteSupplierCommand, SupplierMutationStatus>
{
    public async Task<SupplierMutationStatus> Handle(
        DeleteSupplierCommand request,
        CancellationToken cancellationToken
    )
    {
        var supplier = await northwindContext.Suppliers.FindAsync(
            [request.SupplierId],
            cancellationToken
        );
        if (supplier is null)
            return SupplierMutationStatus.NotFound;

        var hasProducts = await northwindContext.Products.AnyAsync(
            product => product.SupplierId == request.SupplierId,
            cancellationToken
        );
        if (hasProducts)
            return SupplierMutationStatus.Conflict;

        northwindContext.Suppliers.Remove(supplier);

        try
        {
            await northwindContext.SaveChangesAsync(cancellationToken);
            return SupplierMutationStatus.Success;
        }
        catch (DbUpdateException)
        {
            return SupplierMutationStatus.Conflict;
        }
    }
}