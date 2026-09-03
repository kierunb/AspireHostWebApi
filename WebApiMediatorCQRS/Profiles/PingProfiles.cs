using AutoMapper;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Profiles;

public class PingProfiles : Profile
{
    public PingProfiles()
    {
        CreateMap<PingRequest, PingCommand>();
        CreateMap<PingCommandResponse, PingResponse>();
    }
}
