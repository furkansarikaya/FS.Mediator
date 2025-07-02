# Getting Started with FS.Mediator

```mermaid
flowchart TD
    A[Install Package] --> B[Define Request]
    B --> C[Create Handler]
    C --> D[Register Services]
    D --> E[Send Request]
```

## Installation

```bash
dotnet add package FS.Mediator
```

## Basic Usage

### 1. Define a Request
```csharp
public record GetUserById(int Id) : IRequest<User>;
```

### 2. Create a Handler
```csharp
public class GetUserByIdHandler : IRequestHandler<GetUserById, User>
{
    public async Task<User> Handle(GetUserById request, CancellationToken ct)
    {
        return await _userRepository.GetByIdAsync(request.Id, ct);
    }
}
```

### 3. Register Services
```csharp
// In Startup.cs or Program.cs
services.AddFSMediator(cfg => 
{
    cfg.RegisterHandlersFromAssemblyContaining<Startup>();
});
```

### 4. Send Requests
```csharp
var user = await _mediator.Send(new GetUserById(123));
```

## Project Structure

```mermaid
flowchart LR
    A[API/UI Layer] -->|Requests| B[Mediator]
    B -->|Handles| C[Domain Layer]
    C -->|Uses| D[Data Layer]
```

## Next Steps

- [Learn Core Concepts](../basic-concepts.md)
- [Explore Examples](../examples/)
- [Configure Behaviors](../configuration/behaviors.md)

## Troubleshooting

1. **Handler Not Found**:
   - Verify handler registration
   - Check assembly scanning

2. **Dependency Issues**:
   - Ensure handlers are registered in DI

3. **Performance Problems**:
   - Review [Performance Tips](../streaming/performance-tips.md)