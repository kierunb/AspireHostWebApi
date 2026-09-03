using AutoMapper;
using WebApiMediatorCQRS.Database;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Profiles;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customers, GetCustomerByIdQueryResponse>();
    }
}
