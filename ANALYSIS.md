--

## Architecture Analysis

### Layered Architecture

NopCommerce uses a classic layered architecture with clear dependency rules:

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

### IEventPublisher — Internal Event Bus

nopCommerce uses an in-process pub/sub dispatcher called IEventPublisher. When code calls `PublishAsync(new SomeEvent(...))`, the EventPublisher resolves all registered `IConsumer<SomeEvent>` handlers from DI and invokes them sequentially. It does not use an external message broker, everything runs in-memory within the same process. Consumers are discovered at startup by assembly scanning.

This is a natural instrumentation boundary for flows that rely on events (e.g., order placement triggers inventory and email events). The search flow does not use IEventPublisher, it calls services directly from controllers and factories.

### Where Observability Is Easy

- **Middleware pipeline** — ASP.NET Core's middleware chain is a natural insertion point for cross-cutting concerns. Any request-level instrumentation (tracing, logging, metrics) can be added with a single middleware registration without touching controllers or routes.
- **DI boundary** — Because services are registered as interfaces, they can be decorated at registration time. This means observability can be added as a layer around existing services without modifying business logic.
- **Centralized registration** — All service registrations live in NopStartup.cs files, making it easy to identify all services in one place and apply instrumentation consistently.

### Where Observability Is Hard

- **Monolithic service methods** — Methods like SearchProductsAsync call into multiple other services (category, specification, pricing) within a single method body. Understanding the internal breakdown of work requires instrumenting each sub-service individually.

