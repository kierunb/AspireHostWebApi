using AutoMapper;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Database;

namespace WebApiMediatorCQRS.Profiles;

public sealed class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Products, ProductResponse>();
    }
}
