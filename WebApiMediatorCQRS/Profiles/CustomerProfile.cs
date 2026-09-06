using AutoMapper;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;
using WebApiMediatorCQRS.Database;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Profiles;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customers, GetAllCustomersQueryResponse>();
        CreateMap<Customers, GetCustomerByIdQueryResponse>();
        CreateMap<AddCustomerRequest, AddCustomerCommand>();
        CreateMap<AddCustomerCommand, Customers>();
        CreateMap<Customers, AddCustomerCommandResponse>();
        CreateMap<AddCustomerCommandResponse, AddCustomerResponse>();
    }
}
