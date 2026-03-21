## nopCommerce Layered Architecture

nopCommerce uses a classic layered architecture with clear dependency rules:

### Layers (from lowest to highest)

- **Nop.Core**: Domain entities, interfaces, and infrastructure. No dependencies on other project layers.
- **Nop.Data**: Data access (repositories, migrations). Depends only on Nop.Core.
- **Nop.Services**: Business logic/services. Depends on Nop.Core and Nop.Data.
- **Nop.Web.Framework**: Shared MVC infrastructure for presentation and plugins. Depends on Nop.Core, Nop.Data, and Nop.Services.
- **Nop.Web**: The main ASP.NET Core MVC web app (controllers, views, entry point). Depends on all above layers.
- **Plugins**: Extension points, discovered and loaded at runtime. Depend on Nop.Web.Framework (and thus transitively on all lower layers).

### Dependency Rules

- Lower layers never reference higher ones (e.g., Nop.Core knows nothing about HTTP or MVC).
- Services use repositories from Nop.Data, not direct DB access.
- Plugins extend via interfaces and events, not by modifying core code.
- Presentation (Nop.Web) depends on all lower layers, but not vice versa.

This structure enforces separation of concerns, extensibility, and testability throughout the application.
## IEventPublisher

`IEventPublisher` in nopCommerce is an in-process event bus / pub-sub dispatcher.

It is used to publish an event object and dispatch it to all registered handlers for that event type.

### How it works

When code calls:

```csharp
await eventPublisher.PublishAsync(new SomeEvent(...));
```

the `EventPublisher` resolves all registered consumers of:

```csharp
IConsumer<SomeEvent>
```

and invokes their:

```csharp
HandleEventAsync(SomeEvent eventMessage)
```

method one by one.

### Important details

- It does not send events to an external queue or broker.
- It does not use RabbitMQ, Kafka, or a background worker system.
- It runs in memory inside the same application process.
- Consumers are discovered at startup by scanning loaded assemblies for classes implementing `IConsumer<T>`.
- Those consumers are registered in dependency injection.
- At runtime, `IEventPublisher` asks DI for all matching consumers for the published event type.
- All matching handlers are executed, not just the first one.
- Handlers are executed sequentially, not in parallel.
- Processing can stop early only if the event implements `IStopProcessingEvent` and sets `StopProcessing = true`.

### Mental model

- Event = "something happened"
- `IEventPublisher` = dispatcher
- `IConsumer<TEvent>` = subscriber/handler

### End-to-end example: EmailSubscribedEvent

The event object is defined in [src/Libraries/Nop.Core/Domain/Messages/EmailSubscribedEvent.cs](src/Libraries/Nop.Core/Domain/Messages/EmailSubscribedEvent.cs).

The newsletter flow decides whether to publish the subscribe event in [src/Libraries/Nop.Services/Messages/NewsLetterSubscriptionService.cs](src/Libraries/Nop.Services/Messages/NewsLetterSubscriptionService.cs):

```csharp
if (isSubscribe)
	await _eventPublisher.PublishNewsLetterSubscribeAsync(subscription);
```

That helper method lives in [src/Libraries/Nop.Services/Messages/EventPublisherExtensions.cs](src/Libraries/Nop.Services/Messages/EventPublisherExtensions.cs) and creates the event:

```csharp
await eventPublisher.PublishAsync(new EmailSubscribedEvent(subscription));
```

The dispatch itself happens in [src/Libraries/Nop.Services/Events/EventPublisher.cs](src/Libraries/Nop.Services/Events/EventPublisher.cs), which resolves all:

```csharp
IConsumer<EmailSubscribedEvent>
```

and invokes each handler sequentially.

Two real consumers of this event are:

- [src/Plugins/Nop.Plugin.Misc.Brevo/Services/EventConsumer.cs](src/Plugins/Nop.Plugin.Misc.Brevo/Services/EventConsumer.cs)
- [src/Plugins/Nop.Plugin.Misc.Omnisend/Infrastructure/EventConsumer.cs](src/Plugins/Nop.Plugin.Misc.Omnisend/Infrastructure/EventConsumer.cs)

Brevo handles it by subscribing the contact:

```csharp
public async Task HandleEventAsync(EmailSubscribedEvent eventMessage)
{
	if (!BrevoManager.IsConfigured(_brevoSettings))
		return;

	await _brevoEmailManager.SubscribeAsync(eventMessage.Subscription);
}
```

Omnisend handles it by updating or creating the contact:

```csharp
public async Task HandleEventAsync(EmailSubscribedEvent eventMessage)
{
	if (!_omnisendService.IsConfigured)
		return;

	await _omnisendService.UpdateOrCreateContactAsync(eventMessage.Subscription, true);
}
```

Flow summary:

1. Newsletter service publishes `EmailSubscribedEvent`.
2. `EventPublisher` resolves all `IConsumer<EmailSubscribedEvent>` handlers.
3. Brevo consumer runs.
4. Omnisend consumer runs.
5. Any other registered consumer for the same event would also run.

So `IEventPublisher` decouples the code that raises the event from the plugin code that reacts to it.



## Middleware

There are multiple middleware components in nopCommerce, they sit between the web server and the MVC pipeline, and can inspect/modify requests and responses.

They are good for observability purposes as well as in IEventPublisher scenarios where you want to publish an event for every request or response.


| Layer        | Project/Namespace         | Responsibility                              |
|--------------|--------------------------|----------------------------------------------|
| Middleware   | Nop.Web.Framework, Plugins | Cross-cutting concerns (auth, logging, etc.) |
| Presentation | Nop.Web                  | Controllers, Views, MVC logic                |
| Service      | Nop.Services             | Business logic, event publishing             |
| Data Access  | Nop.Data                 | Database access, repositories                |
| Core         | Nop.Core                 | Domain entities, interfaces, infrastructure  |


## Observability Challenges in Data Access

In nopCommerce, observability for data access is less centralized and more difficult to implement than in other layers:

- Repository methods (in `Nop.Data`) are called directly from services, with no universal decorator or middleware for database operations.
- To log queries, measure execution time, or track errors, you must either:
  - Add logging code to every repository method (repetitive and error-prone), or
  - Implement a custom base repository or decorator pattern (not provided out of the box).
- There is no single, central place for data access observability like there is for HTTP requests (middleware) or events (`IEventPublisher`).
- For consistent observability, you would need to create a custom solution (e.g., a base repository with logging, or use EF Core interceptors) to monitor all database operations.

**Summary:**
Observability at the data access layer requires extra effort and custom infrastructure, making it a weak spot compared to the middleware and event layers.

## Observability Challenges in Direct Service Calls

In nopCommerce, when controllers call service methods directly (such as `SearchProductsAsync` in `IProductService`), observability becomes more difficult:

- There is no central place (like middleware or `IEventPublisher`) to capture metrics, logs, or traces for these operations.
- You must instrument each service method individually to add observability, which is repetitive and easy to miss.
- This can lead to inconsistent monitoring and blind spots in business logic flows.
- Adding a decorator or aspect-oriented approach could help, but is not provided out of the box.

**Summary:**
Direct controller-to-service calls make it harder to achieve consistent, centralized observability compared to HTTP middleware or event-driven flows.


## Load testing and performance monitoring in nopCommerce

````bash
k6 run -e BASE_URL=http://localhost:5000 loadtest/search-browse.js
````

````bash
Otlp__Endpoint=http://localhost:4317 dotnet run --project src/Presentation/Nop.Web
````

````bash
docker compose up jaeger otel-collector prometheus grafana nopcommerce_database -d
````
