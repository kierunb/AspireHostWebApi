using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Queries;

public record GetAllCustomersQuery : IRequest<IEnumerable<GetAllCustomersQueryResponse>>;

public record GetAllCustomersQueryResponse
{
    public string CustomerId { get; set; } = default!;

    public string CompanyName { get; set; } = default!;

    public string ContactName { get; set; } = default!;

    public string ContactTitle { get; set; } = default!;

    public string Address { get; set; } = default!;

    public string City { get; set; } = default!;

    public string Region { get; set; } = default!;

    public string PostalCode { get; set; } = default!;

    public string Country { get; set; } = default!;

    public string Phone { get; set; } = default!;

    public string Fax { get; set; } = default!;
}

public class GetAllCustomersQueryHandler(
    ILogger<GetAllCustomersQueryHandler> logger,
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<GetAllCustomersQuery, IEnumerable<GetAllCustomersQueryResponse>>
{
    public async Task<IEnumerable<GetAllCustomersQueryResponse>> Handle(
        GetAllCustomersQuery request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Handling {type}", typeof(GetAllCustomersQuery).Name);

        return await northwindContext
            .Customers.ProjectTo<GetAllCustomersQueryResponse>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
