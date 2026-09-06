using AutoMapper;
using AutoMapper.QueryableExtensions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Queries;

public sealed record GetAllSuppliersQuery : IRequest<IReadOnlyList<SupplierResponse>>;

public sealed class GetAllSuppliersQueryHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<GetAllSuppliersQuery, IReadOnlyList<SupplierResponse>>
{
    public async Task<IReadOnlyList<SupplierResponse>> Handle(
        GetAllSuppliersQuery request,
        CancellationToken cancellationToken
    ) =>
        await northwindContext
            .Suppliers.AsNoTracking()
            .OrderBy(supplier => supplier.SupplierId)
            .ProjectTo<SupplierResponse>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
}

public sealed record GetSupplierByIdQuery(int SupplierId) : IRequest<SupplierResponse?>;

public sealed class GetSupplierByIdQueryValidator : AbstractValidator<GetSupplierByIdQuery>
{
    public GetSupplierByIdQueryValidator()
    {
        RuleFor(query => query.SupplierId).GreaterThan(0);
    }
}

public sealed class GetSupplierByIdQueryHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<GetSupplierByIdQuery, SupplierResponse?>
{
    public async Task<SupplierResponse?> Handle(
        GetSupplierByIdQuery request,
        CancellationToken cancellationToken
    ) =>
        await northwindContext
            .Suppliers.AsNoTracking()
            .Where(supplier => supplier.SupplierId == request.SupplierId)
            .ProjectTo<SupplierResponse>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(cancellationToken);
}