using AutoMapper;
using AutoMapper.QueryableExtensions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Queries;

public sealed record GetAllProductsQuery : IRequest<IReadOnlyList<ProductResponse>>;

public sealed class GetAllProductsQueryHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<GetAllProductsQuery, IReadOnlyList<ProductResponse>>
{
    public async Task<IReadOnlyList<ProductResponse>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken
    ) =>
        await northwindContext
            .Products.AsNoTracking()
            .OrderBy(product => product.ProductId)
            .ProjectTo<ProductResponse>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
}

public sealed record GetProductByIdQuery(int ProductId) : IRequest<ProductResponse?>;

public sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(query => query.ProductId).GreaterThan(0);
    }
}

public sealed class GetProductByIdQueryHandler(
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<GetProductByIdQuery, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken
    ) =>
        await northwindContext
            .Products.AsNoTracking()
            .Where(product => product.ProductId == request.ProductId)
            .ProjectTo<ProductResponse>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(cancellationToken);
}
