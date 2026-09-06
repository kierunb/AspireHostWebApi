using AutoMapper;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Profiles;

public sealed class SupplierProfile : Profile
{
    public SupplierProfile()
    {
        CreateMap<Suppliers, SupplierResponse>();
    }
}