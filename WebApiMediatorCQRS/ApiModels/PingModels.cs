namespace WebApiMediatorCQRS.ApiModels;

public record PingRequest(Guid Id, string Message);
public record PingResponse(string Message);

