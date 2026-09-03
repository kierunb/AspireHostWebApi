using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public class PingEndpoint
{
    [Post("repr/ping")]
    public static async Task<PingCommandResponse> Handle(
        PingRequest request,
        IMediator mediator,
        IMapper mapper
    ) => await mediator.Send(mapper.Map<PingCommand>(request));
}

