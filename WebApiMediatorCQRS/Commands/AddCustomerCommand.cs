using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Commands;

public record AddCustomerCommand : IRequest<AddCustomerCommandResponse?>
{
    public string CustomerId { get; set; } = default!;

    public string CompanyName { get; set; } = default!;

    public string? ContactName { get; set; }

    public string? ContactTitle { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }
}

public class AddCustomerCommandValidator : AbstractValidator<AddCustomerCommand>
{
    public AddCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().Length(5);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ContactName).MaximumLength(30);
        RuleFor(x => x.ContactTitle).MaximumLength(30);
        RuleFor(x => x.Address).MaximumLength(60);
        RuleFor(x => x.City).MaximumLength(15);
        RuleFor(x => x.Region).MaximumLength(15);
        RuleFor(x => x.PostalCode).MaximumLength(10);
        RuleFor(x => x.Country).MaximumLength(15);
        RuleFor(x => x.Phone).MaximumLength(24);
        RuleFor(x => x.Fax).MaximumLength(24);
    }
}

public record AddCustomerCommandResponse
{
    public string CustomerId { get; set; } = default!;

    public string CompanyName { get; set; } = default!;

    public string? ContactName { get; set; }

    public string? ContactTitle { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }
}

public class AddCustomerCommandHandler(
    ILogger<AddCustomerCommandHandler> logger,
    IMapper mapper,
    NorthwindContext northwindContext
) : IRequestHandler<AddCustomerCommand, AddCustomerCommandResponse?>
{
    public async Task<AddCustomerCommandResponse?> Handle(
        AddCustomerCommand request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Handling {type}, CustomerId: {customerId}",
            typeof(AddCustomerCommand).Name,
            request.CustomerId
        );

        var customerExists = await northwindContext.Customers.AnyAsync(
            x => x.CustomerId == request.CustomerId,
            cancellationToken
        );
        if (customerExists)
            return null;

        var customer = mapper.Map<Customers>(request);
        await northwindContext.Customers.AddAsync(customer, cancellationToken);
        await northwindContext.SaveChangesAsync(cancellationToken);

        return mapper.Map<AddCustomerCommandResponse>(customer);
    }
}
