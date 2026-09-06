using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Commands;

public enum ProductMutationStatus
{
    Success,
    NotFound,
    InvalidSupplier,
    InvalidCategory,
    Conflict,
}

public sealed record ProductMutationResult(
    ProductMutationStatus Status,
    ProductResponse? Product = null
);

public sealed record CreateProductCommand(
    string ProductName,
    int? SupplierId,
    int? CategoryId,
    string? QuantityPerUnit,
    decimal? UnitPrice,
    short? UnitsInStock,
    short? UnitsOnOrder,
    short? ReorderLevel,
    bool Discontinued
) : IRequest<ProductMutationResult>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.ProductName).NotEmpty().MaximumLength(40);
        RuleFor(command => command.SupplierId).GreaterThan(0).When(command => command.SupplierId.HasValue);
        RuleFor(command => command.CategoryId).GreaterThan(0).When(command => command.CategoryId.HasValue);
        RuleFor(command => command.QuantityPerUnit).MaximumLength(20);
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.UnitsInStock).GreaterThanOrEqualTo((short)0);
        RuleFor(command => command.UnitsOnOrder).GreaterThanOrEqualTo((short)0);
        RuleFor(command => command.ReorderLevel).GreaterThanOrEqualTo((short)0);
    }
}

public sealed class CreateProductCommandHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<CreateProductCommand, ProductMutationResult>
{
    public async Task<ProductMutationResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var referenceStatus = await ProductReferenceValidator.ValidateReferencesAsync(
            request.SupplierId,
            request.CategoryId,
            northwindContext,
            cancellationToken
        );
        if (referenceStatus is not ProductMutationStatus.Success)
            return new ProductMutationResult(referenceStatus);

        var product = new Products
        {
            ProductName = request.ProductName,
            SupplierId = request.SupplierId,
            CategoryId = request.CategoryId,
            QuantityPerUnit = request.QuantityPerUnit,
            UnitPrice = request.UnitPrice,
            UnitsInStock = request.UnitsInStock,
            UnitsOnOrder = request.UnitsOnOrder,
            ReorderLevel = request.ReorderLevel,
            Discontinued = request.Discontinued,
        };

        northwindContext.Products.Add(product);
        await northwindContext.SaveChangesAsync(cancellationToken);

        return new ProductMutationResult(
            ProductMutationStatus.Success,
            mapper.Map<ProductResponse>(product)
        );
    }
}

internal static class ProductReferenceValidator
{
    public static async Task<ProductMutationStatus> ValidateReferencesAsync(
        int? supplierId,
        int? categoryId,
        NorthwindContext northwindContext,
        CancellationToken cancellationToken
    )
    {
        if (
            supplierId.HasValue
            && !await northwindContext.Suppliers.AnyAsync(
                supplier => supplier.SupplierId == supplierId.Value,
                cancellationToken
            )
        )
            return ProductMutationStatus.InvalidSupplier;

        if (
            categoryId.HasValue
            && !await northwindContext.Categories.AnyAsync(
                category => category.CategoryId == categoryId.Value,
                cancellationToken
            )
        )
            return ProductMutationStatus.InvalidCategory;

        return ProductMutationStatus.Success;
    }
}

public sealed record UpdateProductCommand(
    int ProductId,
    string ProductName,
    int? SupplierId,
    int? CategoryId,
    string? QuantityPerUnit,
    decimal? UnitPrice,
    short? UnitsInStock,
    short? UnitsOnOrder,
    short? ReorderLevel,
    bool Discontinued
) : IRequest<ProductMutationResult>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.ProductId).GreaterThan(0);
        RuleFor(command => command.ProductName).NotEmpty().MaximumLength(40);
        RuleFor(command => command.SupplierId).GreaterThan(0).When(command => command.SupplierId.HasValue);
        RuleFor(command => command.CategoryId).GreaterThan(0).When(command => command.CategoryId.HasValue);
        RuleFor(command => command.QuantityPerUnit).MaximumLength(20);
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.UnitsInStock).GreaterThanOrEqualTo((short)0);
        RuleFor(command => command.UnitsOnOrder).GreaterThanOrEqualTo((short)0);
        RuleFor(command => command.ReorderLevel).GreaterThanOrEqualTo((short)0);
    }
}

public sealed class UpdateProductCommandHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<UpdateProductCommand, ProductMutationResult>
{
    public async Task<ProductMutationResult> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var product = await northwindContext.Products.FindAsync(
            [request.ProductId],
            cancellationToken
        );
        if (product is null)
            return new ProductMutationResult(ProductMutationStatus.NotFound);

        var referenceStatus = await ProductReferenceValidator.ValidateReferencesAsync(
            request.SupplierId,
            request.CategoryId,
            northwindContext,
            cancellationToken
        );
        if (referenceStatus is not ProductMutationStatus.Success)
            return new ProductMutationResult(referenceStatus);

        product.ProductName = request.ProductName;
        product.SupplierId = request.SupplierId;
        product.CategoryId = request.CategoryId;
        product.QuantityPerUnit = request.QuantityPerUnit;
        product.UnitPrice = request.UnitPrice;
        product.UnitsInStock = request.UnitsInStock;
        product.UnitsOnOrder = request.UnitsOnOrder;
        product.ReorderLevel = request.ReorderLevel;
        product.Discontinued = request.Discontinued;

        await northwindContext.SaveChangesAsync(cancellationToken);

        return new ProductMutationResult(
            ProductMutationStatus.Success,
            mapper.Map<ProductResponse>(product)
        );
    }
}

public sealed record DeleteProductCommand(int ProductId) : IRequest<ProductMutationStatus>;

public sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(command => command.ProductId).GreaterThan(0);
    }
}

public sealed class DeleteProductCommandHandler(NorthwindContext northwindContext)
    : IRequestHandler<DeleteProductCommand, ProductMutationStatus>
{
    public async Task<ProductMutationStatus> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var product = await northwindContext.Products.FindAsync(
            [request.ProductId],
            cancellationToken
        );
        if (product is null)
            return ProductMutationStatus.NotFound;

        var isUsedByOrder = await northwindContext.OrderDetails.AnyAsync(
            orderDetail => orderDetail.ProductId == request.ProductId,
            cancellationToken
        );
        if (isUsedByOrder)
            return ProductMutationStatus.Conflict;

        northwindContext.Products.Remove(product);

        try
        {
            await northwindContext.SaveChangesAsync(cancellationToken);
            return ProductMutationStatus.Success;
        }
        catch (DbUpdateException)
        {
            return ProductMutationStatus.Conflict;
        }
    }
}